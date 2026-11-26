using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Player;
using UnityEngine;

namespace MicroduckVR.Games
{
    /// <summary>
    /// Football on the living-room rug pitch (port of games.js Football): your team (orange) against the blue team,
    /// first ball you see is the match ball. Ducks chase in pairs, the rest hold position; walk into the ball to
    /// kick it. A goal pops GOAL!, the scoring team dances, and the ball goes back on the centre spot.
    /// </summary>
    public class FootballGame : MonoBehaviour
    {
        /// <summary>The football game, once made.</summary>
        public static FootballGame Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        /// <summary>A match is on.</summary>
        public static bool Active => Instance != null && Instance.active;

        // pitch (Unity space): centre, half length along z, goal mouth
        /// <summary>Centre spot of the rug pitch (Unity space).</summary>
        public static readonly Vector3 Centre = new Vector3(1.93f, 0f, 0.73f);
        /// <summary>Half the pitch length along z, and the goal mouth size (m).</summary>
        public const float Half = 1.245f, GoalWidth = 0.34f, GoalHeight = 0.16f, GoalDepth = 0.12f;

        bool active; float cool; Rigidbody ball;
        /// <summary>The match ball.</summary>
        public Rigidbody Ball => ball;
        /// <summary>Goals per team.</summary>
        public readonly int[] Score = new int[2];
        /// <summary>Each AI duck's team (0 = yours, 1 = blue).</summary>
        public readonly Dictionary<DuckBrain, int> Team = new Dictionary<DuckBrain, int>();
        /// <summary>The player's team.</summary>
        public int PlayerTeam => 0;

        /// <summary>The football game, made on first use.</summary>
        public static FootballGame Ensure()
        {
            if (Instance == null) Instance = new GameObject("Football game").AddComponent<FootballGame>();
            return Instance;
        }

        /// <summary>Centre of a team's goal mouth on the floor.</summary>
        public Vector3 GoalCentre(int team) => Centre + new Vector3(0f, 0f, team == 0 ? -Half : Half);
        /// <summary>Where the ball is (the centre spot when there is none).</summary>
        public Vector3 BallPos => ball != null ? ball.position : Centre;

        /// <summary>Kicks off a match (stops a race first).</summary>
        public void StartGame()
        {
            if (RaceGame.Active) RaceGame.Instance.StopRace();
            ball = null; float bd = 99f;
            foreach (var rb in FindObjectsByType<Rigidbody>())
                if (rb.name == "Football" || rb.name == "Sock ball") { float d = Vector3.Distance(rb.position, Centre); if (d < bd) { bd = d; ball = rb; } }
            if (ball == null) return;
            active = true; Score[0] = Score[1] = 0; cool = 0f; Team.Clear();
            int k = 0;
            foreach (var b in DuckBrain.All) Team[b] = (k++ % 2 == 0) ? 1 : 0;      // 3 blue, 3 orange beside you
            ResetBall();
            // everyone onto the pitch: line up in their own half
            foreach (var kv in Team)
            {
                var hp = HomeSpot(kv.Key); int tm = kv.Value;
                kv.Key.Teleport(hp + new Vector3(0f, 0f, tm == 0 ? -0.1f : 0.1f), tm == 0 ? 0f : Mathf.PI);
            }
            var p = PlayerDuck.Instance;
            if (p != null) p.Teleport(Centre + new Vector3(0f, 0.02f, -0.3f), 0f);
            GameHud.Get().Big("KICK OFF!", "#FF9F0A", 1.2f); Caption();
        }

        /// <summary>Ends the match and clears the HUD.</summary>
        public void StopGame() { active = false; GameHud.Get().Clear(); }

        void Caption() => GameHud.Get().Caption($"ORANGE {Score[0]}  :  {Score[1]} BLUE");

        void ResetBall()
        {
            ball.linearVelocity = Vector3.zero; ball.angularVelocity = Vector3.zero;
            ball.position = Centre + Vector3.up * 0.05f; ball.transform.position = ball.position;
        }

        /// <summary>The two duck team-mates nearest the ball chase it; the others hang back.</summary>
        public bool IsChaser(DuckBrain d)
        {
            if (!Team.TryGetValue(d, out int team)) return false;
            var b = BallPos; int closer = 0; float my = Flat(d.Pos - b).magnitude;
            foreach (var kv in Team) if (kv.Key != d && kv.Value == team && !kv.Key.Body.Fallen && Flat(kv.Key.Pos - b).magnitude < my) closer++;
            return closer < 2;
        }

        /// <summary>Where a duck that is not chasing stands.</summary>
        public Vector3 HomeSpot(DuckBrain d)
        {
            int team = Team.TryGetValue(d, out var t) ? t : 0;
            var p = Vector3.Lerp(GoalCentre(team), Centre, 0.45f);
            p.x += ((d.index % 3) - 1) * 0.14f;
            return p;
        }

        void Update()
        {
            if (!active) return;
            if (ball == null) { StopGame(); return; }
            if (cool > 0f) { cool -= Time.deltaTime; if (cool <= 0f) ResetBall(); return; }
            var b = ball.position;
            float along = b.z - Centre.z, across = b.x - Centre.x;
            if (Mathf.Abs(along) > Half + 0.01f && Mathf.Abs(across) < GoalWidth / 2f && b.y < GoalHeight)
            {
                int scorer = along < 0f ? 1 : 0;         // into the orange goal (z-) is a goal for blue
                Score[scorer]++; cool = 2.2f; Caption();
                GameHud.Get().Big("GOAL!", scorer == 0 ? "#FF9F0A" : "#64D2FF", 1.6f);
                foreach (var kv in Team) if (kv.Value == scorer) kv.Key.Body.PlayEmote(EmoteKind.Dance);
                if (scorer == PlayerTeam && PlayerDuck.Instance != null) PlayerDuck.Instance.Body.PlayEmote(EmoteKind.Dance);
                return;
            }
            if (b.y < -0.1f || Mathf.Abs(across) > 0.75f || Mathf.Abs(along) > Half + 0.3f) cool = 0.8f;   // out of play
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
