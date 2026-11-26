using System.Collections.Generic;
using MicroduckVR.Audio;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Games;
using MicroduckVR.Player;
using UnityEngine;

namespace MicroduckVR.Flock
{
    /// <summary>Adds ducks to the flock while you play (ADD DUCK in the toy bag) and sends the newest one home again.</summary>
    public static class FlockSpawner
    {
        public const int Max = 12, Min = 1;
        static readonly string[] Skins = { "cream", "lavender", "sky", "graphite" };
        static readonly List<GameObject> added = new List<GameObject>();
        static int serial;

        public static DuckBrain Add()
        {
            var pd = PlayerDuck.Instance; if (pd == null) return null;
            if (DuckBrain.All.Count >= Max) { GameHud.Get().Big("FULL HOUSE!", "#FF8A3D", 1.1f); return null; }
            var src = pd.GetComponent<DuckSetup>(); if (src == null || src.rigPrefab == null) return null;
            return Add(pd.Body.Pos, pd.cam.transform.forward);
        }

        /// <summary>A new duck a little in front of a duck standing at 'here' and looking along 'forward'
        /// (you, or the other player when you play together).</summary>
        public static DuckBrain Add(Vector3 here, Vector3 forward)
        {
            var pd = PlayerDuck.Instance; if (pd == null) return null;
            if (DuckBrain.All.Count >= Max) { GameHud.Get().Big("FULL HOUSE!", "#FF8A3D", 1.1f); return null; }
            var src = pd.GetComponent<DuckSetup>(); if (src == null || src.rigPrefab == null) return null;
            // a free spot on the floor a little in front of you
            var f = forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            Vector3 at = here + f * 0.35f;
            for (int i = 0; i < 12; i++)
            {
                var d = Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * (i / 2) * 30f, 0f) * f;
                var p = here + d * 0.35f; p.y = WorldQueries.GroundNear(p.x, p.z, here.y);
                if (Mathf.Abs(p.y - here.y) < 0.06f && !Physics.CheckCapsule(p + Vector3.up * 0.06f, p + Vector3.up * 0.16f, 0.05f, ~0, QueryTriggerInteraction.Ignore)) { at = p; break; }
            }
            serial++;
            string skin = Skins[Random.Range(0, Skins.Length)];
            int seed = 1000 + serial * 13 + Random.Range(0, 1000);
            var brain = Make(src, "Duck +" + serial, at, Quaternion.LookRotation(-f, Vector3.up), skin, seed, DuckBrain.All.Count);
            ComicPop.Spawn("QUACK!", at + Vector3.up * 0.26f, "#FFC43D", 1.2f);
            GameHud.Get().Big("NEW DUCK!", "#FFC43D", 1.0f);
            return brain;
        }

        /// <summary>Build a flock duck with this name, look and seed (also used to mirror the host's flock on a joining PC).</summary>
        public static DuckBrain Make(DuckSetup src, string name, Vector3 at, Quaternion rot, string skin, int seed, int index)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at, rot);
            go.SetActive(false);
            go.AddComponent<DuckBody>();
            var s = go.AddComponent<DuckSetup>(); s.rigPrefab = src.rigPrefab; s.jointMap = src.jointMap; s.skin = skin;
            go.SetActive(true);
            var brain = go.AddComponent<DuckBrain>(); brain.seed = seed; brain.index = index;
            go.AddComponent<DuckAudio>().seed = brain.seed;
            added.Add(go);
            return brain;
        }

        public static void Remove()
        {
            added.RemoveAll(g => g == null);
            GameObject go = added.Count > 0 ? added[added.Count - 1] : null;
            if (go == null && DuckBrain.All.Count > Min) go = DuckBrain.All[DuckBrain.All.Count - 1].gameObject;
            if (go == null || DuckBrain.All.Count <= Min) { GameHud.Get().Big("LAST DUCK STAYS!", "#FF8A3D", 1.1f); return; }
            if (FootballGame.Active) FootballGame.Instance.StopGame();
            if (RaceGame.Active) RaceGame.Ensure().StopRace();
            var s = go.GetComponent<DuckSetup>(); var brain = go.GetComponent<DuckBrain>();
            ComicPop.Spawn("BYE!", (brain != null ? brain.Pos : go.transform.position) + Vector3.up * 0.26f, "#7B61FF", 1.2f);
            if (s != null && s.Mover != null) Object.Destroy(s.Mover.gameObject);
            added.Remove(go); Object.Destroy(go);
        }
    }
}
