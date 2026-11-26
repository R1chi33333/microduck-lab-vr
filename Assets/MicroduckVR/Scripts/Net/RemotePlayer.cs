using MicroduckVR.Audio;
using MicroduckVR.Comic;
using MicroduckVR.Custom;
using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>
    /// The other player's duck: a full duck body (same model, legs, gait and voice as the flock), posed from what
    /// their PC sends, blended at the playback time. It has no brain and no physics capsule of its own; the flock
    /// treats it as a player (PlayerTargets). Its sounds play from its own body.
    /// </summary>
    public class RemotePlayer : MonoBehaviour
    {
        public DuckBody Body { get; private set; }
        public DuckSetup Setup { get; private set; }
        public string DisplayName { get; private set; } = "DUCK";
        public bool LaserOn { get; private set; }
        public Vector3 LaserPoint { get; private set; }

        readonly Track<PlayerSample> track = new Track<PlayerSample>(PlayerSample.Lerp);
        DuckAudio au;
        SkatePair skates; bool skating;
        string customJson = "";
        static ComicBeam beam;                           // one laser for the other player, made once and reused

        /// <summary>A new duck for the other player, standing at pos, in their saved look.</summary>
        public static RemotePlayer Create(DuckSetup src, Vector3 pos, float yaw, string custom, string displayName)
        {
            var go = new GameObject("Other player");
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f));
            go.SetActive(false);
            go.AddComponent<DuckBody>();
            var s = go.AddComponent<DuckSetup>(); s.rigPrefab = src.rigPrefab; s.jointMap = src.jointMap; s.skin = SkinIds.Cream;
            go.SetActive(true);                                                     // builds the body (DuckSetup.Awake)
            if (s.Mover != null) s.Mover.enabled = false;                           // posed from the network, never pushed about here
            var a = go.AddComponent<DuckAudio>(); a.idleChirps = false; a.seed = 3;
            var rp = go.AddComponent<RemotePlayer>();
            rp.Setup = s; rp.Body = s.Body; rp.au = a; rp.DisplayName = LanDiscovery.CleanName(displayName);
            rp.skates = new SkatePair();
            rp.ApplyCustom(custom);
            PlayerTargets.Remote.Add(rp);
            ComicPop.Spawn("HELLO!", pos + Vector3.up * 0.36f, ComicColors.Yellow, 1.3f);
            return rp;
        }

        public void ApplyCustom(string json)
        {
            if (string.IsNullOrEmpty(json) || json == customJson) return;
            customJson = json;
            DuckCustom c = null;
            try { c = JsonUtility.FromJson<DuckCustom>(json); } catch (System.Exception) { }
            if (c == null) return;
            bool wasSkating = skating;
            if (wasSkating) SetSkates(false);                // the skates' own colours stay out of the repaint
            c.ApplyTo(gameObject, true);
            if (wasSkating) SetSkates(true);
        }

        public void AddSample(double t, PlayerSample s) => track.Add(t, s);

        public void Quack() { if (au != null) au.Chirp(); }
        public void Emote(EmoteKind kind, float dur) { if (Body != null) Body.PlayEmote(kind, dur); }
        public void Tip(Vector3 dir) { if (Body != null && !Body.Fallen) Body.StartTip(dir); }
        /// <summary>A word about the other player's duck pops up big over its head.</summary>
        public void Word(string word, string hex, float size) { if (Body != null) ComicPop.Spawn(word, Body.Pos + Vector3.up * 0.36f, hex, size); }

        void SetSkates(bool on)
        {
            if (on == skating || Body == null || skates == null) return;
            skating = on;
            if (on) { skates.Attach(Body); Body.Skating = true; Body.SkateLift = SkatePair.Height; }
            else { skates.Detach(); Body.Skating = false; Body.SkateLean = 0f; Body.Wobble = 0f; }
        }

        /// <summary>Pose the duck at the playback time (called once a frame by the session).</summary>
        public void Step(double renderTime, float dt)
        {
            if (Body == null) return;
            if (!track.Sample(renderTime, out var s)) { LaserOn = false; ShowLaser(false); return; }
            SetSkates(s.skating);
            Body.LookAt.gazeDir = s.gaze.sqrMagnitude > 1e-6f ? s.gaze : (Vector3?)null;
            Body.JawTarget = s.jaw; Body.JawStop = 0f;
            Body.Hop = s.hop;
            if (skating) { Body.SkateLean = s.lean; Body.Stroke = s.stroke; Body.StrokeSide = s.strokeSide >= 0f ? 1f : -1f; Body.Wobble = s.wobble; }
            Body.ReachState.target = s.reach ? s.reachTarget : (Vector3?)null;
            Body.ReachState.weight = s.reach ? s.reachWeight : 0f;
            var vel = new Vector3(s.vx, 0f, s.vz);
            if (Setup.Mover != null) Setup.Mover.transform.position = s.pos;       // the feet look for the ground from here
            Body.Step(dt, s.pos, s.yaw, vel, s.yawRate);
            if (skating) skates.Spin(Vector3.Dot(vel, GltfSpace.Forward(s.yaw)) * dt);

            LaserOn = s.laser; LaserPoint = s.laserPoint;
            ShowLaser(s.laser);
            if (s.laser) beam.Set(s.laserFrom, s.laserPoint);
        }

        static void ShowLaser(bool on)
        {
            if (beam == null) { if (!on) return; beam = new ComicBeam("Other player laser", ComicColors.Pink, 0.0028f, 0.016f); }
            beam.Show(on);
        }

        /// <summary>Gone: the duck, its capsule and its skates.</summary>
        public void Despawn()
        {
            PlayerTargets.Remote.Remove(this);
            LaserOn = false; ShowLaser(false);
            if (Body != null) ComicPop.Spawn("BYE!", Body.Pos + Vector3.up * 0.36f, ComicColors.Purple, 1.2f);
            if (skates != null) { if (skates.Left != null) Destroy(skates.Left.gameObject); if (skates.Right != null) Destroy(skates.Right.gameObject); }
            if (Setup != null && Setup.Mover != null) Destroy(Setup.Mover.gameObject);
            Destroy(gameObject);
        }

        void OnDestroy() { PlayerTargets.Remote.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { beam = null; }
    }
}
