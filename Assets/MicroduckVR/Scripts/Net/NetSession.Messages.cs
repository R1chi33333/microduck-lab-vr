using System;
using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.Custom;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MicroduckVR.Net
{
    public partial class NetSession
    {
        // ================================================================== sending
        internal FastBufferWriter Begin(NetMsg type)
        {
            var w = new FastBufferWriter(1200, Allocator.Temp, 60000);
            w.WriteValueSafe((byte)type);
            return w;
        }

        /// <summary>Send to the other PC and free the writer. Reliable: in order, never lost (events); otherwise the newest wins (poses).</summary>
        internal void Send(FastBufferWriter w, bool reliable)
        {
            try
            {
                if (nm == null || nm.CustomMessagingManager == null) return;
                var d = reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;
                if (nm.IsServer) { if (remoteId.HasValue) nm.CustomMessagingManager.SendNamedMessage(Channel, remoteId.Value, w, d); }
                else if (nm.IsConnectedClient) nm.CustomMessagingManager.SendNamedMessage(Channel, NetworkManager.ServerClientId, w, d);
            }
            catch (Exception e) { Debug.LogWarning("[Microduck] net: send failed: " + e.Message); }
            finally { w.Dispose(); }
        }

        static string CustomJson()
        {
            var ms = MirrorStation.Instance;
            var c = ms != null && ms.Current != null ? ms.Current : DuckCustom.Load();
            return JsonUtility.ToJson(c);
        }

        void WriteMe(ref FastBufferWriter w)
        {
            var pd = PlayerDuck.Instance;
            w.WriteValueSafe(Name());
            w.WriteValueSafe(CustomJson());
            w.WriteValueSafe(pd != null && pd.Body != null ? pd.Body.Pos : Vector3.zero);
            w.WriteValueSafe(pd != null && pd.Body != null ? pd.Body.Yaw : 0f);
        }

        RemotePlayer SpawnRemote(ref FastBufferReader r)
        {
            r.ReadValueSafe(out string name); r.ReadValueSafe(out string custom); r.ReadValueSafe(out Vector3 pos); r.ReadValueSafe(out float yaw);
            if (Remote != null) { Remote.ApplyCustom(custom); return Remote; }
            var pd = PlayerDuck.Instance; var src = pd != null ? pd.GetComponent<DuckSetup>() : null;
            if (src == null || src.rigPrefab == null) return null;
            Remote = RemotePlayer.Create(src, pos, yaw, custom, name);
            return Remote;
        }

        // ================================================================== receiving
        void OnMessage(ulong sender, FastBufferReader r)
        {
            try
            {
                if (nm != null && nm.IsServer && (!remoteId.HasValue || remoteId.Value != sender))
                {
                    // the first word from a new player must be hello (the approval already capped the house at two)
                    if (remoteId.HasValue) return;
                    r.ReadValueSafe(out byte first);
                    if ((NetMsg)first != NetMsg.Hello) return;
                    remoteId = sender;
                    OnHello(ref r);
                    return;
                }
                lastHeard = Time.unscaledTime;
                r.ReadValueSafe(out byte b);
                var type = (NetMsg)b;
                switch (type)
                {
                    case NetMsg.Welcome: OnWelcome(ref r); break;
                    case NetMsg.Bye: if (mode == Mode.Hosting) RemoteGone("PLAYER 2 LEFT"); else pendingEnd = "HOST LEFT"; break;
                    case NetMsg.Custom: { r.ReadValueSafe(out string json); if (Remote != null) Remote.ApplyCustom(json); break; }
                    case NetMsg.Quack: if (Remote != null) Remote.Quack(); break;
                    case NetMsg.Emote: { r.ReadValueSafe(out byte k); r.ReadValueSafe(out float d); if (Remote != null) Remote.Emote((EmoteKind)k, d); break; }
                    case NetMsg.Fall: { r.ReadValueSafe(out Vector3 dir); if (Remote != null) Remote.Tip(dir); break; }
                    case NetMsg.Word: { r.ReadValueSafe(out string word); r.ReadValueSafe(out string hex); r.ReadValueSafe(out float size); if (Remote != null) Remote.Word(word, hex, size); break; }
                    case NetMsg.Pow:
                    {
                        r.ReadValueSafe(out string word); r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out string hex);
                        var cam = ComicPop.Viewer != null ? ComicPop.Viewer : Camera.main;
                        float k = cam != null ? Mathf.Clamp(Vector3.Distance(cam.transform.position, at), 1f, 3f) : 1f;   // far ones bigger, as on the host
                        ComicPop.Spawn(word, at, hex, 1.2f * k);
                        break;
                    }
                    case NetMsg.Splash: { r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out float size); ComicSplash.Spawn(at, size); break; }
                    case NetMsg.PlayerState: OnPlayerState(ref r); break;

                    // the flock (host -> joining PC)
                    case NetMsg.FlockRoster: if (mode == Mode.Joined) Flock.OnRoster(ref r); break;
                    case NetMsg.FlockAdd: if (mode == Mode.Joined) Flock.OnAdd(ref r); break;
                    case NetMsg.FlockRemove: if (mode == Mode.Joined) Flock.OnRemove(ref r); break;
                    case NetMsg.FlockMeta: if (mode == Mode.Joined) Flock.OnMeta(ref r); break;
                    case NetMsg.FlockEvent: if (mode == Mode.Joined) Flock.OnEvent(ref r); break;
                    case NetMsg.FlockEmote: if (mode == Mode.Joined) Flock.OnEmote(ref r); break;
                    case NetMsg.FlockState: if (mode == Mode.Joined) Flock.OnState(ref r); break;
                    // (joining PC -> host)
                    case NetMsg.AddDuckRequest:
                        if (mode == Mode.Hosting && Remote != null && Remote.Body != null) FlockSpawner.Add(Remote.Body.Pos, GltfSpace.Forward(Remote.Body.Yaw));
                        break;
                    case NetMsg.RemoveDuckRequest: if (mode == Mode.Hosting) FlockSpawner.Remove(); break;

                    // toys and props
                    case NetMsg.PropMap: if (mode == Mode.Joined) Props.OnMap(ref r); break;
                    case NetMsg.ToySpawn: if (mode == Mode.Joined) Props.OnSpawn(ref r); break;
                    case NetMsg.ToyGone: if (mode == Mode.Joined) Props.OnGone(ref r); break;
                    case NetMsg.GrabDenied: if (mode == Mode.Joined) Props.OnDenied(ref r); break;
                    case NetMsg.PropState: if (mode == Mode.Joined) Props.OnState(ref r); break;
                    case NetMsg.ToyRequest: if (mode == Mode.Hosting) Props.OnToyRequest(ref r); break;
                    case NetMsg.TrashRequest: if (mode == Mode.Hosting) Props.OnTrashRequest(ref r); break;
                    case NetMsg.Grab: if (mode == Mode.Hosting) Props.OnGrab(ref r); break;
                    case NetMsg.Drop: if (mode == Mode.Hosting) Props.OnDrop(ref r); break;
                    case NetMsg.Push: if (mode == Mode.Hosting) Props.OnPush(ref r); break;
                }
            }
            catch (Exception e) { Debug.LogWarning("[Microduck] net: bad message: " + e.Message); }
        }

        /// <summary>Hosting PC: a player arrived. Their duck appears; they get my duck, the flock and the toys.</summary>
        void OnHello(ref FastBufferReader r)
        {
            r.ReadValueSafe(out string version);
            if (version != Version) { remoteId = null; return; }
            if (SpawnRemote(ref r) == null) { remoteId = null; return; }
            lastHeard = Time.unscaledTime;
            var w = Begin(NetMsg.Welcome); WriteMe(ref w); Send(w, true);
            Flock.HostBegin();
            Props.HostBegin();
            HookPlayer();
            sentCustom = CustomJson();
            GameHud.Get().Big("PLAYER 2 JOINED!", ComicColors.Yellow, 1.3f);
            Debug.Log("[Microduck] net: " + Remote.DisplayName + " joined");
        }

        /// <summary>Joining PC: the host said hello back. From now on its flock and toys are the ones in this house.</summary>
        void OnWelcome(ref FastBufferReader r)
        {
            if (mode != Mode.Connecting) return;
            if (SpawnRemote(ref r) == null) { EndSession("COULD NOT JOIN"); return; }
            SetMode(Mode.Joined);
            StopGames();
            TutorialCoach.HideAll();                       // the tutorial is your own; it does not follow you into someone's house
            HookPlayer();
            sentCustom = CustomJson();
            GameHud.Get().Big("CONNECTED!", "#1FA6A0", 1.3f);
            Debug.Log("[Microduck] net: joined " + Remote.DisplayName);
        }

        void OnPlayerState(ref FastBufferReader r)
        {
            r.ReadValueSafe(out double t);
            r.ReadValueSafe(out byte flags);
            var s = new PlayerSample { skating = (flags & 1) != 0, reach = (flags & 2) != 0, laser = (flags & 4) != 0 };
            r.ReadValueSafe(out s.pos); r.ReadValueSafe(out s.yaw);
            r.ReadValueSafe(out s.vx); r.ReadValueSafe(out s.vz); r.ReadValueSafe(out s.yawRate);
            r.ReadValueSafe(out s.gaze); r.ReadValueSafe(out s.jaw); r.ReadValueSafe(out s.hop);
            if (s.skating) { r.ReadValueSafe(out s.lean); r.ReadValueSafe(out s.stroke); r.ReadValueSafe(out s.strokeSide); r.ReadValueSafe(out s.wobble); }
            if (s.reach) { r.ReadValueSafe(out s.reachTarget); r.ReadValueSafe(out s.reachWeight); }
            if (s.laser) { r.ReadValueSafe(out s.laserFrom); r.ReadValueSafe(out s.laserPoint); }
            if (Remote == null) return;
            Playback.Observe(t);
            Remote.AddSample(t, s);
        }
    }
}
