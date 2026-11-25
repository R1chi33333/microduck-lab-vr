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

        void Awake() { Body = GetComponent<Rigidbody>(); }
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
                float d = Vector3.Distance(g.transform.position, p);
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }
    }
}
