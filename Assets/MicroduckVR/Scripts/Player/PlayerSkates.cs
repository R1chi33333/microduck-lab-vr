using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Player
{
    /// <summary>
    /// Roller skates for the player's duck (port of the skate model in agent.js): momentum, push strokes, braking,
    /// grip that depends on the floor, a drift (hold left grip while turning: lower grip, faster turn, the tail slides
    /// out; let go after a moment for a slingshot) and a boost (left trigger: a short burst, then a cooldown).
    /// Comic effects: smoke puffs when kicking off, braking or boosting, sparks while drifting, skid marks on the floor.
    ///
    /// VR comfort: you steer by pointing the stick where you want to go relative to where you look (the skates turn
    /// towards it); the view itself never turns, rolls or leans with the duck. Speeds are capped (comfortScale) and the
    /// vignette tightens with speed.
    /// </summary>
    public class PlayerSkates : MonoBehaviour
    {
        // web SKATE constants (duck-scale metres and seconds)
        const float Accel = 0.42f, MaxSpeed = 0.42f, Friction = 0.32f, Brake = 0.9f, Grip = 2.4f, Turn = 2.0f, StrokeHz = 1.25f;
        const float BoostMult = 1.5f, BoostTime = 1.2f, BoostCooldown = 2.0f, BoostKick = 0.22f, BoostAccel = 1.1f;
        const float DriftGrip = 0.45f, DriftTurnMult = 1.7f, DriftFlick = 0.22f, DriftMinSpeed = 0.1f, SlingAfter = 0.6f, SlingKick = 0.16f, SlingTime = 0.5f;
        const float CrashSpeed = 0.5f;

        [Range(0.5f, 1f)] public float comfortScale = 0.85f;      // caps top speed and boost in VR

        public bool On { get; private set; }
        public static float? DebugDrift, DebugBoost;          // editor tests
        float DriftIn => DebugDrift ?? driftA.ReadValue<float>();
        float BoostIn => DebugBoost ?? boostA.ReadValue<float>();
        public float Speed => sv.magnitude;
        public float TopSpeed => MaxSpeed * comfortScale;
        public bool Drifting => drifting;
        public bool Boosting => boostT > 0f;
        public Vector3 Velocity => sv;

        DuckBody body;
        SkatePair pair;
        InputAction driftA, boostA;
        Vector3 sv; float w, boostT, boostCd, driftT, slip, prevSpeed;
        bool drifting;

        // effects
        Material smokeMat, sparkMat, skidMat;
        readonly List<(Transform t, Vector3 v, float age, float life, int kind)> fx = new List<(Transform, Vector3, float, float, int)>();
        readonly Queue<(Transform t, float age)> skids = new Queue<(Transform, float)>();
        Vector3?[] lastSkid = new Vector3?[2];
        float smokeT, sparkT;
        AudioSource rollSrc, sfxSrc; AudioClip boostClip, pushClip, clackClip;

        public void Init(DuckBody b)
        {
            body = b;
            pair = new SkatePair();
            driftA = new InputAction("Drift", InputActionType.Value, expectedControlType: "Axis");
            driftA.AddBinding("<XRController>{LeftHand}/grip"); driftA.AddBinding("<Keyboard>/leftShift");
            boostA = new InputAction("Boost", InputActionType.Value, expectedControlType: "Axis");
            boostA.AddBinding("<XRController>{LeftHand}/trigger"); boostA.AddBinding("<Keyboard>/leftCtrl");
            driftA.Enable(); boostA.Enable();
            smokeMat = Unlit("#FFF6E4", "skate_smoke"); sparkMat = Unlit("#FFE45C", "skate_spark"); skidMat = Unlit("#1B1430", "skate_skid");
            rollSrc = gameObject.AddComponent<AudioSource>(); rollSrc.clip = Noise(1f, 0.25f, true); rollSrc.loop = true; rollSrc.volume = 0f; rollSrc.spatialBlend = 0f; rollSrc.Play();
            sfxSrc = gameObject.AddComponent<AudioSource>(); sfxSrc.spatialBlend = 0f;
            boostClip = Sweep(300f, 1400f, 0.35f); pushClip = Noise(0.12f, 0.6f, false); clackClip = Sweep(900f, 500f, 0.04f);
        }

        void OnDestroy() { driftA?.Disable(); boostA?.Disable(); }

        static Material Unlit(string hex, string key) { var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), key); m.SetFloat("_Unlit", 1f); return m; }

        // ------------------------------------------------------------------ equip
        public void Toggle(float yaw, Vector3 walkVel)
        {
            if (body.Fallen) return;
            On = !On;
            if (On) { pair.Attach(body); body.Skating = true; body.SkateLift = SkatePair.Height; sv = walkVel; w = 0f; }
            else { pair.Detach(); body.Skating = false; body.SkateLean = 0f; body.Wobble = 0f; drifting = false; boostT = 0f; sv = Vector3.zero; }
            sfxSrc.PlayOneShot(clackClip, 0.8f);
        }

        // ------------------------------------------------------------------ physics
        static Vector3 Fwd(float yaw) => new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

        /// <summary>One step. stick: wanted direction in the world (length 0..1, already relative to the head).
        /// friction: the floor's (wood 0.7, rug 1, tiles 0.15). Updates yaw; returns the velocity to move by.</summary>
        public Vector3 Step(float dt, Vector3 stick, float friction, Vector3 pos, ref float yaw)
        {
            float fric = Mathf.Clamp(friction / 0.7f, 0.18f, 1.45f);
            boostCd = Mathf.Max(0f, boostCd - dt); boostT = Mathf.Max(0f, boostT - dt);
            bool boosting = boostT > 0f;
            if (body.Fallen) stick = Vector3.zero;

            // the command, from where the stick points relative to the skates' heading
            float m = Mathf.Clamp01(stick.magnitude), push = 0f, wCmd = 0f; bool braking = false;
            if (m > 0.15f)
            {
                float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(stick.x, stick.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float c = Mathf.Cos(dy);
                if (c < -0.6f && sv.magnitude > 0.03f) braking = true;          // stick pulled against the heading: brake
                else { push = m * Mathf.Max(0f, c); wCmd = Mathf.Clamp(dy * 2.2f, -1f, 1f) * m; }
            }
            if (!body.Fallen && BoostIn > 0.6f && boostCd <= 0f) Boost(false, yaw);

            float sp0 = sv.magnitude;
            bool wantDrift = DriftIn > 0.5f && Mathf.Abs(wCmd) > 0.1f && sp0 > DriftMinSpeed * Mathf.Min(1f, fric);
            if (wantDrift && !drifting) { drifting = true; driftT = 0f; yaw += Mathf.Sign(wCmd) * DriftFlick; }
            if (drifting && !wantDrift) { drifting = false; if (driftT > SlingAfter && !body.Fallen) Boost(true, yaw); }
            if (drifting) driftT += dt;

            var f = Fwd(yaw);
            if (push > 0.02f)
            {
                body.Stroke = (body.Stroke + StrokeHz * dt) % 1f;
                if (body.Stroke < StrokeHz * dt) { body.StrokeSide *= -1f; sfxSrc.PlayOneShot(pushClip, 0.25f + 0.35f * push); EmitSmoke(2); }
                float phase = Mathf.Max(0f, Mathf.Sin(body.Stroke * Mathf.PI * 2f));
                sv += f * Accel * push * (0.4f + phase) * dt;
            }
            else body.Stroke = Mathf.Max(0f, body.Stroke - dt);
            if (boosting) sv += f * BoostAccel * dt;
            if (braking) sv *= Mathf.Exp(-Brake * 2.2f * dt);
            sv *= Mathf.Exp(-Friction * fric * dt);
            float top = TopSpeed * (boosting ? Mathf.Lerp(1f, BoostMult, comfortScale) : 1f);
            float sp = sv.magnitude;
            if (sp > top) { float k = boosting ? top / sp : Mathf.Max(top / sp, Mathf.Exp(-2.5f * dt)); sv *= k; sp = sv.magnitude; }

            // steer; the velocity only slowly follows the heading -> drift
            float turn = Turn * (drifting ? DriftTurnMult : 1f);
            w = Mathf.MoveTowards(w, wCmd * turn, (drifting ? 12f : 8f) * dt);
            yaw += w * dt;
            var f2 = Fwd(yaw);
            float along = Vector3.Dot(sv, f2);
            var target = f2 * (along >= 0f ? sp : -sp);
            float grip = (drifting ? DriftGrip : Grip) * fric;
            sv = Vector3.Lerp(sv, target, 1f - Mathf.Exp(-grip * dt));

            // ramps pull it downhill
            const float e = 0.03f;
            float gx = (WorldQueries.GroundNear(pos.x + e, pos.z, pos.y) - WorldQueries.GroundNear(pos.x - e, pos.z, pos.y)) / (2f * e);
            float gz = (WorldQueries.GroundNear(pos.x, pos.z + e, pos.y) - WorldQueries.GroundNear(pos.x, pos.z - e, pos.y)) / (2f * e);
            float slope = Mathf.Abs(gx) < 0.6f && Mathf.Abs(gz) < 0.6f ? Mathf.Sqrt(gx * gx + gz * gz) : 0f;
            if (slope > 0f) { sv.x -= gx * 2.2f * dt; sv.z -= gz * 2.2f * dt; }

            // lean into turns, counter-lean while drifting (the body only; never the view)
            slip = Mathf.Abs(sv.x * f2.z - sv.z * f2.x);
            float spk = Mathf.Clamp(sp / 0.35f, 0f, 1.4f);
            body.SkateLean = drifting ? Mathf.Clamp(w * 0.16f * spk, -0.42f, 0.42f) : Mathf.Clamp(-w * 0.11f * spk, -0.3f, 0.3f);
            float wob = Mathf.Clamp01((sp - MaxSpeed * 1.05f) / 0.2f) * 0.35f + slope * 1.2f;
            body.Wobble = Mathf.Lerp(body.Wobble, Mathf.Clamp(wob, 0f, 0.6f), 1f - Mathf.Exp(-3f * dt));

            pair.Spin(Vector3.Dot(sv, f2) * dt);
            Effects(dt, sp, push, braking, boosting);
            prevSpeed = sp;
            return sv;
        }

        void Boost(bool sling, float yaw)
        {
            if (!sling) { boostCd = BoostTime + BoostCooldown; boostT = BoostTime; }
            else boostT = Mathf.Max(boostT, SlingTime);
            sv += Fwd(yaw) * (sling ? SlingKick : BoostKick) * comfortScale;
            sfxSrc.PlayOneShot(boostClip, sling ? 0.5f : 0.8f);
            EmitSmoke(6);
        }

        /// <summary>After the capsule moved: a wall stops the skates; a hard hit (only when boosting) knocks the duck over.</summary>
        public void AfterMove(Vector3 intended, Vector3 got)
        {
            float want = new Vector2(intended.x, intended.z).magnitude; if (want < 1e-5f) return;
            float blocked = 1f - new Vector2(got.x, got.z).magnitude / want;
            if (blocked < 0.5f) return;
            if (sv.magnitude > CrashSpeed * comfortScale && !body.Fallen)
            {
                // bounce off and sit down backwards: tipping forwards put the duck's head through the wall it had just hit
                body.StartTip(-new Vector3(intended.x, 0f, intended.z).normalized);
                sfxSrc.PlayOneShot(clackClip, 1f); EmitSparks(10, 1f);
                sv = Vector3.zero;
            }
            else sv *= 0.25f;
            drifting = false; boostT = 0f;
        }

        // ------------------------------------------------------------------ comic effects
        void Effects(float dt, float sp, float push, bool braking, bool boosting)
        {
            float accel = (sp - prevSpeed) / Mathf.Max(dt, 1e-4f);
            smokeT -= dt; sparkT -= dt;
            if ((boosting || braking && sp > 0.08f || (accel > 0.25f && sp < 0.2f) || drifting && sp > 0.15f) && smokeT <= 0f) { smokeT = boosting ? 0.05f : 0.1f; EmitSmoke(1); }
            if (drifting && sp > 0.12f && sparkT <= 0f) { sparkT = 0.04f; EmitSparks(2, Mathf.Clamp01(sp / 0.4f)); }
            bool marking = (drifting || slip > 0.06f || braking) && sp > 0.06f;
            Skid(0, pair.Left, marking); Skid(1, pair.Right, marking);
            rollSrc.volume = Mathf.Lerp(rollSrc.volume, On ? Mathf.Clamp01(sp / 0.42f) * 0.32f : 0f, 1f - Mathf.Exp(-8f * dt));
            rollSrc.pitch = 0.6f + sp * 1.4f + (drifting ? 0.25f : 0f);

            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var (t, v, age, life, kind) = fx[i];
                age += dt;
                if (age >= life) { Destroy(t.gameObject); fx.RemoveAt(i); continue; }
                float u = age / life;
                if (kind == 0) { t.position += v * dt; float s = u < 0.18f ? Mathf.Lerp(0.01f, 0.032f, u / 0.18f) : Mathf.Lerp(0.032f, 0f, (u - 0.18f) / 0.82f); t.localScale = new Vector3(s, s * 0.85f, s); }
                else
                {
                    // a little comic star that spins, faces you and shrinks as it falls
                    v += Vector3.down * 1.2f * dt; t.position += v * dt;
                    var c = Camera.main; var look = c != null ? t.position - c.transform.position : Vector3.forward;
                    t.rotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, 0f, age * 720f);
                    float s = 0.011f * (u < 0.15f ? u / 0.15f : 1f - (u - 0.15f) / 0.85f); t.localScale = new Vector3(s, s, s);
                }
                fx[i] = (t, v, age, life, kind);
            }
            int n = skids.Count;
            for (int i = 0; i < n; i++)
            {
                var (t, age) = skids.Dequeue(); age += dt;
                if (age > 7f) { Destroy(t.gameObject); continue; }
                if (age > 5f) { var s = t.localScale; s.x = 0.008f * (1f - (age - 5f) / 2f); t.localScale = s; }
                skids.Enqueue((t, age));
            }
        }

        Transform Bit(Mesh mesh, Material mat, Vector3 at)
        {
            var go = new GameObject("skate fx");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.transform.position = at;
            return go.transform;
        }

        static Mesh star, stroke;
        /// <summary>A flat four-point comic star (1 unit across, a little thickness so the ink pass outlines it).</summary>
        static Mesh Star()
        {
            if (star != null) return star;
            var v = new List<Vector3>(); var t = new List<int>();
            const int P = 8; float th = 0.12f;
            foreach (var z in new[] { -th, th })
            {
                v.Add(new Vector3(0f, 0f, z));
                for (int i = 0; i < P; i++) { float a = i * Mathf.PI * 2f / P, r = i % 2 == 0 ? 0.5f : 0.17f; v.Add(new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, z)); }
            }
            int o = P + 1;
            for (int i = 0; i < P; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % P;
                t.Add(0); t.Add(a); t.Add(b);                         // front
                t.Add(o); t.Add(o + b); t.Add(o + a);                 // back
                t.Add(a); t.Add(o + a); t.Add(o + b); t.Add(a); t.Add(o + b); t.Add(b);   // rim
            }
            star = new Mesh { name = "comic star" }; star.SetVertices(v); star.SetTriangles(t, 0); star.RecalculateNormals(); star.RecalculateBounds();
            return star;
        }
        /// <summary>A tapered brush stroke along +z (length 1, width 1 in the middle), for skid marks.</summary>
        static Mesh Stroke()
        {
            if (stroke != null) return stroke;
            var v = new List<Vector3>(); var t = new List<int>(); const int N = 8;
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, w = 0.5f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.08f, 0.92f, u));
                v.Add(new Vector3(-w, 0f, u - 0.5f)); v.Add(new Vector3(w, 0f, u - 0.5f));
                if (i > 0) { int a = (i - 1) * 2; t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3); }
            }
            stroke = new Mesh { name = "skid stroke" }; stroke.SetVertices(v); stroke.SetTriangles(t, 0); stroke.RecalculateNormals(); stroke.RecalculateBounds();
            return stroke;
        }

        void EmitSmoke(int n)
        {
            if (!On || fx.Count > 80) return;
            var back = -Fwd(body.Yaw);
            foreach (var sk in new[] { pair.Left, pair.Right })
                for (int i = 0; i < n; i++)
                {
                    var p = sk.position + back * 0.03f + Vector3.down * 0.012f + Random.insideUnitSphere * 0.006f;
                    var v = back * Random.Range(0.02f, 0.06f) + Vector3.up * Random.Range(0.02f, 0.05f) + Random.insideUnitSphere * 0.015f;
                    fx.Add((Bit(ToyKit.Sphere(), smokeMat, p), v, 0f, Random.Range(0.45f, 0.7f), 0));
                }
        }

        void EmitSparks(int n, float k)
        {
            if (!On || fx.Count > 80) return;
            var back = -Fwd(body.Yaw);
            foreach (var sk in new[] { pair.Left, pair.Right })
                for (int i = 0; i < n; i++)
                {
                    var p = sk.position + Vector3.down * 0.018f + back * 0.02f;
                    var side = Vector3.Cross(Vector3.up, back) * Random.Range(-1f, 1f);
                    var v = (back + side * 0.8f).normalized * Random.Range(0.15f, 0.35f) * (0.5f + k) + Vector3.up * Random.Range(0.12f, 0.3f);
                    fx.Add((Bit(Star(), sparkMat, p), v, 0f, Random.Range(0.25f, 0.4f), 1));
                }
        }

        void Skid(int i, Transform sk, bool on)
        {
            if (!on || !On) { lastSkid[i] = null; return; }
            var p = sk.position; p.y = WorldQueries.GroundNear(p.x, p.z, p.y - 0.03f) + 0.0012f;
            if (lastSkid[i] is Vector3 a)
            {
                var d = p - a; float len = d.magnitude;
                if (len < 0.012f) return;
                if (len < 0.08f && Mathf.Abs(d.y) < 0.01f)
                {
                    var t = Bit(Stroke(), skidMat, (a + p) / 2f);
                    t.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                    t.localScale = new Vector3(0.008f, 1f, len * 1.35f + 0.004f);
                    skids.Enqueue((t, 0f));
                    while (skids.Count > 240) Destroy(skids.Dequeue().t.gameObject);
                }
            }
            lastSkid[i] = p;
        }

        // ------------------------------------------------------------------ procedural sounds
        static AudioClip Noise(float len, float vol, bool loop)
        {
            int n = (int)(44100 * len); var d = new float[n]; var r = new System.Random(9); float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += ((float)r.NextDouble() * 2f - 1f - lp) * 0.08f;          // rumbly low-passed noise
                float env = loop ? 1f : Mathf.Sin(Mathf.PI * i / n);
                d[i] = lp * 3f * vol * env;
            }
            if (loop) for (int i = 0; i < 400; i++) { float k = i / 400f; d[n - 400 + i] = Mathf.Lerp(d[n - 400 + i], d[i], k); }
            var c = AudioClip.Create("skate noise", n, 1, 44100, false); c.SetData(d, 0); return c;
        }

        static AudioClip Sweep(float f0, float f1, float len)
        {
            int n = (int)(44100 * len); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++) { float u = i / (float)n; ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u * u) / 44100f; d[i] = (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 2.01f) * 0.2f) * (1f - u) * Mathf.Min(1f, u * 20f) * 0.6f; }
            var c = AudioClip.Create("sweep", n, 1, 44100, false); c.SetData(d, 0); return c;
        }
    }
}
