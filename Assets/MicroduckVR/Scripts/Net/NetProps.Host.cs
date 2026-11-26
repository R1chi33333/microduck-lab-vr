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
    public partial class NetProps
    {
        // ================================================================== hosting PC
        /// <summary>Hosting PC: gives every prop an id and sends the map.</summary>
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

        /// <summary>Hosting PC: forgets the props.</summary>
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

        /// <summary>Hosting PC: sends new, removed and moving toys.</summary>
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

        /// <summary>Hosting PC: the joining player bit a toy.</summary>
        public void OnGrab(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            var rp = net.Remote;
            if (rp != null && byId.TryGetValue(id, out var p) && p.g != null && p.rb != null && (p.g.Holder == (Object)rp || p.g.TryHold(rp))) return;
            var w = net.Begin(NetMsg.GrabDenied); w.WriteValueSafe(id); net.Send(w, true);   // someone got there first
        }

        /// <summary>Hosting PC: the joining player let go of a toy.</summary>
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

        /// <summary>Hosting PC: the joining player walked into some toys.</summary>
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

        /// <summary>Hosting PC: the joining player wants a toy from the bag.</summary>
        public void OnToyRequest(ref FastBufferReader r)
        {
            r.ReadValueSafe(out string kind); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out float yaw);
            if (ToyCatalog.Get(kind) == null) return;
            var go = ToyCatalog.Spawn(kind, pos, yaw);
            var rb = go != null ? go.GetComponent<Rigidbody>() : null;
            if (rb != null && !byBody.ContainsKey(rb)) SendSpawn(Add(nextId++, rb, kind));
        }

        /// <summary>Hosting PC: the joining player binned a toy.</summary>
        public void OnTrashRequest(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            if (!byId.TryGetValue(id, out var p) || p.rb == null) return;
            if (p.rb.GetComponent<ToyTag>() == null || (p.g != null && p.g.IsHeld)) return;
            byId.Remove(id); byBody.Remove(p.rb);
            Object.Destroy(p.rb.gameObject);
            SendGone(id);
        }
    }
}
