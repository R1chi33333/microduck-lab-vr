using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Player
{
    /// <summary>Watches the player's duck and the toys and throws out sound words at the right moments.</summary>
    public class ComicEvents : MonoBehaviour
    {
        int chirps = -1, grabs = -1; bool boosting, inWater, paddle; float scanT, powCd, waterT;
        readonly Dictionary<MicroduckVR.Flock.DuckBrain, bool> wet = new Dictionary<MicroduckVR.Flock.DuckBrain, bool>();
        readonly Dictionary<Rigidbody, float> speeds = new Dictionary<Rigidbody, float>();
        readonly List<Rigidbody> props = new List<Rigidbody>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindAnyObjectByType<ComicEvents>() == null) new GameObject("Comic events").AddComponent<ComicEvents>(); }

        /// <summary>A word about the player's own duck. Through your own eyes it is small, off to the side of what you are
        /// looking at, and travels with you; seen from outside (third person, filming) it pops big over the duck's head.</summary>
        public static void Own(PlayerDuck pd, string word, string hex, float size)
        {
            Coop.SendOwnWord(word, hex, size);          // playing together: it pops over this duck on the other PC
            var c = pd.cam.transform; var f = c.forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            if (pd.thirdPerson || ComicPop.Viewer != null) { ComicPop.Spawn(word, pd.Body.Pos + Vector3.up * 0.36f, hex, size); return; }
            var right = Vector3.Cross(Vector3.up, f);
            ComicPop.Spawn(word, c.position + f * 0.32f + Vector3.up * 0.06f + right * Random.Range(0.09f, 0.12f), hex, size * 0.6f, 0.9f, c);
        }

        void Update()
        {
            var pd = PlayerDuck.Instance; if (pd == null || pd.cam == null || pd.Body == null) return;
            float dt = Time.deltaTime; powCd -= dt;
            if (chirps < 0) { chirps = pd.ChirpCount; grabs = pd.GrabCount; }
            if (pd.ChirpCount != chirps) { chirps = pd.ChirpCount; Own(pd, "QUACK!", ComicColors.Yellow, 1f); }
            if (pd.GrabCount != grabs) { grabs = pd.GrabCount; Own(pd, "CHOMP!", ComicColors.Pink, 0.85f); }
            var sk = pd.Skates;
            bool b = sk != null && sk.On && sk.Boosting;
            if (b && !boosting) Own(pd, "ZOOM!", ComicColors.Blue, 1.1f);
            boosting = b;

            waterT -= dt;
            if (waterT <= 0f)
            {
                waterT = 0.1f;
                bool w = Physics.Raycast(pd.Body.Pos + Vector3.up * 0.05f, Vector3.down, out var hit, 0.3f, 1 << Layers.World, QueryTriggerInteraction.Ignore)
                         && hit.collider.GetComponent<SurfaceTag>() is SurfaceTag st && st.Kind == SurfaceType.Water;
                if (w && !inWater) { Own(pd, "SPLASH!", ComicColors.Blue, 1.1f); ComicSplash.Spawn(pd.Body.Pos, 1.2f); Coop.SendSplash(pd.Body.Pos, 1.2f); }
                else if (w && pd.Speed > 0.03f && (paddle = !paddle)) { ComicSplash.Spawn(pd.Body.Pos, 0.7f); Coop.SendSplash(pd.Body.Pos, 0.7f); }     // paddling about: a small one every other look
                inWater = w;
                // the other ducks splash too
                foreach (var d in MicroduckVR.Flock.DuckBrain.All)
                {
                    if (d == null) continue;
                    bool dw = d.Surface == SurfaceType.Water; wet.TryGetValue(d, out bool was);
                    if (dw && !was) ComicSplash.Spawn(d.Pos, 1f);
                    wet[d] = dw;
                }
            }

            // a toy suddenly sent flying near you: POW!
            scanT -= dt;
            if (scanT <= 0f)
            {
                scanT = 1.5f; props.Clear(); speeds.Clear();
                foreach (var rb in FindObjectsByType<Rigidbody>())
                    if (rb.gameObject.layer == Layers.Prop && !rb.isKinematic && ((rb.position - pd.Body.Pos).sqrMagnitude < 9f || MicroduckVR.Net.PlayerTargets.NearRemote(rb.position, 3f))) { props.Add(rb); speeds[rb] = rb.linearVelocity.magnitude; }
            }
            foreach (var rb in props)
            {
                if (rb == null) continue;
                float s = rb.linearVelocity.magnitude, last = speeds.TryGetValue(rb, out var l) ? l : s;
                if (s - last > 0.45f && powCd <= 0f) { powCd = 0.7f; 
                    // far ones are drawn bigger, so they still read from across the room
                    var eye = ComicPop.Viewer != null ? ComicPop.Viewer.transform.position : pd.cam.transform.position;
                    string pow = Random.value < 0.5f ? "POW!" : "BONK!";
                    ComicPop.Spawn(pow, rb.worldCenterOfMass + Vector3.up * 0.1f, ComicColors.Orange, 1.2f * Mathf.Clamp(Vector3.Distance(eye, rb.worldCenterOfMass), 1f, 3f));
                    Coop.SendPow(pow, rb.worldCenterOfMass + Vector3.up * 0.1f, ComicColors.Orange);     // the host simulates toys: the joining PC sees it too
                }
                speeds[rb] = s;
            }
        }
    }
}
