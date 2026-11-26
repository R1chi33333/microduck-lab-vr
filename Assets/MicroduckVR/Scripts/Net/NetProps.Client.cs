using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.Player;
using MicroduckVR.World;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.Net
{
    public partial class NetProps
    {
        // ================================================================== joining PC
        void PuppetOn(Prop p)
        {
            p.track = new Track<NetPose>(NetPose.Lerp);
            p.wasKinematic = p.rb.isKinematic; p.interp = p.rb.interpolation;
            p.rb.interpolation = RigidbodyInterpolation.None;      // posed by hand from now on
            if (p.g == null || !p.g.IsHeld) p.rb.isKinematic = true;
        }

        /// <summary>Joining PC: the host's ids for the scene props (in parts).</summary>
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

        /// <summary>Joining PC: the host made a toy.</summary>
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

        /// <summary>Joining PC: the host removed a toy.</summary>
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

        /// <summary>Joining PC: the host refused a grab, so the duck drops the toy.</summary>
        public void OnDenied(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            var pd = PlayerDuck.Instance;
            if (pd != null && byId.TryGetValue(id, out var p) && p.g != null && pd.Held == p.g) pd.DropHeld();   // the next frame tells the host it is dropped
        }

        /// <summary>Joining PC: a batch of toy poses and holders.</summary>
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
        /// <summary>Joining PC: asks the host to push a toy my duck walked into.</summary>
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

        /// <summary>Joining PC: asks the host to bin a toy. False when the toy is not shared.</summary>
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
