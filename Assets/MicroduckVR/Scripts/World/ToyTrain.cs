using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>
    /// Wind-up toy train running round its circular track: the engine and carriages follow the circle (kinematic
    /// bodies, so they shove loose toys out of the way), wheels spin to match the distance travelled, and the
    /// chimney puffs comic smoke clouds that rise, swell and pop.
    /// </summary>
    public class ToyTrain : MonoBehaviour
    {
        /// <summary>Centre of the circular track.</summary>
        public Vector3 centre;
        /// <summary>Radius of the track (m).</summary>
        public float radius = 0.55f;
        /// <summary>Speed along the track (m/s).</summary>
        public float speed = 0.11f;
        /// <summary>Y of the carriages' bottoms.</summary>
        public float height;
        /// <summary>The engine and carriages, engine first.</summary>
        public Transform[] cars;
        /// <summary>Arc distance of each car behind the engine.</summary>
        public float[] behind;
        /// <summary>Where the smoke comes out.</summary>
        public Transform chimneyTop;
        /// <summary>Material of the smoke clouds.</summary>
        public Material smokeMaterial;
        /// <summary>Mesh of the smoke clouds.</summary>
        public Mesh smokeMesh;
        /// <summary>Wheel radius (m), for the wheel spin.</summary>
        public float wheelRadius = 0.024f;

        float s;
        Rigidbody[] bodies;
        readonly List<(Transform t, Quaternion baseRot)> wheels = new List<(Transform, Quaternion)>();
        float wheelAngle;

        class Puff { public Transform t; public float age; public Vector3 drift; public bool live; }
        readonly List<Puff> puffs = new List<Puff>();
        float puffT;
        const float PuffLife = 1.7f, PuffEvery = 0.28f;

        void Start()
        {
            bodies = new Rigidbody[cars.Length];
            for (int i = 0; i < cars.Length; i++)
            {
                bodies[i] = cars[i].GetComponent<Rigidbody>();
                foreach (var t in cars[i].GetComponentsInChildren<Transform>()) if (t.name == "Wheel") wheels.Add((t, t.localRotation));
            }
            for (int i = 0; i < 10; i++)
            {
                var go = new GameObject("Smoke puff");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = smokeMesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = smokeMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false);
                puffs.Add(new Puff { t = go.transform });
            }
            Place(true);
        }

        /// <summary>Puts every car on the circle for the current arc position (also used by the editor builder).</summary>
        public void Place(bool teleport)
        {
            for (int i = 0; i < cars.Length; i++)
            {
                float a = (s - (behind != null && i < behind.Length ? behind[i] : 0f)) / radius;
                var p = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius; p.y = height;
                var rot = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
                var rb = bodies != null ? bodies[i] : null;
                if (rb != null && !teleport) { rb.MovePosition(p); rb.MoveRotation(rot); }
                else cars[i].SetPositionAndRotation(p, rot);
            }
        }

        void FixedUpdate()
        {
            s += speed * Time.fixedDeltaTime;
            Place(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            wheelAngle += speed * dt / wheelRadius * Mathf.Rad2Deg;
            foreach (var (t, r) in wheels) t.localRotation = Quaternion.AngleAxis(wheelAngle, Vector3.right) * r;

            puffT -= dt;
            if (puffT <= 0f && chimneyTop != null)
            {
                puffT = PuffEvery;
                foreach (var p in puffs)
                {
                    if (p.live) continue;
                    p.live = true; p.age = 0f; p.t.gameObject.SetActive(true);
                    p.t.position = chimneyTop.position;
                    p.drift = -cars[0].forward * speed * 0.6f + new Vector3(Random.Range(-0.01f, 0.01f), 0f, Random.Range(-0.01f, 0.01f));
                    break;
                }
            }
            foreach (var p in puffs)
            {
                if (!p.live) continue;
                p.age += dt;
                if (p.age >= PuffLife) { p.live = false; p.t.gameObject.SetActive(false); continue; }
                float u = p.age / PuffLife;
                p.t.position += (Vector3.up * Mathf.Lerp(0.09f, 0.03f, u) + p.drift) * dt;
                // pop out, swell, then shrink away (no transparency needed: comic puffs just shrink)
                float sc = u < 0.12f ? Mathf.SmoothStep(0.012f, 0.03f, u / 0.12f) : u < 0.7f ? Mathf.Lerp(0.03f, 0.055f, (u - 0.12f) / 0.58f) : Mathf.Lerp(0.055f, 0f, (u - 0.7f) / 0.3f);
                p.t.localScale = new Vector3(sc, sc * 0.85f, sc);
            }
        }
    }
}
