using System;
using System.Collections.Generic;
using MicroduckVR.Core;
using MicroduckVR.Duck;
using MicroduckVR.Net;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Flock
{
    /// <summary>
    /// The AI duck's brain (port of agent.js think / _pick / _steerAvoid / _route): follow the player,
    /// roam to another room through the doorways, wander, fetch a toy (walk up, bow, bite, carry, drop),
    /// sit, idle with emotes. Boids-style spacing and a whisker probe keep ducks apart and off ledges.
    /// Publishes its intent (state + context) for the thought bubbles and fires events (got, drop, bed...).
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(DuckSetup))]
    public class DuckBrain : MonoBehaviour
    {
        public static readonly List<DuckBrain> All = new List<DuckBrain>();

        // ---- a card or board the player is reading: ducks do not stand between it and the player's eyes
        static Vector3 readZone; static float readRadius, readUntil; static bool readAlways;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); readZone = Vector3.zero; readRadius = 0f; readUntil = 0f; readAlways = false; }
        static readonly EmoteKind[] IdleEmotes = { EmoteKind.Nod, EmoteKind.Look, EmoteKind.Dance, EmoteKind.Shake };
        static readonly EmoteKind[] PauseEmotes = { EmoteKind.Look, EmoteKind.Nod, EmoteKind.Dance };
        /// <summary>Call every frame while something is being read: keeps the flock out of the way between eyes and panel.
        /// 'always' also during a match or a race (filming).</summary>
        public static void ClearView(Vector3 eye, Vector3 panel, bool always = false)
        {
            readZone = Vector3.Lerp(eye, panel, 0.6f); readRadius = Mathf.Max(0.2f, Vector3.Distance(eye, panel) * 0.55f); readUntil = Time.time + 0.25f; readAlways = always;
        }
        public int seed = 1;
        public int index;

        public AiState State { get; private set; } = AiState.Idle;
        public string Context { get; private set; } = "";        // room name, toy label...
        public event Action<DuckBrain, DuckEventKind, string> OnEvent;  // (duck, kind, context)
        public DuckBody Body => body;
        public Vector3 Pos => mover != null ? mover.transform.position : transform.position;
        public float Speed { get; private set; }
        public SurfaceType Surface { get; private set; } = SurfaceType.Wood;

        DuckBody body; CharacterController mover;
        System.Random rng;
        float t, dur, yaw, yawRate, vy, stuckT, bedT, boing;
        Vector3 vel, lastPos;
        Vector3? target;
        List<Vector3> route; int ri; float best, routeStuck;
        // toy task
        enum TaskPhase { None, Approach, Reach, Carry, Drop }
        Grabbable toy, held; float taskT; TaskPhase taskPhase;

        float R(float a = 0f, float b = 1f) => a + (float)rng.NextDouble() * (b - a);

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            var s = GetComponent<DuckSetup>(); body = s.Body; mover = s.Mover;
            rng = new System.Random(seed); yaw = body.Yaw; lastPos = Pos;
            if (!Puppet) { Pick(null); t = R(0f, dur * 0.6f); }
        }

        // ------------------------------------------------------------------ playing together (joining PC)
        /// <summary>On a PC that joined someone else's house the host's brain decides: this duck only plays back what
        /// the host sends (MicroduckVR.Net.NetFlock poses it). Off again, it carries on with a mind of its own.</summary>
        public bool Puppet { get; private set; }

        public void SetPuppet(bool on)
        {
            if (body == null || mover == null) { var s = GetComponent<DuckSetup>(); body = s.Body; mover = s.Mover; }
            if (Puppet == on) return;
            if (on)
            {
                DropHeld(); pounceT = -1f; target = null; route = null;
                body.SitTarget = false; body.CrouchTarget = false; body.LookAt.weight = 0f;
            }
            Puppet = on;
            if (mover != null) mover.enabled = !on;                     // posed by hand while a puppet
            if (!on)
            {
                if (rng == null) rng = new System.Random(seed);
                yaw = body.Yaw; vel = Vector3.zero; vy = 0f; lastPos = Pos;
                body.Hop = 0f; body.SitTarget = false; body.CrouchTarget = false; body.LookAt.target = null; body.LookAt.weight = 0f;
                body.ReachState.target = null; body.ReachState.weight = 0f; body.JawTarget = 0f; body.JawStop = 0f;
                Pick(AiState.Idle);
            }
        }

        /// <summary>What the host's brain says this duck is doing (the thought bubbles read it).</summary>
        public void PuppetState(AiState state, string context) { State = state; Context = context ?? ""; }
        /// <summary>A toy event the host's duck had (got, drop): bubbles and chirps here too.</summary>
        public void PuppetEvent(DuckEventKind kind, string context) => OnEvent?.Invoke(this, kind, context);

        /// <summary>Pose the puppet: the body walks to the host's pose; the floor under it is looked up here (splashes, boings).</summary>
        public void PuppetStep(float dt, Vector3 p, float yawRad, Vector3 v, float w)
        {
            if (body == null) return;
            var surf = SurfaceTag.At(p);
            SurfaceType st = surf != null ? surf.Kind : SurfaceType.Wood;
            if (st != Surface) { Surface = st; if (st == SurfaceType.Bed || st == SurfaceType.Water || st == SurfaceType.Tile) OnEvent?.Invoke(this, SurfaceEvent(st), ""); }
            if (mover != null) mover.transform.position = p;
            Speed = Flat(p - lastPos).magnitude / Mathf.Max(dt, 1e-4f);
            lastPos = p; yaw = yawRad; yawRate = w;
            body.Step(dt, p, yawRad, Flat(v), w);
        }

        // ------------------------------------------------------------------ decisions
        void Pick(AiState? force)
        {
            float r = R();
            t = 0f; target = null; route = null; body.SitTarget = false;
            if (held == null) { toy = null; taskPhase = TaskPhase.None; body.ReachState.weight = 0f; body.JawTarget = 0f; }
            State = force ?? (r < 0.3f ? AiState.Follow : r < 0.42f ? AiState.Roam : r < 0.66f ? AiState.Wander : r < 0.8f ? AiState.Toy : r < 0.9f ? AiState.Sit : AiState.Idle);
            if (State == AiState.Follow && PlayerTargets.Count == 0) State = AiState.Wander;
            dur = State switch { AiState.Roam => 40f, AiState.Follow => R(8f, 16f), AiState.Wander => R(7f, 13f), AiState.Toy => 14f, AiState.Sit => R(3f, 6f), _ => R(1.5f, 3.5f) };
            Context = "";
            if (State == AiState.Idle && R() < 0.5f) body.PlayEmote(IdleEmotes[rng.Next(4)]);
        }

        /// <summary>Put the duck somewhere at once (race grid, duck swap): drops what it carries, stops what it was doing.</summary>
        public void Teleport(Vector3 pos, float yawRad)
        {
            if (held != null) { held.Release(Vector3.zero); held = null; }
            toy = null; taskPhase = TaskPhase.None; body.ReachState.weight = 0f; body.JawTarget = 0f; body.JawStop = 0f;
            mover.enabled = false; mover.transform.position = pos + Vector3.up * 0.02f; mover.enabled = true;
            yaw = yawRad; vel = Vector3.zero; vy = 0f; lastPos = mover.transform.position;
            body.Teleport(new Vector3(pos.x, WorldQueries.GroundNear(pos.x, pos.z, pos.y + 0.02f), pos.z), yawRad);
            Pick(AiState.Idle);
        }

        /// <summary>The event for stepping onto a surface that has one (bed, water, tiles).</summary>
        static DuckEventKind SurfaceEvent(SurfaceType s) => s == SurfaceType.Bed ? DuckEventKind.Bed : s == SurfaceType.Water ? DuckEventKind.Water : DuckEventKind.Tile;

        void Mode(AiState s) { if (State != s) { State = s; Context = ""; } }
        void DropHeld() { if (held != null) { held.Release(Vector3.zero); OnEvent?.Invoke(this, DuckEventKind.Drop, held.label); held = null; } toy = null; taskPhase = TaskPhase.None; body.ReachState.weight = 0f; body.JawTarget = 0f; body.JawStop = 0f; }

        // ------------------------------------------------------------------ games (they take over from the normal life)
        float lastKick = -9f, lastPounce = -9f, pounceT = -1f; bool cheered;

        (float v, float w)? Games(float dt)
        {
            if (MicroduckVR.Games.RaceGame.Active)
            {
                var rg = MicroduckVR.Games.RaceGame.Instance;
                Mode(AiState.Race); DropHeld();
                if (MicroduckVR.Games.RaceGame.Frozen) { cheered = false; return (0f, 0f); }
                if (rg.Done(this)) { if (!cheered) { cheered = true; body.PlayEmote(EmoteKind.Dance); } return (0f, 0f); }
                // a slightly different pace per duck so it is a real race
                return SteerAvoid(rg.TargetFor(this), Gait.MaxSpeed * 1.9f * (0.8f + 0.16f * ((index * 37) % 7) / 7f), 0.05f);   // race pace, as on the web
            }
            if (MicroduckVR.Games.FootballGame.Active && MicroduckVR.Games.FootballGame.Instance.Team.TryGetValue(this, out int team))
            {
                var fg = MicroduckVR.Games.FootballGame.Instance;
                Mode(AiState.Football); DropHeld();
                if (!fg.IsChaser(this)) return SteerAvoid(fg.HomeSpot(this), Gait.MaxSpeed * 0.6f, 0.04f);
                Vector3 b = fg.BallPos, goal = fg.GoalCentre(1 - team);
                Vector3 toGoal = Flat(goal - b).normalized, behind = Flat(b) - toGoal * 0.09f;
                float d = Flat(behind - Pos).magnitude;
                body.LookAt.target = b; body.LookAt.weight = 0.7f;
                // close to the ball, facing the goal: kick it
                if (Flat(b - Pos).magnitude < 0.085f && Time.time - lastKick > 0.45f && fg.Ball != null && Vector3.Dot(Flat(b - Pos).normalized, toGoal) > -0.2f)
                {
                    lastKick = Time.time;
                    var spread = Quaternion.Euler(0f, R(-12f, 12f), 0f) * toGoal;
                    fg.Ball.AddForce(spread * (0.18f + Speed * 0.8f) + Vector3.up * 0.04f, ForceMode.VelocityChange);
                }
                if (d < 0.04f) return SteerAvoid(Flat(b) + toGoal * 0.1f, Gait.MaxSpeed, 0.01f);
                return SteerAvoid(behind, Gait.MaxSpeed, 0.02f);
            }
            if (PlayerTargets.NearestLaser(Pos, out Vector3 P))         // your dot, or the other player's
            {
                float d = Flat(P - Pos).magnitude;
                if (d < 0.8f && Mathf.Abs(P.y - Pos.y) < 0.12f)
                {
                    Mode(AiState.Laser); DropHeld();
                    body.LookAt.target = P; body.LookAt.weight = 0.8f;
                    if (pounceT >= 0f) return Pounce(dt);
                    if (d < 0.13f && Time.time - lastPounce > 2.2f) { pounceT = 0f; lastPounce = Time.time; return (0f, 0f); }
                    // gather in a ring round the dot instead of piling onto it
                    float ang = index * 2.39996f;
                    Vector3 ring = new Vector3(P.x + Mathf.Cos(ang) * 0.11f, P.y, P.z + Mathf.Sin(ang) * 0.11f);
                    if (d < 0.16f) { float err = Wrap(Mathf.Atan2(P.x - Pos.x, P.z - Pos.z) - yaw); return (0f, Mathf.Clamp(err * 4f, -2f, 2f)); }
                    return SteerAvoid(ring, Gait.MaxSpeed, 0.03f);
                }
            }
            if (pounceT >= 0f) return Pounce(dt);
            if (State == AiState.Race || State == AiState.Football || State == AiState.Laser) { body.LookAt.weight = 0f; Pick(AiState.Idle); }
            return null;
        }

        (float v, float w) Pounce(float dt)
        {
            pounceT += dt; float u = pounceT / 0.6f;
            body.CrouchTarget = u < 0.35f;
            body.Hop = u > 0.35f && u < 1f ? Mathf.Sin(Mathf.PI * (u - 0.35f) / 0.65f) * 0.03f : 0f;
            if (u >= 1f) { pounceT = -1f; body.Hop = 0f; body.CrouchTarget = false; body.HappyWiggle = 1f; OnEvent?.Invoke(this, DuckEventKind.Got, "dot"); }
            return (u > 0.35f && u < 0.9f ? 0.26f : 0f, 0f);
        }

        (float v, float w) Think(float dt)
        {
            if (body.Fallen) return (0f, 0f);
            var gm = Games(dt); if (gm.HasValue) return gm.Value;
            t += dt;
            if (held != null || toy != null) return ToyTask(dt);
            if (t > dur) Pick(null);
            switch (State)
            {
                case AiState.Follow:
                {
                    var L = PlayerTargets.ForDuck(index);                       // you (or, playing together, one of the two players)
                    if (L == null) { Pick(AiState.Wander); break; }
                    int slot = index % 5;
                    Vector3 lf = GltfSpace.Forward(L.Yaw);
                    Vector3 back = -lf * (0.22f + 0.13f * (slot / 2));
                    Vector3 side = new Vector3(lf.z, 0f, -lf.x) * ((slot % 2 == 1 ? 1f : -1f) * (0.1f + 0.02f * slot));
                    Vector3 goal = L.Pos + back + side;
                    float d = Flat(goal - Pos).magnitude;
                    if (d < 0.04f && Flat(L.Vel).magnitude < 0.02f) return (0f, Mathf.Clamp(Wrap(L.Yaw - yaw) * 3f, -1.2f, 1.2f));
                    return SteerAvoid(goal, Gait.MaxSpeed * Mathf.Clamp(d / 0.25f, 0.4f, 1f), 0.03f);
                }
                case AiState.Roam:
                {
                    if (route == null)
                    {
                        var rooms = new List<string>(HouseLayout.Spawns.Keys);
                        string here = HouseLayout.RoomAt(Pos);
                        rooms.Remove(here);
                        string to = rooms[rng.Next(rooms.Count)];
                        var sp = HouseLayout.Spawns[to]; var dest = sp[rng.Next(sp.Length)];
                        route = HouseLayout.Route(Pos, dest); ri = 0; best = 99f; routeStuck = 0f;
                        Context = HouseLayout.RoomName[to];
                    }
                    if (ri >= route.Count) { route = null; Pick(AiState.Idle); break; }
                    var tgt = route[ri];
                    float dd = Flat(tgt - Pos).magnitude;
                    if (dd < 0.12f) { ri++; best = 99f; if (ri >= route.Count) { route = null; Pick(AiState.Idle); } break; }
                    if (dd < best - 0.02f) { best = dd; routeStuck = 0f; } else if ((routeStuck += dt) > 4f) { route = null; Pick(AiState.Wander); break; }
                    return SteerAvoid(tgt, Gait.MaxSpeed * 0.85f, 0.03f);
                }
                case AiState.Wander:
                {
                    if (!target.HasValue || Flat(target.Value - Pos).magnitude < 0.04f)
                    {
                        if (target.HasValue)
                        {
                            State = AiState.Idle; t = 0f; dur = R(1f, 3f);
                            if (R() < 0.3f) body.PlayEmote(PauseEmotes[rng.Next(3)]);
                            break;
                        }
                        target = HouseLayout.RandomSpot(Pos, 0.6f, rng);
                    }
                    return SteerAvoid(target.Value, Gait.MaxSpeed * 0.7f, 0.03f);
                }
                case AiState.Toy:
                {
                    Grabbable bestToy = null; float bd = 0.7f;
                    foreach (var g in Grabbable.All)
                    {
                        if (g.IsHeld) continue;
                        Vector3 gp = g.transform.position;
                        float d = Flat(gp - Pos).magnitude;
                        if (d < bd && Mathf.Abs(gp.y - Pos.y) < 0.05f) { bd = d; bestToy = g; }
                    }
                    if (bestToy == null) { Pick(AiState.Wander); break; }
                    toy = bestToy; taskPhase = TaskPhase.Approach; taskT = 0f; Context = toy.label;
                    return (0f, 0f);
                }
                case AiState.Sit:
                    if (!body.SitTarget && t < 0.2f) body.SitTarget = true;
                    if (t > dur - 0.5f) body.SitTarget = false;
                    return (0f, 0f);
            }
            return (0f, 0f);
        }

        (float v, float w) ToyTask(float dt)
        {
            taskT += dt;
            if (held != null)
            {
                // carry it around for a bit, then put it down
                if (taskPhase == TaskPhase.Carry)
                {
                    if (!target.HasValue || Flat(target.Value - Pos).magnitude < 0.05f) target = HouseLayout.RandomSpot(Pos, 0.5f, rng);
                    if (taskT > 4.5f) { taskPhase = TaskPhase.Drop; taskT = 0f; }
                    return SteerAvoid(target.Value, Gait.MaxSpeed * 0.6f, 0.03f);
                }
                if (taskPhase == TaskPhase.Drop)
                {
                    body.JawStop = 0f; body.JawTarget = 0.5f;
                    if (taskT > 0.25f)
                    {
                        held.Release(Vector3.zero); OnEvent?.Invoke(this, DuckEventKind.Drop, held.label); held = null; body.JawTarget = 0f;
                        Pick(R() < 0.5f ? AiState.Idle : AiState.Wander);
                    }
                    return (0f, 0f);
                }
            }
            if (toy == null || toy.IsHeld || taskT > 10f) { toy = null; body.ReachState.weight = 0f; body.JawTarget = 0f; Pick(AiState.Wander); return (0f, 0f); }
            Vector3 tp = toy.transform.position;
            Vector3 to = Flat(tp - Pos); float dist = to.magnitude;
            if (taskPhase == TaskPhase.Approach)
            {
                // stand ~8 cm short of the toy, facing it
                if (dist > 0.1f) return SteerAvoid(tp - to.normalized * 0.08f, Gait.MaxSpeed * 0.6f, 0.02f);
                float err = Wrap(Mathf.Atan2(to.x, to.z) - yaw);
                if (Mathf.Abs(err) > 0.12f) return (0f, Mathf.Clamp(err * 3f, -1.5f, 1.5f));
                taskPhase = TaskPhase.Reach; taskT = 0f;
            }
            if (taskPhase == TaskPhase.Reach)
            {
                body.ReachState.target = tp; body.ReachState.weight = 1f; body.JawTarget = 0.5f;
                if (Vector3.Distance(body.BeakGap(), tp) < 0.03f && taskT > 0.5f)
                {
                    if (toy.TryHold(this)) { held = toy; toy = null; body.JawTarget = 0f; body.JawStop = 0.12f; body.ReachState.weight = 0f; taskPhase = TaskPhase.Carry; taskT = 0f; target = null; OnEvent?.Invoke(this, DuckEventKind.Got, held.label); }
                }
                else if (taskT > 3f) { taskPhase = TaskPhase.Approach; taskT = 0f; body.ReachState.weight = 0f; }
                return (0f, 0f);
            }
            return (0f, 0f);
        }

        /// <summary>Steer to a goal with separation from other ducks (and the player) and a whisker probe for ledges and furniture.</summary>
        (float v, float w) SteerAvoid(Vector3 goal, float speed, float tol)
        {
            Vector3 to = Flat(goal - Pos); float dist = to.magnitude;
            if (dist < tol) return (0f, 0f);
            to /= dist;
            Vector3 sep = Vector3.zero;
            void Push(Vector3 other) { var d = Flat(Pos - other); float L = d.magnitude; if (L < 0.19f && L > 1e-4f) sep += d / L * ((0.19f - L) / 0.19f); }
            foreach (var o in All) if (o != this) Push(o.Pos);
            for (int i = 0, n = PlayerTargets.Count; i < n; i++) { var pb = PlayerTargets.Get(i); if (pb != null) Push(pb.Pos); }   // the players
            Vector3 dir = to + sep * 1.6f;
            // whisker: a step up or a drop ahead, or furniture in the way -> bend around it
            Vector3 pr = dir.normalized;
            Vector3 probe = Pos + pr * 0.12f;
            float gy = WorldQueries.GroundNear(probe.x, probe.z, Pos.y + 0.06f);
            bool blocked = Physics.Raycast(Pos + Vector3.up * 0.08f, pr, 0.14f, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore);
            if (gy > Pos.y + 0.03f || gy < Pos.y - 0.03f || blocked) dir = Quaternion.AngleAxis((index % 2 == 1 ? 0.9f : -0.9f) * Mathf.Rad2Deg, Vector3.up) * dir;
            float desired = Mathf.Atan2(dir.x, dir.z), err = Wrap(desired - yaw);
            float w = Mathf.Clamp(err * 4f, -Gait.MaxYawRate, Gait.MaxYawRate);
            float v = speed * Mathf.Clamp01(1f - Mathf.Abs(err) / 1.1f) * Mathf.Clamp(dist / 0.1f, 0.3f, 1f);
            return (v, w);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (Puppet) return;                                          // posed by the network session instead
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            var (v, w) = Think(dt);
            if (Time.time < readUntil && (readAlways || (!MicroduckVR.Games.RaceGame.Active && !MicroduckVR.Games.FootballGame.Active)))
            {
                var away = Flat(Pos - readZone); float L = away.magnitude;
                if (L < readRadius)
                {
                    if (L < 1e-3f) away = Flat(Pos - (PlayerDuck.Instance != null ? PlayerDuck.Instance.Body.Pos : Pos + Vector3.forward));
                    float err = Wrap(Mathf.Atan2(away.x, away.z) - yaw);
                    w = Mathf.Clamp(err * 4f, -Gait.MaxYawRate, Gait.MaxYawRate);
                    v = Gait.MaxSpeed * 0.9f * Mathf.Clamp01(1f - Mathf.Abs(err) / 1.4f);
                }
            }
            // surface: slippery tiles, slow water, bouncy bed
            var surf = SurfaceTag.At(Pos);
            SurfaceType st = surf != null ? surf.Kind : SurfaceType.Wood;
            if (st != Surface) { var prevS = Surface; Surface = st; if (st == SurfaceType.Bed || st == SurfaceType.Water || st == SurfaceType.Tile) OnEvent?.Invoke(this, SurfaceEvent(st), ""); if (st == SurfaceType.Bed) boing = 1f; }
            if (st == SurfaceType.Water) v *= 0.55f;
            float grip = surf != null ? Mathf.Lerp(0.18f, 1f, Mathf.InverseLerp(0.15f, 0.7f, surf.Friction)) : 1f;
            yaw += w * dt;
            Vector3 want = GltfSpace.Forward(yaw) * v;
            vel = Vector3.MoveTowards(vel, want, 0.6f * grip * dt + (grip > 0.9f ? 1f : 0f) * dt);
            vy = mover.isGrounded ? -0.05f : vy - 9.81f * dt;
            mover.Move(new Vector3(vel.x, vy, vel.z) * dt);
            Vector3 p = Pos;
            Speed = Flat(p - lastPos).magnitude / Mathf.Max(dt, 1e-4f);
            // unstick: wanting to move but not moving
            if (v > 0.05f && Speed < 0.01f) { stuckT += dt; if (stuckT > 1.2f) { stuckT = 0f; target = HouseLayout.RandomSpot(p, 0.3f, rng); } } else stuckT = 0f;
            lastPos = p;
            yawRate = w;
            bedT = st == SurfaceType.Bed ? bedT + dt : 0f;
            boing *= Mathf.Exp(-2.2f * dt);
            if (pounceT < 0f) body.Hop = st == SurfaceType.Bed ? Mathf.Abs(Mathf.Sin(bedT * 13f + seed)) * (0.011f * Mathf.Clamp01(Speed / 0.02f) + 0.026f * boing) : Mathf.MoveTowards(body.Hop, 0f, 0.2f * dt);
            body.Step(dt, p, yaw, Flat(vel), yawRate);
            if (held != null) { held.transform.position = body.BeakGap(); held.transform.rotation = Quaternion.LookRotation(body.HeadDir(), Vector3.up); }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
    }
}
