using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Games
{
    /// <summary>
    /// Race round the whole house (port of games.js Race on the web raceHouse track): everyone lines up in the
    /// hallway, 3-2-1-GO, one lap through glowing gates (yours is the bright one), lap and position on the HUD.
    /// </summary>
    public class RaceGame : MonoBehaviour
    {
        public static RaceGame Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        public static bool Active => Instance != null && Instance.active;
        public static bool Frozen => Active && Instance.frozen;

        // web raceHouse with X mirrored: start line, then the checkpoints (centre, radius)
        static readonly (Vector3 p, float r)[] Cps =
        {
            (new Vector3(2.4f, 0f, 3.22f), 0.47f), (new Vector3(-2.6f, 0f, 3.22f), 0.45f), (new Vector3(-5.0f, 0f, 2.56f), 0.4f),
            (new Vector3(-5.3f, 0f, -0.6f), 0.5f), (new Vector3(-3.06f, 0f, -1.2f), 0.4f), (new Vector3(-0.4f, 0f, -1.35f), 0.45f),
            (new Vector3(3.06f, 0f, -0.8f), 0.4f), (new Vector3(4.6f, 0f, 0.7f), 0.5f), (new Vector3(5.0f, 0f, 2.56f), 0.4f), (new Vector3(3.6f, 0f, 3.22f), 0.45f),
        };
        /// <summary>Name of the gate parts that keep their own material (the chequered band) when the gate changes colour.</summary>
        const string KeepMaterial = "chk";
        const int Laps = 1;                       // a lap of the whole house is long at duck pace

        class Prog { public int next = 1, lap = 1; public bool done; public float time; }
        bool active, frozen; float countdown, t, endAt = -1f;
        readonly Dictionary<object, Prog> prog = new Dictionary<object, Prog>();
        readonly List<object> finish = new List<object>();
        readonly List<(Transform g, Material m)> gates = new List<(Transform, Material)>();
        Material gateOn, gateOff, gateStart;

        public static RaceGame Ensure()
        {
            if (Instance == null) Instance = new GameObject("Race game").AddComponent<RaceGame>();
            return Instance;
        }

        Material Unlit(string hex, string key) { var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), key); m.SetFloat(ShaderIds.Unlit, 1f); return m; }

        void BuildGates()
        {
            if (gates.Count > 0) return;
            gateOn = ComicLook.ToonMaterial(ComicLook.Hex(ComicColors.Yellow), "race_on"); gateOff = ComicLook.ToonMaterial(ComicLook.Hex(ComicColors.Purple), "race_off");
            gateStart = ComicLook.ToonMaterial(ComicLook.Hex(ComicColors.Green), "race_start");
            for (int i = 0; i < Cps.Length; i++)
            {
                var (p, r) = Cps[i];
                var nxt = Cps[(i + 1) % Cps.Length].p; var prv = Cps[(i - 1 + Cps.Length) % Cps.Length].p;
                var g = new GameObject("Race gate " + i).transform; g.SetParent(transform, false);
                g.position = new Vector3(p.x, WorldQueries.GroundAt(p.x, p.z, 0.3f), p.z);
                g.rotation = Quaternion.LookRotation(Flat(nxt - prv).normalized, Vector3.up);
                var m = i == 0 ? gateStart : gateOff;
                // a chunky comic arch: fat posts on feet, a banner with a checker strip and the gate number, a thick floor ring
                float w = r * 0.9f, top = 0.36f;      // clear of a duck's head (0.27 m)
                foreach (var sx in new[] { -1f, 1f })
                {
                    ToyKit.Cyl(g, new Vector3(sx * w, top / 2f, 0f), 0.016f, top, "#FFFFFF", default, 0.013f, 14);
                    ToyKit.Cyl(g, new Vector3(sx * w, 0.006f, 0f), 0.028f, 0.012f, "#FFFFFF", default, 0.022f, 16);
                    ToyKit.Ball(g, new Vector3(sx * w, top + 0.004f, 0f), Vector3.one * 0.036f, "#FFFFFF");
                }
                float bw = w * 2f + 0.03f, bh = 0.046f;
                ToyKit.Box(g, new Vector3(0f, top - 0.012f, 0f), new Vector3(bw, bh, 0.02f), "#FFFFFF", 0.35f);
                // checker strip along the lower edge of the banner, on both faces
                int nChk = Mathf.Max(6, Mathf.RoundToInt(bw / 0.022f)); float cw = (bw - 0.02f) / nChk;
                foreach (var fz in new[] { -1f, 1f })
                    for (int k = 0; k < nChk; k++)
                    {
                        var c = ToyKit.Box(g, new Vector3(-bw / 2f + 0.01f + (k + 0.5f) * cw, top - 0.027f + (k % 2) * 0.0f, fz * 0.0105f),
                            new Vector3(cw, 0.012f, 0.0015f), k % 2 == 0 ? ComicColors.Ink : "#FFF6E4", 0f);
                        c.name = KeepMaterial;
                        if (k % 2 == 0) { var c2 = ToyKit.Box(g, new Vector3(-bw / 2f + 0.01f + (k + 0.5f) * cw, top - 0.015f, fz * 0.0105f), new Vector3(cw, 0.012f, 0.0015f), "#FFF6E4", 0f); c2.name = KeepMaterial; }
                        else { var c2 = ToyKit.Box(g, new Vector3(-bw / 2f + 0.01f + (k + 0.5f) * cw, top - 0.015f, fz * 0.0105f), new Vector3(cw, 0.012f, 0.0015f), ComicColors.Ink, 0f); c2.name = KeepMaterial; }
                    }
                // a fat ring on the floor, with a cream inner band
                ToyKit.Ring(g, new Vector3(0f, 0.003f, 0f), r * 0.88f, r * 0.74f, 0.006f, "#FFFFFF");
                var inner = ToyKit.Ring(g, new Vector3(0f, 0.0035f, 0f), r * 0.74f, r * 0.7f, 0.006f, "#FFF6E4"); inner.name = KeepMaterial;
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr.name != KeepMaterial) mr.sharedMaterial = m;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                // the gate's number, big, on both sides above the banner
                var font = GameHud.Get().Font;
                foreach (var fz in new[] { -1f, 1f })
                {
                    var ng = new GameObject(KeepMaterial); ng.transform.SetParent(g, false);    // KeepMaterial: keeps its own material when the gate changes colour
                    ng.transform.localPosition = new Vector3(0f, top + 0.06f, 0f); ng.transform.localRotation = Quaternion.Euler(0f, fz > 0f ? 180f : 0f, 0f);
                    var nt = ng.AddComponent<TMPro.TextMeshPro>(); if (font != null) nt.font = font;
                    nt.text = i == 0 ? "GO" : i.ToString(); nt.fontSize = 0.9f; nt.alignment = TMPro.TextAlignmentOptions.Center; nt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    nt.color = ComicLook.Ink; nt.rectTransform.sizeDelta = new Vector2(0.3f, 0.1f);
                }
                gates.Add((g, m));
            }
            // a bouncing pointer over the gate to go for next: seen from across a room, over the furniture
            beacon = new GameObject("Next gate pointer").transform; beacon.SetParent(transform, false);
            ToyKit.Cyl(beacon, new Vector3(0f, 0.045f, 0f), 0.004f, 0.09f, ComicColors.Pink, default, 0.05f, 18);
            ToyKit.Cyl(beacon, new Vector3(0f, 0.115f, 0f), 0.02f, 0.05f, ComicColors.Pink, default, 0.02f, 14);
            var bm = Unlit(ComicColors.Pink, "race_beacon");
            foreach (var mr in beacon.GetComponentsInChildren<MeshRenderer>()) { mr.sharedMaterial = bm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            beacon.gameObject.SetActive(false);
        }
        Transform beacon;

        public void StartRace()
        {
            if (FootballGame.Active) FootballGame.Instance.StopGame();
            BuildGates(); foreach (var (g, _) in gates) g.gameObject.SetActive(true);
            active = true; frozen = true; countdown = 3.6f; t = 0f; endAt = -1f; prog.Clear(); finish.Clear();
            // grid behind the start line, facing along the hallway (-x)
            var c0 = Cps[0].p; var dir = Vector3.left; var side = Vector3.forward;
            var racers = new List<object>();
            if (PlayerDuck.Instance != null) racers.Add(PlayerDuck.Instance);
            foreach (var b in DuckBrain.All) racers.Add(b);
            for (int i = 0; i < racers.Count; i++)
            {
                int row = i / 2; float col = i % 2 == 1 ? 1f : -1f;
                var p = c0 - dir * (0.14f + row * 0.26f) + side * (col * 0.16f + (row % 2 == 1 ? 0.05f : 0f));
                float yaw = Mathf.Atan2(dir.x, dir.z);
                if (racers[i] is PlayerDuck pd) pd.Teleport(p + Vector3.up * 0.03f, yaw);
                else ((DuckBrain)racers[i]).Teleport(p, yaw);
                prog[racers[i]] = new Prog();
            }
            GameHud.Get().Caption("GET READY!");
        }

        public void StopRace() { active = false; frozen = false; foreach (var (g, _) in gates) g.gameObject.SetActive(false); if (beacon != null) beacon.gameObject.SetActive(false); GameHud.Get().Clear(); }

        /// <summary>The gate the player's duck should head for next (filming and tests).</summary>
        public Vector3? PlayerTarget() { var pd = PlayerDuck.Instance; return pd != null && prog.TryGetValue(pd, out var P) && !P.done ? Cps[P.next].p : (Vector3?)null; }
        public Vector3 TargetFor(DuckBrain d) => prog.TryGetValue(d, out var P) && !P.done ? Cps[P.next].p : d.Pos;
        public bool Done(DuckBrain d) => prog.TryGetValue(d, out var P) && P.done;

        static Vector3 PosOf(object o) => o is PlayerDuck pd ? pd.Body.Pos : ((DuckBrain)o).Pos;

        void Update()
        {
            if (!active) return;
            var hud = GameHud.Get();
            if (frozen)
            {
                int before = Mathf.CeilToInt(countdown); countdown -= Time.deltaTime; int after = Mathf.CeilToInt(countdown);
                if (after != before && after >= 1 && after <= 3) hud.Big(after.ToString(), ComicColors.Yellow, 0.7f);
                if (countdown <= 0.6f) { frozen = false; t = 0f; hud.Big("GO!", ComicColors.Green, 0.9f); }
                return;
            }
            t += Time.deltaTime;
            foreach (var kv in prog)
            {
                var P = kv.Value; if (P.done) continue;
                P.time = t;
                var (cp, r) = Cps[P.next]; var pos = PosOf(kv.Key);
                if (new Vector2(cp.x - pos.x, cp.z - pos.z).magnitude < r * 0.8f)
                {
                    if (P.next == 0)
                    {
                        if (P.lap >= Laps) { P.done = true; finish.Add(kv.Key); if (kv.Key is PlayerDuck) hud.Big(Place(finish.Count) + "!", ComicColors.Yellow, 2f); continue; }
                        P.lap++;
                        if (kv.Key is PlayerDuck) hud.Big("LAST LAP!", ComicColors.Pink, 1.2f);
                    }
                    P.next = (P.next + 1) % Cps.Length;
                }
            }
            // HUD and the player's next gate
            if (PlayerDuck.Instance != null && prog.TryGetValue(PlayerDuck.Instance, out var me))
            {
                for (int i = 0; i < gates.Count; i++)
                {
                    bool on = !me.done && i == me.next;
                    var mat = on ? gateOn : i == 0 ? gateStart : gateOff;
                    foreach (var mr in gates[i].g.GetComponentsInChildren<MeshRenderer>()) if (mr.name != KeepMaterial) mr.sharedMaterial = mat;
                    gates[i].g.localScale = Vector3.one * (on ? 1f + 0.05f * Mathf.Sin(Time.time * 6f) : 1f);
                }
                hud.Caption(me.done ? $"FINISHED {Place(finish.IndexOf(PlayerDuck.Instance) + 1)}  {me.time:0.0}s"
                    : $"{(Laps > 1 ? $"LAP {me.lap}/{Laps}   " : "")}GATE {(me.next == 0 ? Cps.Length : me.next)}/{Cps.Length}   {Place(Position(PlayerDuck.Instance))}   {t:0.0}s");
                // where to go: the pointer over the next gate, and arrows on the HUD when that gate is not in front of you
                beacon.gameObject.SetActive(!me.done);
                int turn = 0;
                if (!me.done)
                {
                    var gp = gates[me.next].g.position;
                    beacon.position = gp + Vector3.up * (0.56f + 0.03f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));
                    beacon.rotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
                    var ct = PlayerDuck.Instance.cam.transform;
                    float ang = Vector3.SignedAngle(Flat(ct.forward), Flat(gp - ct.position), Vector3.up);
                    if (!frozen && Mathf.Abs(ang) > 32f) turn = ang > 0f ? 1 : -1;
                }
                hud.Arrow(turn);
            }
            if (endAt < 0f && (finish.Count >= Mathf.Min(prog.Count, 3) || (PlayerDuck.Instance != null && prog.TryGetValue(PlayerDuck.Instance, out var pm) && pm.done))) endAt = t + 4f;
            if (endAt > 0f && t > endAt) StopRace();
        }

        int Position(object who)
        {
            float Score(object o)
            {
                var P = prog[o]; if (P.done) return 1e6f - P.time;
                var cp = Cps[P.next].p; var p = PosOf(o);
                return (P.lap - 1) * Cps.Length * 10f + ((P.next - 1 + Cps.Length) % Cps.Length) * 10f - new Vector2(cp.x - p.x, cp.z - p.z).magnitude;
            }
            float mine = Score(who); int n = 1;
            foreach (var o in prog.Keys) if (o != who && Score(o) > mine) n++;
            return n;
        }

        static string Place(int n) => n == 1 ? "1ST" : n == 2 ? "2ND" : n == 3 ? "3RD" : n + "TH";
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
