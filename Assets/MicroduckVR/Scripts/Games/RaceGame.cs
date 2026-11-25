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
        public static bool Active => Instance != null && Instance.active;
        public static bool Frozen => Active && Instance.frozen;

        // web raceHouse with X mirrored: start line, then the checkpoints (centre, radius)
        static readonly (Vector3 p, float r)[] Cps =
        {
            (new Vector3(2.4f, 0f, 3.22f), 0.47f), (new Vector3(-2.6f, 0f, 3.22f), 0.45f), (new Vector3(-5.0f, 0f, 2.56f), 0.4f),
            (new Vector3(-5.3f, 0f, -0.6f), 0.5f), (new Vector3(-3.06f, 0f, -1.2f), 0.4f), (new Vector3(-0.4f, 0f, -1.35f), 0.45f),
            (new Vector3(3.06f, 0f, -0.8f), 0.4f), (new Vector3(4.6f, 0f, 0.7f), 0.5f), (new Vector3(5.0f, 0f, 2.56f), 0.4f), (new Vector3(3.6f, 0f, 3.22f), 0.45f),
        };
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

        Material Unlit(string hex, string key) { var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), key); m.SetFloat("_Unlit", 1f); return m; }

        void BuildGates()
        {
            if (gates.Count > 0) return;
            gateOn = ComicLook.ToonMaterial(ComicLook.Hex("#FFC43D"), "race_on"); gateOff = ComicLook.ToonMaterial(ComicLook.Hex("#7B61FF"), "race_off");
            gateStart = ComicLook.ToonMaterial(ComicLook.Hex("#8FD35A"), "race_start");
            for (int i = 0; i < Cps.Length; i++)
            {
                var (p, r) = Cps[i];
                var nxt = Cps[(i + 1) % Cps.Length].p; var prv = Cps[(i - 1 + Cps.Length) % Cps.Length].p;
                var g = new GameObject("Race gate " + i).transform; g.SetParent(transform, false);
                g.position = new Vector3(p.x, WorldQueries.GroundAt(p.x, p.z, 0.3f), p.z);
                g.rotation = Quaternion.LookRotation(Flat(nxt - prv).normalized, Vector3.up);
                var m = i == 0 ? gateStart : gateOff;
                // a chunky comic arch: fat posts on feet, a banner with a checker strip and the gate number, a thick floor ring
                float w = r * 0.9f, top = 0.22f;
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
                            new Vector3(cw, 0.012f, 0.0015f), k % 2 == 0 ? "#1B1430" : "#FFF6E4", 0f);
                        c.name = "chk";
                        if (k % 2 == 0) { var c2 = ToyKit.Box(g, new Vector3(-bw / 2f + 0.01f + (k + 0.5f) * cw, top - 0.015f, fz * 0.0105f), new Vector3(cw, 0.012f, 0.0015f), "#FFF6E4", 0f); c2.name = "chk"; }
                        else { var c2 = ToyKit.Box(g, new Vector3(-bw / 2f + 0.01f + (k + 0.5f) * cw, top - 0.015f, fz * 0.0105f), new Vector3(cw, 0.012f, 0.0015f), "#1B1430", 0f); c2.name = "chk"; }
                    }
                // a fat ring on the floor, with a cream inner band
                ToyKit.Ring(g, new Vector3(0f, 0.003f, 0f), r * 0.88f, r * 0.74f, 0.006f, "#FFFFFF");
                var inner = ToyKit.Ring(g, new Vector3(0f, 0.0035f, 0f), r * 0.74f, r * 0.7f, 0.006f, "#FFF6E4"); inner.name = "chk";
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr.name != "chk") mr.sharedMaterial = m;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                gates.Add((g, m));
            }
        }

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

        public void StopRace() { active = false; frozen = false; foreach (var (g, _) in gates) g.gameObject.SetActive(false); GameHud.Get().Clear(); }

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
                if (after != before && after >= 1 && after <= 3) hud.Big(after.ToString(), "#FFC43D", 0.7f);
                if (countdown <= 0.6f) { frozen = false; t = 0f; hud.Big("GO!", "#8FD35A", 0.9f); }
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
                        if (P.lap >= Laps) { P.done = true; finish.Add(kv.Key); if (kv.Key is PlayerDuck) hud.Big(Place(finish.Count) + "!", "#FFC43D", 2f); continue; }
                        P.lap++;
                        if (kv.Key is PlayerDuck) hud.Big("LAST LAP!", "#FF5D73", 1.2f);
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
                    foreach (var mr in gates[i].g.GetComponentsInChildren<MeshRenderer>()) if (mr.name != "chk") mr.sharedMaterial = mat;
                    gates[i].g.localScale = Vector3.one * (on ? 1f + 0.05f * Mathf.Sin(Time.time * 6f) : 1f);
                }
                hud.Caption(me.done ? $"FINISHED {Place(finish.IndexOf(PlayerDuck.Instance) + 1)}  {me.time:0.0}s" : $"LAP {me.lap}/{Laps}   {Position(PlayerDuck.Instance)}/{prog.Count}   {t:0.0}s");
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
