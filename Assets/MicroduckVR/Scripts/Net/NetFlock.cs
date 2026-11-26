using System;
using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.Net
{
    /// <summary>
    /// The AI flock in a shared house. The hosting PC runs every duck's brain as usual and sends where each duck
    /// is and what it does (about 20 times a second, plus its thoughts, emotes and toy events when they happen).
    /// On the joining PC the same ducks are puppets (DuckBrain.Puppet): their brains are switched off and their
    /// bodies are posed from those snapshots. ADD DUCK / REMOVE DUCK on the host show up on both PCs.
    /// </summary>
    public class NetFlock
    {
        const int PerMessage = 10;
        readonly NetSession net;
        readonly Dictionary<DuckBrain, ushort> idOf = new Dictionary<DuckBrain, ushort>();
        readonly Dictionary<ushort, DuckBrain> byId = new Dictionary<ushort, DuckBrain>();
        readonly Dictionary<ushort, string> sentMeta = new Dictionary<ushort, string>();
        readonly Dictionary<ushort, Track<DuckSample>> tracks = new Dictionary<ushort, Track<DuckSample>>();
        readonly List<Action> unhook = new List<Action>();
        readonly HashSet<DuckBrain> seen = new HashSet<DuckBrain>();
        readonly List<ushort> scratch = new List<ushort>();
        ushort nextId = 1;

        public NetFlock(NetSession session) { net = session; }

        public bool TryGet(ushort id, out DuckBrain b) { if (byId.TryGetValue(id, out b) && b != null) return true; b = null; return false; }
        public bool TryGetId(DuckBrain b, out ushort id) => idOf.TryGetValue(b, out id);

        static DuckSetup SetupOf(DuckBrain b) => b != null ? b.GetComponent<DuckSetup>() : null;

        // ================================================================== host
        public void HostBegin()
        {
            HostEnd();
            foreach (var b in DuckBrain.All) if (b != null) Register(b);
            SendRoster();
        }

        ushort Register(DuckBrain b)
        {
            ushort id = nextId++;
            idOf[b] = id; byId[id] = b;
            var body = SetupOf(b) != null ? SetupOf(b).Body : null;
            if (body != null)
            {
                Action<string, float> em = (k, d) => SendEmote(id, k, d);
                body.OnEmote += em;
                unhook.Add(() => { if (body != null) body.OnEmote -= em; });
            }
            Action<DuckBrain, string, string> ev = (d, kind, ctx) => { if (kind == "got" || kind == "drop") SendEvent(id, kind, ctx); };
            b.OnEvent += ev;
            unhook.Add(() => { if (b != null) b.OnEvent -= ev; });
            return id;
        }

        public void HostEnd()
        {
            foreach (var u in unhook) { try { u(); } catch (Exception) { } }
            unhook.Clear(); idOf.Clear(); byId.Clear(); sentMeta.Clear();
        }

        void WriteEntry(ref FastBufferWriter w, ushort id, DuckBrain b)
        {
            var s = SetupOf(b);
            w.WriteValueSafe(id);
            w.WriteValueSafe(b.gameObject.name);
            w.WriteValueSafe(s != null ? s.skin : "cream");
            w.WriteValueSafe(b.seed);
            w.WriteValueSafe(b.index);
            w.WriteValueSafe(b.Pos);
            w.WriteValueSafe(s != null && s.Body != null ? s.Body.Yaw : 0f);
        }

        void SendRoster()
        {
            var list = new List<KeyValuePair<ushort, DuckBrain>>(byId);
            int i = 0;
            do
            {
                int n = Mathf.Min(PerMessage, list.Count - i);
                var w = net.Begin(NetMsg.FlockRoster);
                w.WriteValueSafe(i == 0);
                w.WriteValueSafe(i + n >= list.Count);
                w.WriteValueSafe((ushort)n);
                for (int k = 0; k < n; k++) WriteEntry(ref w, list[i + k].Key, list[i + k].Value);
                net.Send(w, true);
                i += n;
            } while (i < list.Count);
        }

        void SendEmote(ushort id, string kind, float dur)
        {
            if (!net.PeerReady) return;
            var w = net.Begin(NetMsg.FlockEmote); w.WriteValueSafe(id); w.WriteValueSafe(kind ?? ""); w.WriteValueSafe(dur); net.Send(w, true);
        }

        void SendEvent(ushort id, string kind, string ctx)
        {
            if (!net.PeerReady) return;
            var w = net.Begin(NetMsg.FlockEvent); w.WriteValueSafe(id); w.WriteValueSafe(kind ?? ""); w.WriteValueSafe(ctx ?? ""); net.Send(w, true);
        }

        /// <summary>Who joined and left the flock, what they think, and where everyone is.</summary>
        public void HostTick()
        {
            // ducks added (ADD DUCK) and sent home (REMOVE DUCK)
            foreach (var b in DuckBrain.All)
            {
                if (b == null || idOf.ContainsKey(b)) continue;
                ushort id = Register(b);
                var w = net.Begin(NetMsg.FlockAdd); WriteEntry(ref w, id, b); net.Send(w, true);
            }
            scratch.Clear();
            foreach (var kv in byId) if (kv.Value == null || !DuckBrain.All.Contains(kv.Value)) scratch.Add(kv.Key);
            foreach (var id in scratch)
            {
                if (byId.TryGetValue(id, out var gone)) idOf.Remove(gone);
                byId.Remove(id); sentMeta.Remove(id);
                var w = net.Begin(NetMsg.FlockRemove); w.WriteValueSafe(id); net.Send(w, true);
            }

            // what each duck is up to (the thought bubbles read it)
            foreach (var kv in byId)
            {
                var b = kv.Value; if (b == null) continue;
                string meta = b.State + "\n" + b.Context;
                if (sentMeta.TryGetValue(kv.Key, out var was) && was == meta) continue;
                sentMeta[kv.Key] = meta;
                var w = net.Begin(NetMsg.FlockMeta); w.WriteValueSafe(kv.Key); w.WriteValueSafe(b.State ?? ""); w.WriteValueSafe(b.Context ?? ""); net.Send(w, true);
            }

            // where everyone is
            var ids = new List<ushort>(byId.Keys);
            for (int i = 0; i < ids.Count; i += PerMessage)
            {
                int n = Mathf.Min(PerMessage, ids.Count - i);
                var w = net.Begin(NetMsg.FlockState);
                w.WriteValueSafe(net.LocalTime);
                w.WriteValueSafe((byte)n);
                for (int k = 0; k < n; k++)
                {
                    ushort id = ids[i + k]; var b = byId[id]; var s = SetupOf(b);
                    var body = s != null ? s.Body : null;
                    w.WriteValueSafe(id);
                    if (b == null || body == null) { w.WriteValueSafe((byte)255); continue; }
                    bool look = body.LookAt.target.HasValue && body.LookAt.weight > 0.01f;
                    bool reach = body.ReachState.target.HasValue && body.ReachState.weight > 0.01f;
                    byte flags = (byte)((body.SitTarget ? 1 : 0) | (body.CrouchTarget ? 2 : 0) | (look ? 4 : 0) | (reach ? 8 : 0));
                    w.WriteValueSafe(flags);
                    w.WriteValueSafe(b.Pos);
                    w.WriteValueSafe(body.Yaw);
                    w.WriteValueSafe(body.Vel.x); w.WriteValueSafe(body.Vel.z);
                    w.WriteValueSafe(body.YawRate);
                    w.WriteValueSafe(body.Jaw);
                    w.WriteValueSafe(body.Hop);
                    w.WriteValueSafe(body.HappyWiggle);
                    if (look) { w.WriteValueSafe(body.LookAt.target.Value); w.WriteValueSafe(body.LookAt.weight); }
                    if (reach) { w.WriteValueSafe(body.ReachState.target.Value); w.WriteValueSafe(body.ReachState.weight); }
                }
                net.Send(w, false);
            }
        }

        // ================================================================== joining PC
        struct Entry { public ushort id; public string name, skin; public int seed, index; public Vector3 pos; public float yaw; }

        static Entry ReadEntry(ref FastBufferReader r)
        {
            var e = new Entry();
            r.ReadValueSafe(out e.id);
            r.ReadValueSafe(out e.name);
            r.ReadValueSafe(out e.skin);
            r.ReadValueSafe(out e.seed);
            r.ReadValueSafe(out e.index);
            r.ReadValueSafe(out e.pos);
            r.ReadValueSafe(out e.yaw);
            return e;
        }

        DuckBrain Resolve(Entry e, out bool spawned)
        {
            spawned = false;
            if (byId.TryGetValue(e.id, out var known) && known != null) return known;
            foreach (var b in DuckBrain.All)
                if (b != null && !idOf.ContainsKey(b) && b.gameObject.name == e.name) return b;     // the same scene duck on this PC
            var pd = PlayerDuck.Instance; var src = pd != null ? pd.GetComponent<DuckSetup>() : null;
            if (src == null || src.rigPrefab == null) return null;
            spawned = true;
            return FlockSpawner.Make(src, e.name, e.pos, Quaternion.Euler(0f, e.yaw * Mathf.Rad2Deg, 0f), e.skin, e.seed, e.index);
        }

        DuckBrain Puppetize(Entry e, out bool spawned)
        {
            var b = Resolve(e, out spawned);
            if (b == null) return null;
            if (byId.TryGetValue(e.id, out var old) && old != b && old != null) Despawn(old);
            b.SetPuppet(true);
            idOf[b] = e.id; byId[e.id] = b;
            if (!tracks.ContainsKey(e.id)) tracks[e.id] = new Track<DuckSample>(DuckSample.Lerp);
            var s = SetupOf(b);
            if (s != null && s.Body != null)
            {
                if (s.skin != e.skin && s.Body.Model.childCount > 0) { s.skin = e.skin; ComicLook.ApplyToDuck(s.Body.Model.GetChild(0), e.skin); }
                if (s.Mover != null) s.Mover.transform.position = e.pos;
                s.Body.Teleport(e.pos, e.yaw);
            }
            return b;
        }

        void Despawn(DuckBrain b)
        {
            if (b == null) return;
            if (idOf.TryGetValue(b, out var id)) { byId.Remove(id); tracks.Remove(id); }
            idOf.Remove(b);
            var s = SetupOf(b);
            if (s != null && s.Mover != null) Object.Destroy(s.Mover.gameObject);
            Object.Destroy(b.gameObject);
        }

        public void OnRoster(ref FastBufferReader r)
        {
            r.ReadValueSafe(out bool first);
            r.ReadValueSafe(out bool last);
            r.ReadValueSafe(out ushort n);
            if (first) seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var b = Puppetize(ReadEntry(ref r), out _);
                if (b != null) seen.Add(b);
            }
            if (!last) return;
            // ducks only this PC had (added before joining) go home: the host's flock is the flock
            foreach (var b in new List<DuckBrain>(DuckBrain.All)) if (b != null && !seen.Contains(b)) Despawn(b);
            seen.Clear();
        }

        public void OnAdd(ref FastBufferReader r)
        {
            var b = Puppetize(ReadEntry(ref r), out _);
            if (b == null) return;
            ComicPop.Spawn("QUACK!", b.Pos + Vector3.up * 0.26f, "#FFC43D", 1.2f);
            GameHud.Get().Big("NEW DUCK!", "#FFC43D", 1.0f);
        }

        public void OnRemove(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id);
            if (!TryGet(id, out var b)) return;
            ComicPop.Spawn("BYE!", b.Pos + Vector3.up * 0.26f, "#7B61FF", 1.2f);
            Despawn(b);
        }

        public void OnMeta(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); r.ReadValueSafe(out string state); r.ReadValueSafe(out string ctx);
            if (TryGet(id, out var b)) b.PuppetState(state, ctx);
        }

        public void OnEvent(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); r.ReadValueSafe(out string kind); r.ReadValueSafe(out string ctx);
            if (TryGet(id, out var b)) b.PuppetEvent(kind, ctx);
        }

        public void OnEmote(ref FastBufferReader r)
        {
            r.ReadValueSafe(out ushort id); r.ReadValueSafe(out string kind); r.ReadValueSafe(out float dur);
            if (TryGet(id, out var b)) { var s = SetupOf(b); if (s != null && s.Body != null) s.Body.PlayEmote(kind, dur); }
        }

        public void OnState(ref FastBufferReader r)
        {
            r.ReadValueSafe(out double t);
            net.Playback.Observe(t);
            r.ReadValueSafe(out byte n);
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id);
                r.ReadValueSafe(out byte flags);
                if (flags == 255) continue;
                var d = new DuckSample { sit = (flags & 1) != 0, crouch = (flags & 2) != 0, look = (flags & 4) != 0, reach = (flags & 8) != 0 };
                r.ReadValueSafe(out d.pos);
                r.ReadValueSafe(out d.yaw);
                r.ReadValueSafe(out d.vx); r.ReadValueSafe(out d.vz);
                r.ReadValueSafe(out d.yawRate);
                r.ReadValueSafe(out d.jaw);
                r.ReadValueSafe(out d.hop);
                r.ReadValueSafe(out d.happy);
                if (d.look) { r.ReadValueSafe(out d.lookTarget); r.ReadValueSafe(out d.lookWeight); }
                if (d.reach) { r.ReadValueSafe(out d.reachTarget); r.ReadValueSafe(out d.reachWeight); }
                if (tracks.TryGetValue(id, out var tr)) tr.Add(t, d);
            }
        }

        /// <summary>Pose every puppet duck at the playback time (once a frame).</summary>
        public void ClientStep(double t, float dt)
        {
            foreach (var kv in byId)
            {
                var b = kv.Value; if (b == null) continue;
                var s = SetupOf(b); if (s == null || s.Body == null) continue;
                var body = s.Body;
                if (tracks.TryGetValue(kv.Key, out var tr) && tr.Sample(t, out var d))
                {
                    body.JawTarget = d.jaw; body.JawStop = 0f;
                    body.Hop = d.hop; body.HappyWiggle = d.happy;
                    body.SitTarget = d.sit; body.CrouchTarget = d.crouch;
                    body.LookAt.target = d.look ? d.lookTarget : (Vector3?)null; body.LookAt.weight = d.look ? d.lookWeight : 0f;
                    body.ReachState.target = d.reach ? d.reachTarget : (Vector3?)null; body.ReachState.weight = d.reach ? d.reachWeight : 0f;
                    b.PuppetStep(dt, d.pos, d.yaw, new Vector3(d.vx, 0f, d.vz), d.yawRate);
                }
                else b.PuppetStep(dt, b.Pos, body.Yaw, Vector3.zero, 0f);
            }
        }

        /// <summary>Back to a flock of your own: the puppets get their brains back where they stand.</summary>
        public void ClientEnd()
        {
            foreach (var b in byId.Values) if (b != null) b.SetPuppet(false);
            byId.Clear(); idOf.Clear(); tracks.Clear(); seen.Clear();
        }
    }
}
