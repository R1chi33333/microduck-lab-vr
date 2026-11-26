using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Flock
{
    /// <summary>
    /// Placeholder AI for M1: wander to random spots inside a box, pause, sometimes emote, keep apart
    /// from other ducks. Replaced by the full brain (port of agent.js) in M4.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(DuckSetup))]
    public class WanderDuck : MonoBehaviour
    {
        public static readonly List<WanderDuck> All = new List<WanderDuck>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }
        static readonly EmoteKind[] Emotes = { EmoteKind.Look, EmoteKind.Nod, EmoteKind.Dance, EmoteKind.Shake };
        public Vector2 boundsMin = new Vector2(-2.6f, -2.1f), boundsMax = new Vector2(2.6f, 2.1f);
        public float speed = 0.1f;
        public int seed = 1;

        DuckBody body; CharacterController mover;
        System.Random rng;
        Vector3? target; float wait, yaw, vy;
        Vector3 vel, lastPos;
        float stuckT, bedT;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            var s = GetComponent<DuckSetup>(); body = s.Body; mover = s.Mover;
            rng = new System.Random(seed); yaw = body.Yaw; wait = (float)rng.NextDouble() * 2f;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            Vector3 p = mover.transform.position;
            Vector3 want = Vector3.zero;
            if (wait > 0f) { wait -= dt; }
            else if (target == null) target = new Vector3(R(boundsMin.x, boundsMax.x), 0f, R(boundsMin.y, boundsMax.y));
            if (target.HasValue && wait <= 0f)
            {
                Vector3 to = target.Value - p; to.y = 0f;
                if (to.magnitude < 0.05f)
                {
                    target = null; wait = R(1f, 3.5f);
                    if (rng.NextDouble() < 0.35) body.PlayEmote(Emotes[rng.Next(4)]);
                }
                else want = to.normalized * speed;
            }
            // keep apart from other ducks (boids-style separation)
            foreach (var o in All)
            {
                if (o == this || o.mover == null) continue;
                Vector3 d = p - o.mover.transform.position; d.y = 0f;
                float m = d.magnitude;
                if (m > 0.001f && m < 0.25f) want += d / m * (0.25f - m) * 0.8f;
            }
            var surf = SurfaceTag.At(p);
            SurfaceType st = surf != null ? surf.Kind : SurfaceType.Wood;
            if (st == SurfaceType.Water) want *= 0.55f;
            float grip = surf != null ? Mathf.Lerp(0.18f, 1f, Mathf.InverseLerp(0.15f, 0.7f, surf.Friction)) : 1f;
            vel = Vector3.MoveTowards(vel, want, 0.6f * grip * dt);
            // stuck against furniture: give up on this target
            if (target.HasValue && wait <= 0f)
            {
                if ((p - lastPos).magnitude < speed * 0.25f * dt) stuckT += dt; else stuckT = 0f;
                if (stuckT > 1.5f) { target = null; stuckT = 0f; wait = R(0.3f, 1f); }
            }
            lastPos = p;
            bedT = st == SurfaceType.Bed ? bedT + dt : 0f;
            body.Hop = st == SurfaceType.Bed ? Mathf.Abs(Mathf.Sin(bedT * 6.5f + seed)) * 0.028f : Mathf.MoveTowards(body.Hop, 0f, 0.2f * dt);
            float prev = yaw;
            if (vel.magnitude > 0.015f)
            {
                float ty = Mathf.Atan2(vel.x, vel.z);
                float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, ty * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                yaw += Mathf.Clamp(dy, -Gait.MaxYawRate * dt, Gait.MaxYawRate * dt);
            }
            float yawRate = Mathf.DeltaAngle(prev * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / Mathf.Max(dt, 1e-4f);
            vy = mover.isGrounded ? -0.05f : vy - 9.81f * dt;
            // walk along the facing direction so feet and body agree
            Vector3 v = GltfSpace.Forward(yaw) * Mathf.Min(vel.magnitude, speed) * Mathf.Clamp01(Vector3.Dot(GltfSpace.Forward(yaw), vel.normalized) + 0.2f);
            mover.Move(new Vector3(v.x, vy, v.z) * dt);
            p = mover.transform.position;
            body.Step(dt, p, yaw, v, yawRate);
        }
    }
}
