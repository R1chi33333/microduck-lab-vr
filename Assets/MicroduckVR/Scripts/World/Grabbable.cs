using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>A small toy a duck can pick up with its beak (grip width must be ≤ 2.2 cm, as in the web version).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        public static readonly List<Grabbable> All = new List<Grabbable>();
        public string label = "toy";
        public float gripWidth = 0.02f;            // beak opening needed to hold it

        public Rigidbody Body { get; private set; }
        public Object Holder { get; private set; }
        public bool IsHeld => Holder != null;

        Collider col;
        /// <summary>Centre of the thing in its own space and its rough radius (for holding it by the middle).</summary>
        public Vector3 CentreLocal { get; private set; }
        public float Radius { get; private set; } = 0.01f;

        void Awake()
        {
            Body = GetComponent<Rigidbody>(); col = GetComponentInChildren<Collider>();
            if (col != null) { CentreLocal = transform.InverseTransformPoint(col.bounds.center); var e = col.bounds.extents; Radius = Mathf.Min(e.x, Mathf.Min(e.y, e.z)); }
        }

        /// <summary>How far a point is from the surface of the thing (0 inside it).</summary>
        public float DistanceTo(Vector3 p)
        {
            if (col == null || !col.enabled) return Vector3.Distance(transform.position, p);
            return Vector3.Distance(col.ClosestPoint(p), p);
        }
        public Vector3 Centre => col != null ? col.bounds.center : transform.position;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool TryHold(Object who)
        {
            if (Holder != null) return false;
            Holder = who;
            Body.isKinematic = true;
            Body.detectCollisions = false;
            return true;
        }

        public void Release(Vector3 velocity)
        {
            Holder = null;
            Body.isKinematic = false;
            Body.detectCollisions = true;
            Body.linearVelocity = velocity;
        }

        /// <summary>Nearest free toy within radius of a point.</summary>
        public static Grabbable Nearest(Vector3 p, float radius)
        {
            Grabbable best = null; float bd = radius;
            foreach (var g in All)
            {
                if (g.IsHeld) continue;
                float d = g.DistanceTo(p);
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }
    }
}
