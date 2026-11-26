using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Player;
using MicroduckVR.World;
using Unity.Netcode;
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
    public class NetProps
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

        // ================================================================== hosting PC
        public void HostBegin()
        {
            HostEnd();
            var keys = new List<(ushort, string)>();
            var toys = new List<Prop>();
            foreach (var rb in AllBodies())
            {
                if (!Candidate(rb)) continue;
                if (RuntimeToy(rb)) toys.Add(Add(nextId++, rb, rb.GetComponent<ToyTag>().kind));
                else keys.Add((Add(nextId++, rb, null).id, Key(rb.transform)));
            }
            // the scene's things, by their place in the scene
            int i = 0;
            do
            {
                int n = Mathf.Min(KeysPerMessage, keys.Count - i);
                var w = net.Begin(NetMsg.PropMap);
                w.WriteValueSafe(i == 0); w.WriteValueSafe((ushort)n);
                for (int k = 0; k < n; k++) { w.WriteValueSafe(keys[i + k].Item1); w.WriteValueSafe(keys[i + k].Item2); }
                net.Send(w, true);
                i += n;
            } while (i < keys.Count);
            foreach (var p in toys) SendSpawn(p);
            fullSends = 3;                                  // everything's pose, a few times, so the joining PC starts in step
        }

        public void HostEnd() { byId.Clear(); byBody.Clear(); pushes.Clear(); }

        void SendSpawn(Prop p)
        {
            var w = net.Begin(NetMsg.ToySpawn);
            w.WriteValueSafe(p.id); w.WriteValueSafe(p.kind ?? ""); w.WriteValueSafe(p.rb.position); w.WriteValueSafe(p.rb.rotation);
            net.Send(w, true);
        }

        void SendGone(ushort id) { var w = net.Begin(NetMsg.ToyGone); w.WriteValueSafe(id); net.Send(w, true); }

        byte HeldBy(Prop p, out ushort duck)
        {
            duck = 0;
            if (p.g == null || !p.g.IsHeld) return HeldNone;
            var h = p.g.Holder;
            if (h is PlayerDuck) return HeldHost;
            if (h is RemotePlayer) return HeldJoiner;
            if (h is DuckBrain b && net.Flock.TryGetId(b, out duck)) return HeldDuck;
            return HeldOther;
        }

        public void HostTick()
        {
            float now = Time.unscaledTime;
            // gone (bin, scripts) and new (toy bag, tutorial)
            scratch.Clear();
            foreach (var kv in byId) if (kv.Value.rb == null) scratch.Add(kv.Key);
            foreach (var id in scratch) { byId.Remove(id); SendGone(id); }
            if (scratch.Count > 0) { var dead = new List<Rigidbody>(); foreach (var kv in byBody) if (kv.Key == null) dead.Add(kv.Key); foreach (var d in dead) byBody.Remove(d); }
            scanT -= Time.unscaledDeltaTime;
            if (scanT <= 0f)
            {
                scanT = 0.25f;
                foreach (var tag in Object.FindObjectsByType<ToyTag>())
                {
                    var rb = tag.GetComponent<Rigidbody>();
                    if (rb == null || byBody.ContainsKey(rb) || !RuntimeToy(rb) || !Candidate(rb)) continue;
                    SendSpawn(Add(nextId++, rb, tag.kind));
                }
            }

            // poses of what moved (and of everything now and then)
            bool full = fullSends > 0; if (full) fullSends--;
            var send = new List<Prop>();
            foreach (var p in byId.Values)
            {
                if (p.rb == null) continue;
                byte held = HeldBy(p, out _);
                Vector3 pos = p.rb.transform.position; Quaternion rot = p.rb.transform.rotation;
                bool moved = (pos - p.sentPos).sqrMagnitude > 1e-6f || Quaternion.Angle(rot, p.sentRot) > 0.5f;
                bool stale = now - p.sentAt > 3f + (p.id % 20) * 0.1f;
                if (full || !p.everSent || moved || held != p.sentHeld || stale) send.Add(p);
            }
            for (int i = 0; i < send.Count; i += PerMessage)
            {
                int n = Mathf.Min(PerMessage, send.Count - i);
                var w = net.Begin(NetMsg.PropState);
                w.WriteValueSafe(net.LocalTime);
                w.WriteValueSafe((byte)n);
                for (int k = 0; k < n; k++)
                {
                    var p = send[i + k];
                    byte held = HeldBy(p, out ushort duck);
                    Vector3 pos = p.rb.transform.position; Quaternion rot = p.rb.transform.rotation;
                    w.WriteValueSafe(p.id); w.WriteValueSafe(held); w.WriteValueSafe(duck);
                    w.WriteValueSafe(pos); w.WriteValueSafe(rot);
                    p.sentPos = pos; p.sentRot = rot; p.sentHeld = held; p.sentAt = now; p.everSent = true;
                }
                net.Send(w, false);
            }
        }

        /// <summary>The joining player's held toy rides in their duck's beak (also on the hosting PC each frame).</summary>
        public void HostLateUpdate()
        {
            var rp = net.Remote;
            if (rp == null || rp.Body == null) return;
            foreach (var p in byId.Values)
                if (p.g != null && p.rb != null && p.g.Holder == (Object)rp) Hold(p, rp.Body, true);
        }

        /// <summary>Put a held thing where a player's duck (player = true, held by its middle) or an AI duck holds it.</summary>
        static void Hold(Prop p, DuckBody body, bool player)
        {
            var t = p.rb.transform;
            if (player && p.g != null)
            {
                var hr = Quaternion.LookRotation(body.HeadDir(), Vector3.up);
                t.rotation = hr;
                t.position = body.BeakGap() + body.HeadDir() * Mathf.Min(p.g.Radius * 0.6f, 0.02f) - hr * Vector3.Scale(p.g.CentreLocal, t.lossyScale);
            }
            else { t.position = body.BeakGap(); t.rotation = Quaternion.LookRotation(body.HeadDir(), Vector3.up); }
        }

        public void OnGrab(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            var rp = net.Remote;
            if (rp != null && byId.TryGetValue(id, out var p) && p.g != null && p.rb != null && (p.g.Holder == (Object)rp || p.g.TryHold(rp))) return;
            var w = net.Begin(NetMsg.GrabDenied); w.WriteValueSafe(id); net.Send(w, true);   // someone got there first
        }

        public void OnDrop(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out Quaternion rot); r.ReadValueSafe(out Vector3 vel);
            var rp = net.Remote;
            if (!byId.TryGetValue(id, out var p) || p.g == null || p.rb == null || rp == null || p.g.Holder != (Object)rp) return;
            p.rb.transform.SetPositionAndRotation(pos, rot);
            p.rb.position = pos; p.rb.rotation = rot;
            p.g.Release(vel);
        }

        /// <summary>The joining player let go of everything (they left).</summary>
        public void ReleaseJoiner(RemotePlayer rp)
        {
            foreach (var p in byId.Values) if (p.g != null && p.rb != null && rp != null && p.g.Holder == (Object)rp) p.g.Release(Vector3.zero);
        }

        public void OnPush(ref FastBufferReader r)
        {
            r.ReadValueSafe(out byte n);
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id); r.ReadValueSafe(out Vector3 dir); r.ReadValueSafe(out float want); r.ReadValueSafe(out float dv); r.ReadValueSafe(out Vector3 at);
                if (!byId.TryGetValue(id, out var p) || p.rb == null || p.rb.isKinematic) continue;
                // the same push PropPusher gives for a duck walking into it on this PC
                float along = Vector3.Dot(p.rb.linearVelocity, dir);
                if (along >= want) continue;
                p.rb.AddForceAtPosition(dir * Mathf.Min(want - along, dv), at, ForceMode.VelocityChange);
                p.rb.WakeUp();
            }
        }

        public void OnToyRequest(ref FastBufferReader r)
        {
            r.ReadValueSafe(out string kind); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out float yaw);
            if (ToyCatalog.Get(kind) == null) return;
            var go = ToyCatalog.Spawn(kind, pos, yaw);
            var rb = go != null ? go.GetComponent<Rigidbody>() : null;
            if (rb != null && !byBody.ContainsKey(rb)) SendSpawn(Add(nextId++, rb, kind));
        }

        public void OnTrashRequest(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            if (!byId.TryGetValue(id, out var p) || p.rb == null) return;
            if (p.rb.GetComponent<ToyTag>() == null || (p.g != null && p.g.IsHeld)) return;
            byId.Remove(id); byBody.Remove(p.rb);
            Object.Destroy(p.rb.gameObject);
            SendGone(id);
        }

        // ================================================================== joining PC
        void PuppetOn(Prop p)
        {
            p.track = new Track<NetPose>(NetPose.Lerp);
            p.wasKinematic = p.rb.isKinematic; p.interp = p.rb.interpolation;
            p.rb.interpolation = RigidbodyInterpolation.None;      // posed by hand from now on
            if (p.g == null || !p.g.IsHeld) p.rb.isKinematic = true;
        }

        public void OnMap(ref FastBufferReader r)
        {
            r.ReadValueSafe(out bool first); r.ReadValueSafe(out ushort n);
            if (first) ClientBegin();
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id); r.ReadValueSafe(out string key);
                if (byId.ContainsKey(id) || localByKey == null || !localByKey.TryGetValue(key, out var rb) || rb == null || byBody.ContainsKey(rb)) continue;
                PuppetOn(Add(id, rb, null));
            }
        }

        /// <summary>Joining: this PC's own bag toys go away (the host's toys come instead); the scene's things are listed.</summary>
        void ClientBegin()
        {
            ClientEnd();
            var pd = PlayerDuck.Instance;
            localByKey = new Dictionary<string, Rigidbody>();
            foreach (var rb in AllBodies())
            {
                if (!Candidate(rb)) continue;
                if (RuntimeToy(rb))
                {
                    if (pd != null && pd.Held != null && pd.Held.gameObject == rb.gameObject) pd.DropHeld();
                    Object.Destroy(rb.gameObject);
                    continue;
                }
                localByKey[Key(rb.transform)] = rb;
            }
            lastHeldKnown = false;
        }

        public void OnSpawn(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); r.ReadValueSafe(out string kind); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out Quaternion rot);
            if (byId.ContainsKey(id)) return;
            var go = ToyCatalog.Spawn(kind, pos, 0f);
            if (go == null) return;
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) { Object.Destroy(go); return; }
            PuppetOn(Add(id, rb, kind));
        }

        public void OnGone(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            if (!byId.TryGetValue(id, out var p)) return;
            byId.Remove(id);
            if (p.rb == null) return;
            byBody.Remove(p.rb);
            var pd = PlayerDuck.Instance;
            if (pd != null && pd.Held != null && pd.Held == p.g) { pd.DropHeld(); lastHeld = null; }
            Object.Destroy(p.rb.gameObject);
        }

        public void OnDenied(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            var pd = PlayerDuck.Instance;
            if (pd != null && byId.TryGetValue(id, out var p) && p.g != null && pd.Held == p.g) pd.DropHeld();   // the next frame tells the host it is dropped
        }

        public void OnState(ref FastBufferReader r)
        {
            r.ReadValueSafe(out double t);
            net.Playback.Observe(t);
            r.ReadValueSafe(out byte n);
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id); r.ReadValueSafe(out byte held); r.ReadValueSafe(out ushort duck);
                r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out Quaternion rot);
                if (!byId.TryGetValue(id, out var p) || p.rb == null || p.track == null) continue;
                p.track.Add(t, new NetPose { pos = pos, rot = rot });
                p.held = held; p.heldDuck = duck;
            }
        }

        /// <summary>Joining PC, once a frame after the bodies moved: tell the host about bites and drops, pose everything.</summary>
        public void ClientLateUpdate(double t)
        {
            var pd = PlayerDuck.Instance;
            var mine = pd != null ? pd.Held : null;
            if (!lastHeldKnown) { lastHeld = null; lastHeldKnown = true; }
            if (mine != lastHeld)
            {
                if (lastHeld != null && lastHeld.Body != null && byBody.TryGetValue(lastHeld.Body, out var was))
                {
                    var w = net.Begin(NetMsg.Drop);
                    w.WriteValueSafe(was.id); w.WriteValueSafe(was.rb.position); w.WriteValueSafe(was.rb.rotation); w.WriteValueSafe(was.rb.linearVelocity);
                    net.Send(w, true);
                    was.rb.isKinematic = true;                          // the host's physics takes it from here
                    was.track.Clear();
                }
                if (mine != null && mine.Body != null && byBody.TryGetValue(mine.Body, out var now))
                {
                    var w = net.Begin(NetMsg.Grab); w.WriteValueSafe(now.id); net.Send(w, true);
                }
                lastHeld = mine;
            }

            var rp = net.Remote;
            foreach (var p in byId.Values)
            {
                if (p.rb == null || p.track == null) continue;
                if (mine != null && p.g == mine) continue;                         // in my own beak: my duck carries it
                if (p.held == HeldJoiner && p.g != null)
                {
                    // the host still thinks it is in my beak, but it is not (a bite and a drop crossed): drop it there too
                    var w = net.Begin(NetMsg.Drop);
                    w.WriteValueSafe(p.id); w.WriteValueSafe(p.rb.position); w.WriteValueSafe(p.rb.rotation); w.WriteValueSafe(Vector3.zero);
                    net.Send(w, true);
                    p.held = HeldNone;
                }
                // held by the host's player or by an AI duck here: mark it taken, carry it in that beak
                DuckBody carrier = null; bool player = false; Object holder = null;
                if (p.held == HeldHost && rp != null) { carrier = rp.Body; player = true; holder = rp; }
                else if (p.held == HeldDuck && net.Flock.TryGet(p.heldDuck, out var heldBy)) { carrier = heldBy.Body; holder = heldBy; }
                bool taken = p.held != HeldNone && p.held != HeldJoiner;
                if (p.g != null)
                {
                    if (taken && !p.g.IsHeld) p.g.TryHold(holder != null ? holder : net);
                    else if (!taken && p.g.IsHeld) p.g.Release(Vector3.zero);
                }
                if (!p.rb.isKinematic) p.rb.isKinematic = true;
                if (carrier != null) { Hold(p, carrier, player); continue; }
                if (p.track.Sample(t, out var pose)) p.rb.transform.SetPositionAndRotation(pose.pos, pose.rot);
            }

            FlushPushes();
        }

        // ------------------------------------------------------------------ pushing things on the joining PC
        public void QueuePush(Rigidbody rb, Vector3 dir, float want, float dv, Vector3 at)
        {
            if (rb == null || !byBody.TryGetValue(rb, out var p)) return;
            if (pushes.TryGetValue(p.id, out var q)) pushes[p.id] = (dir, want, q.dv + dv, at);
            else pushes[p.id] = (dir, want, dv, at);
        }

        void FlushPushes()
        {
            if (pushes.Count == 0) return;
            var w = net.Begin(NetMsg.Push);
            w.WriteValueSafe((byte)Mathf.Min(pushes.Count, PushesPerMessage));
            int k = 0;
            foreach (var kv in pushes)
            {
                if (k++ >= PushesPerMessage) break;
                w.WriteValueSafe(kv.Key); w.WriteValueSafe(kv.Value.dir); w.WriteValueSafe(kv.Value.want); w.WriteValueSafe(kv.Value.dv); w.WriteValueSafe(kv.Value.at);
            }
            net.Send(w, false);
            pushes.Clear();
        }

        /// <summary>Joining PC: ask the host for a toy from the bag.</summary>
        public void RequestToy(string kind, Vector3 pos, float yawDeg)
        {
            var w = net.Begin(NetMsg.ToyRequest); w.WriteValueSafe(kind ?? ""); w.WriteValueSafe(pos); w.WriteValueSafe(yawDeg); net.Send(w, true);
        }

        public bool RequestTrash(GameObject go)
        {
            if (!TryGetId(go, out ushort id)) return false;
            var w = net.Begin(NetMsg.TrashRequest); w.WriteValueSafe(id); net.Send(w, true);
            return true;
        }

        /// <summary>Back on your own: everything is physics again, where it stands now.</summary>
        public void ClientEnd()
        {
            var pd = PlayerDuck.Instance;
            foreach (var p in byId.Values)
            {
                if (p.rb == null) continue;
                bool mine = pd != null && p.g != null && pd.Held == p.g;
                if (p.g != null && p.g.IsHeld && !mine) p.g.Release(Vector3.zero);
                if (p.track != null)
                {
                    p.rb.interpolation = p.interp;
                    if (!mine) { p.rb.isKinematic = false; p.rb.WakeUp(); }    // only free physics bodies take part, so that is how it was
                }
            }
            byId.Clear(); byBody.Clear(); pushes.Clear(); localByKey = null; lastHeld = null; lastHeldKnown = false;
        }
    }
}
