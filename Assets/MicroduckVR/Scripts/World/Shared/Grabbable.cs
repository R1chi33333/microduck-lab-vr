using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>A small toy a duck can pick up with its beak (grip width must be ≤ 2.2 cm, as in the web version).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : MonoBehaviour
    {
        /// <summary>Every enabled grabbable toy.</summary>
        public static readonly List<Grabbable> All = new List<Grabbable>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }
        /// <summary>The name shown in thought bubbles.</summary>
        public string label = "toy";
        /// <summary>Beak opening needed to hold it (m).</summary>
        public float gripWidth = 0.02f;

        /// <summary>The toy's rigidbody.</summary>
        public Rigidbody Body { get; private set; }
        /// <summary>Who holds it now (a duck brain, the player or a remote player), or null.</summary>
        public Object Holder { get; private set; }
        /// <summary>Someone is holding it.</summary>
        public bool IsHeld => Holder != null;

        Collider col;
        /// <summary>Centre of the thing in its own space and its rough radius (for holding it by the middle).</summary>
        public Vector3 CentreLocal { get; private set; }
        /// <summary>The toy's rough radius (its smallest half-extent).</summary>
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
        /// <summary>The centre of the toy in the world.</summary>
        public Vector3 Centre => col != null ? col.bounds.center : transform.position;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Takes the toy if nobody holds it: it stops being simulated until released.</summary>
        public bool TryHold(Object who)
        {
            if (Holder != null) return false;
            Holder = who;
            Body.isKinematic = true;
            Body.detectCollisions = false;
            return true;
        }

        /// <summary>Lets go of the toy with this velocity.</summary>
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
