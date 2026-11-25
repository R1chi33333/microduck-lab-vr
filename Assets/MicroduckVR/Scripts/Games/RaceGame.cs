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
            gateOn = Unlit("#BF5AF2", "race_on"); gateOff = Unlit("#E5CCF5", "race_off"); gateStart = Unlit("#30D158", "race_start");
            for (int i = 0; i < Cps.Length; i++)
            {
                var (p, r) = Cps[i];
                var nxt = Cps[(i + 1) % Cps.Length].p; var prv = Cps[(i - 1 + Cps.Length) % Cps.Length].p;
                var g = new GameObject("Race gate " + i).transform; g.SetParent(transform, false);
                g.position = new Vector3(p.x, WorldQueries.GroundAt(p.x, p.z, 0.3f), p.z);
                g.rotation = Quaternion.LookRotation(Flat(nxt - prv).normalized, Vector3.up);
                var m = i == 0 ? gateStart : gateOff;
                // an arch over the track: two posts and a bar, plus a ring on the floor
                float w = r * 0.9f;
                foreach (var s in new[] { -1f, 1f }) ToyKit.Cyl(g, new Vector3(s * w, 0.11f, 0f), 0.008f, 0.22f, "#FFFFFF", default, -1f, 10);
                ToyKit.Box(g, new Vector3(0f, 0.225f, 0f), new Vector3(w * 2f + 0.03f, 0.03f, 0.016f), "#FFFFFF", 0.4f);
                ToyKit.Ring(g, new Vector3(0f, 0.002f, 0f), r * 0.85f, r * 0.75f, 0.002f, "#FFFFFF");
                foreach (var mr in g.GetComponentsInChildren<MeshRenderer>()) { mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
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
                if (countdown <= 0.6f) { frozen = false; t = 0f; hud.Big("GO!", "#30D158", 0.9f); }
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
                    foreach (var mr in gates[i].g.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = mat;
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
