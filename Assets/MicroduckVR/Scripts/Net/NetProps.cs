using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.Net
{
    /// <summary>
    /// Toys and loose props in a shared house. The hosting PC owns the physics: it sends the pose of everything
    /// that moved (about 20 times a second) and who holds what. On the joining PC those things are kinematic and
    /// follow the host. When the joining player bites a toy, it holds it locally and tells the host, whose copy
    /// then rides in that player's duck's beak; letting go sends the exact drop pose and speed, and the host's
    /// physics carries on from there. Walking into things on the joining PC asks the host to push them.
    /// Toys from the toy bag are spawned by the host (a joining PC asks) and appear on both PCs.
    ///
    /// Scene props are matched between the PCs by their place in the scene hierarchy (both PCs load the same
    /// scene); toys made at runtime (the bag, the tutorial) are sent with their kind and pose.
    /// </summary>
    public partial class NetProps
    {
        public const byte HeldNone = 0, HeldHost = 1, HeldJoiner = 2, HeldDuck = 3, HeldOther = 4;
        const int PerMessage = 28, KeysPerMessage = 16, PushesPerMessage = 24;

        class Prop
        {
            public ushort id; public Rigidbody rb; public Grabbable g; public string kind;   // kind: runtime toys only
            // hosting PC
            public Vector3 sentPos; public Quaternion sentRot; public byte sentHeld; public float sentAt = -99f; public bool everSent;
            // joining PC
            public Track<NetPose> track; public bool wasKinematic; public RigidbodyInterpolation interp;
            public byte held; public ushort heldDuck;
        }

        readonly NetSession net;
        readonly Dictionary<ushort, Prop> byId = new Dictionary<ushort, Prop>();
        readonly Dictionary<Rigidbody, Prop> byBody = new Dictionary<Rigidbody, Prop>();
        readonly Dictionary<ushort, (Vector3 dir, float want, float dv, Vector3 at)> pushes = new Dictionary<ushort, (Vector3, float, float, Vector3)>();
        readonly List<ushort> scratch = new List<ushort>();
        Dictionary<string, Rigidbody> localByKey;
        ushort nextId = 1;
        float scanT; int fullSends;
        Grabbable lastHeld; bool lastHeldKnown;

        public NetProps(NetSession session) { net = session; }

        // ------------------------------------------------------------------ which things take part
        static bool Candidate(Rigidbody rb)
        {
            if (rb == null) return false;
            if (rb.GetComponentInParent<DuckBody>() != null || rb.GetComponent<CharacterController>() != null) return false;
            return !rb.isKinematic || rb.GetComponent<Grabbable>() != null;      // driven things (the toy train) stay local
        }

        /// <summary>A toy made at runtime (bag, tutorial): it sits at the scene root and has a ToyTag.</summary>
        static bool RuntimeToy(Rigidbody rb) => rb.transform.parent == null && rb.GetComponent<ToyTag>() != null;

        /// <summary>Where a thing sits in the scene: names from the root, with a count among same-named siblings.</summary>
        static string Key(Transform t)
        {
            var parts = new List<string>();
            for (var x = t; x != null; x = x.parent) parts.Add(x.name + "#" + SameNameIndex(x));
            parts.Add(t.gameObject.scene.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        static int SameNameIndex(Transform x)
        {
            int k = 0;
            if (x.parent != null)
            {
                for (int i = 0; i < x.parent.childCount; i++) { var c = x.parent.GetChild(i); if (c == x) return k; if (c.name == x.name) k++; }
                return k;
            }
            foreach (var r in x.gameObject.scene.GetRootGameObjects())
            {
                if (r.transform == x) return k;
                if (r.name == x.name && r.GetComponent<ToyTag>() == null) k++;
            }
            return k;
        }

        Prop Add(ushort id, Rigidbody rb, string kind)
        {
            var p = new Prop { id = id, rb = rb, g = rb.GetComponent<Grabbable>(), kind = kind };
            byId[id] = p; byBody[rb] = p;
            return p;
        }

        public bool TryGetId(GameObject go, out ushort id)
        {
            id = 0;
            var rb = go != null ? go.GetComponent<Rigidbody>() : null;
            if (rb != null && byBody.TryGetValue(rb, out var p)) { id = p.id; return true; }
            return false;
        }

        static Rigidbody[] AllBodies() => Object.FindObjectsByType<Rigidbody>();
    }
}
