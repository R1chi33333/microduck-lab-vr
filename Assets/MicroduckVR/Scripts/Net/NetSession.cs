using System;
using System.Linq;
using System.Text;
using MicroduckVR.Comic;
using MicroduckVR.Custom;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Net
{
    /// <summary>Message kinds (the first byte of every message).</summary>
    public enum NetMsg : byte
    {
        Hello = 1, Welcome, Custom, Quack, Emote, Fall, Word, Pow, Splash, Bye,
        FlockRoster = 20, FlockAdd, FlockRemove, FlockMeta, FlockEvent, FlockEmote, AddDuckRequest, RemoveDuckRequest,
        PropMap = 30, ToySpawn, ToyGone, ToyRequest, TrashRequest, Grab, Drop, GrabDenied, Push,
        PlayerState = 50, FlockState, PropState,
    }

    /// <summary>
    /// Two players in the same house over the local network (LAN co-op). One PC hosts (it is the server and a
    /// player), the other joins; no accounts, no internet. HOST and JOIN are in the toy bag; the joining PC finds
    /// the host by itself (LanDiscovery), or uses "hostIp" from microduck_net.json (NetConfig).
    ///
    /// Netcode for GameObjects carries the connection (Unity Transport, UDP), but no NetworkObjects are used:
    /// everything in this game is built in code, so this class sends its own small messages over one named
    /// message channel and matches things up by stable ids. The host runs the flock's brains and the physics
    /// (NetFlock, NetProps); both PCs send their own duck (RemotePlayer on the other side).
    ///
    /// Nothing here runs until HOST or JOIN is pressed: on your own the game is exactly the single-player game.
    /// Editor and desktop shortcuts: F5 host, F6 join, F7 leave.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class NetSession : MonoBehaviour
    {
        /// <summary>Both PCs must run the same version; it is checked when joining.</summary>
        public const string Version = "mdvr-coop-1";
        const string Channel = "microduck";
        const float PlayerRate = 1f / 30f, WorldRate = 1f / 20f, PeerTimeout = 6f, ConnectTimeout = 10f;

        public enum Mode { Off, Hosting, Searching, Connecting, Joined, Leaving }

        public static NetSession Instance { get; private set; }
        public static Mode State => Instance != null ? Instance.mode : Mode.Off;
        /// <summary>Hosting, or joined to a host: the games (football, race) and duck swapping wait for phase 2.</summary>
        public static bool InSession => State == Mode.Hosting || State == Mode.Joined;
        /// <summary>This PC joined a host: the host decides about the flock and the toys.</summary>
        public static bool Joined => State == Mode.Joined;
        public static bool IsHost => State == Mode.Hosting;
        /// <summary>The line on the toy bag.</summary>
        public static string Status => Instance != null ? Instance.StatusText() : "";

        // ---- state
        Mode mode = Mode.Off;
        GameObject nmGo, leavingGo; NetworkManager nm; UnityTransport utp;
        LanBeacon beacon; LanListener listener;
        NetConfig cfg = new NetConfig();
        string lastStatus = "", hostAddress = "", joinTarget = "";
        ulong? remoteId;
        float modeT, lastHeard, playerT, worldT, customT, leaveT;
        bool helloSent;
        string pendingEnd;
        string sentCustom = "";
        int chirps = -1;
        PlayerDuck hookedPlayer; Action<string, float> emoteHook; Action<Vector3> fallHook;

        internal RemotePlayer Remote { get; private set; }
        internal readonly NetClock Playback = new NetClock();
        internal NetFlock Flock { get; private set; }
        internal NetProps Props { get; private set; }
        internal bool PeerReady => Remote != null && (mode == Mode.Hosting || mode == Mode.Joined);
        internal double LocalTime => Time.realtimeSinceStartupAsDouble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindAnyObjectByType<NetSession>() == null) new GameObject("Net session").AddComponent<NetSession>(); }

        void Awake() { Instance = this; Flock = new NetFlock(this); Props = new NetProps(this); }
        bool tearingDown;
        void OnDestroy() { if (Instance == this) { tearingDown = true; EndSession(""); Instance = null; } }      // quitting / leaving the scene

        // ================================================================== public controls (toy bag, keys)
        public static void HostOrStop() { if (Instance == null) return; if (Instance.mode == Mode.Hosting) Instance.Leave(); else if (Instance.mode == Mode.Off) Instance.StartHosting(); }
        public static void JoinOrLeave() { if (Instance == null) return; if (Instance.mode == Mode.Off) Instance.StartJoining(); else if (Instance.mode != Mode.Hosting && Instance.mode != Mode.Leaving) Instance.Leave(); }
        public static void LeaveSession() { if (Instance != null && Instance.mode != Mode.Off && Instance.mode != Mode.Leaving) Instance.Leave(); }

        string StatusText()
        {
            switch (mode)
            {
                case Mode.Hosting: return Remote != null ? "CONNECTED: 2 PLAYERS" : "HOSTING  " + hostAddress + "  (WAITING)";
                case Mode.Searching: return lastStatus != "" ? lastStatus : modeT > 20f ? "SEARCHING... NO HOST FOUND YET" : "SEARCHING...";
                case Mode.Connecting: return "CONNECTING TO " + joinTarget + "...";
                case Mode.Joined: return "CONNECTED: 2 PLAYERS";
                case Mode.Leaving: return "LEAVING...";
                default: return lastStatus;
            }
        }

        bool CanStart()
        {
            if (mode != Mode.Off) return false;
            if (leavingGo != null) return false;                       // the last session is still closing
            var pd = PlayerDuck.Instance;
            return pd != null && pd.Body != null;
        }

        void StartHosting()
        {
            if (!CanStart()) return;
            cfg = NetConfig.Load();
            if (!StartNetcode(true, "127.0.0.1", cfg.Port)) { lastStatus = "COULD NOT HOST (PORT " + cfg.Port + " IN USE?)"; return; }
            try { beacon = new LanBeacon(cfg.DiscoveryPort, cfg.Port, Name()); }
            catch (Exception e) { Debug.LogWarning("[Microduck] net: no discovery beacon: " + e.Message); }
            hostAddress = LanDiscovery.MainAddress();
            SetMode(Mode.Hosting);
            StopGames();
            GameHud.Get().Big("HOSTING!", "#3A86FF", 1.1f);
            Debug.Log("[Microduck] net: hosting on port " + cfg.Port + " (" + hostAddress + ")" + (cfg.LoadedFrom != "" ? ", settings from " + cfg.LoadedFrom : ""));
        }

        void StartJoining()
        {
            if (!CanStart()) return;
            cfg = NetConfig.Load();
            lastStatus = "";
            if (!string.IsNullOrEmpty(cfg.hostIp)) { Connect(cfg.hostIp, cfg.Port); return; }
            try { listener = new LanListener(cfg.DiscoveryPort); }
            catch (Exception e)
            {
                Debug.LogWarning("[Microduck] net: cannot listen for hosts: " + e.Message);
                lastStatus = "CANNOT SEARCH: SET HOSTIP IN THE SETTINGS FILE"; return;
            }
            SetMode(Mode.Searching);
            GameHud.Get().Big("SEARCHING...", "#1FA6A0", 1.0f);
        }

        void Connect(string address, ushort port)
        {
            DisposeDiscovery();
            joinTarget = address;
            if (!StartNetcode(false, address, port)) { lastStatus = "COULD NOT CONNECT TO " + address; SetMode(Mode.Off); return; }
            helloSent = false;
            SetMode(Mode.Connecting);
            Debug.Log("[Microduck] net: connecting to " + address + ":" + port);
        }

        void Leave()
        {
            if (Remote != null || mode == Mode.Connecting) { try { var w = Begin(NetMsg.Bye); Send(w, true); } catch (Exception) { } }
            EndSession("LEFT");
        }

        void SetMode(Mode m) { mode = m; modeT = 0f; }

        static string Name()
        {
            string n = "";
            try { n = SystemInfo.deviceName; } catch (Exception) { }
            return LanDiscovery.CleanName(n);
        }

        static void StopGames()
        {
            if (FootballGame.Active) FootballGame.Instance.StopGame();
            if (RaceGame.Active) RaceGame.Instance.StopRace();
        }

        // ================================================================== Netcode for GameObjects
        bool StartNetcode(bool host, string address, ushort port)
        {
            try
            {
                nmGo = new GameObject("Network manager");
                nmGo.SetActive(false);                                           // configure before it wakes up
                utp = nmGo.AddComponent<UnityTransport>();
                nm = nmGo.AddComponent<NetworkManager>();
                nm.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = utp,
                    EnableSceneManagement = false,                                // both PCs already have the house; never load scenes over the network
                    ConnectionApproval = true,
                    ConnectionData = Encoding.UTF8.GetBytes(Version),
                };
                nmGo.SetActive(true);
                Application.runInBackground = true;                              // keep talking while the other window has the focus
                bool ok;
                if (host)
                {
                    utp.SetConnectionData(address, port, "0.0.0.0");               // listen on every network this PC is on
                    nm.ConnectionApprovalCallback = (request, response) =>
                    {
                        string v = request.Payload != null ? Encoding.UTF8.GetString(request.Payload) : "";
                        response.CreatePlayerObject = false;
                        response.Approved = v == Version && (request.ClientNetworkId == NetworkManager.ServerClientId || nm.ConnectedClientsIds.Count < 2);
                        if (!response.Approved) Debug.Log("[Microduck] net: turned away a player (version '" + v + "', or the house is full)");
                    };
                    nm.OnClientDisconnectCallback += OnClientDisconnect;
                    ok = nm.StartHost();
                }
                else
                {
                    utp.SetConnectionData(address, port);
                    ok = nm.StartClient();
                }
                if (!ok || nm.CustomMessagingManager == null) { StopNetcode(); return false; }
                nm.CustomMessagingManager.RegisterNamedMessageHandler(Channel, OnMessage);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Microduck] net: could not start: " + e);
                StopNetcode();
                return false;
            }
        }

        void StopNetcode()
        {
            if (nm != null)
            {
                try { if (nm.CustomMessagingManager != null) nm.CustomMessagingManager.UnregisterNamedMessageHandler(Channel); } catch (Exception) { }
                try { nm.OnClientDisconnectCallback -= OnClientDisconnect; } catch (Exception) { }
                try { if (nm.IsListening) nm.Shutdown(); } catch (Exception) { }
            }
            if (nmGo != null) { leavingGo = nmGo; Destroy(nmGo, 0.5f); }
            nm = null; utp = null; nmGo = null; remoteId = null;
        }

        void OnClientDisconnect(ulong id)
        {
            if (nm == null || !nm.IsServer || id == NetworkManager.ServerClientId) return;
            if (remoteId.HasValue && remoteId.Value == id) RemoteGone("PLAYER 2 LEFT");
        }

        void DisposeDiscovery()
        {
            beacon?.Dispose(); beacon = null;
            listener?.Dispose(); listener = null;
        }

        /// <summary>Back to single player: the other duck goes, the flock and the toys are this PC's own again.</summary>
        void EndSession(string why)
        {
            if (mode == Mode.Off || mode == Mode.Leaving) { DisposeDiscovery(); return; }
            bool wasJoined = mode == Mode.Joined || mode == Mode.Connecting;
            if (!tearingDown)                                    // (when the scene itself is going, everything goes with it)
            {
                if (wasJoined) { Flock.ClientEnd(); Props.ClientEnd(); }
                else { if (Remote != null) Props.ReleaseJoiner(Remote); Flock.HostEnd(); Props.HostEnd(); }
                if (Remote != null) Remote.Despawn();
                Unhook();
            }
            Remote = null;
            DisposeDiscovery();
            StopNetcode();
            pendingEnd = null;
            Playback.Reset();
            lastStatus = why;
            SetMode(Mode.Leaving); leaveT = 0f;
            if (!tearingDown && !string.IsNullOrEmpty(why) && why != "LEFT") GameHud.Get().Big(why, "#7B61FF", 1.3f);
            Debug.Log("[Microduck] net: session ended" + (why != "" ? " (" + why + ")" : ""));
        }

        /// <summary>Hosting PC: the other player went; keep hosting for the next one.</summary>
        void RemoteGone(string why)
        {
            if (Remote != null) { Props.ReleaseJoiner(Remote); Remote.Despawn(); Remote = null; }
            Flock.HostEnd(); Props.HostEnd(); Unhook();
            remoteId = null; Playback.Reset();
            GameHud.Get().Big(why, "#7B61FF", 1.2f);
            Debug.Log("[Microduck] net: " + why.ToLowerInvariant());
        }

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
                    case NetMsg.Emote: { r.ReadValueSafe(out string k); r.ReadValueSafe(out float d); if (Remote != null) Remote.Emote(k, d); break; }
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
            GameHud.Get().Big("PLAYER 2 JOINED!", "#FFC43D", 1.3f);
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

        // ================================================================== my own duck, to the other PC
        void HookPlayer()
        {
            Unhook();
            var pd = PlayerDuck.Instance; if (pd == null || pd.Body == null) return;
            hookedPlayer = pd;
            emoteHook = (k, d) => { if (PeerReady) { var w = Begin(NetMsg.Emote); w.WriteValueSafe(k ?? ""); w.WriteValueSafe(d); Send(w, true); } };
            fallHook = dir => { if (PeerReady) { var w = Begin(NetMsg.Fall); w.WriteValueSafe(dir); Send(w, true); } };
            pd.Body.OnEmote += emoteHook;
            pd.Body.OnFall += fallHook;
            chirps = pd.ChirpCount;
        }

        void Unhook()
        {
            if (hookedPlayer != null && hookedPlayer.Body != null)
            {
                if (emoteHook != null) hookedPlayer.Body.OnEmote -= emoteHook;
                if (fallHook != null) hookedPlayer.Body.OnFall -= fallHook;
            }
            hookedPlayer = null; emoteHook = null; fallHook = null;
        }

        void SendPlayerState()
        {
            var pd = PlayerDuck.Instance; if (pd == null || pd.Body == null) return;
            var b = pd.Body;
            var sk = pd.Skates;
            bool skating = sk != null && sk.On;
            bool reach = b.ReachState.target.HasValue && b.ReachState.weight > 0.01f;
            bool laser = LaserPointer.Active;
            var w = Begin(NetMsg.PlayerState);
            w.WriteValueSafe(LocalTime);
            w.WriteValueSafe((byte)((skating ? 1 : 0) | (reach ? 2 : 0) | (laser ? 4 : 0)));
            w.WriteValueSafe(b.Pos); w.WriteValueSafe(b.Yaw);
            w.WriteValueSafe(b.Vel.x); w.WriteValueSafe(b.Vel.z); w.WriteValueSafe(b.YawRate);
            w.WriteValueSafe(b.LookAt.gazeDir.HasValue ? b.LookAt.gazeDir.Value : GltfSpace.Forward(b.Yaw));
            w.WriteValueSafe(b.Jaw); w.WriteValueSafe(b.Hop);
            if (skating) { w.WriteValueSafe(b.SkateLean); w.WriteValueSafe(b.Stroke); w.WriteValueSafe(b.StrokeSide); w.WriteValueSafe(b.Wobble); }
            if (reach) { w.WriteValueSafe(b.ReachState.target.Value); w.WriteValueSafe(b.ReachState.weight); }
            if (laser) { w.WriteValueSafe(LaserFrom(pd)); w.WriteValueSafe(LaserPointer.Point); }
            Send(w, false);
        }

        /// <summary>Where my laser starts (the right hand, or just under the eyes without controllers), as LaserPointer draws it.</summary>
        static Vector3 LaserFrom(PlayerDuck pd)
        {
            var lp = pd.GetComponent<LaserPointer>();
            var cam = pd.cam;
            if (lp != null && lp.rightHand != null && lp.rightHand.localPosition.sqrMagnitude > 1e-6f) return lp.rightHand.position;
            return cam != null ? cam.transform.position - cam.transform.up * 0.02f : pd.Body.EyePosition;
        }

        void SendMyEvents()
        {
            var pd = PlayerDuck.Instance; if (pd == null) return;
            if (chirps < 0) chirps = pd.ChirpCount;
            if (pd.ChirpCount != chirps) { chirps = pd.ChirpCount; var w = Begin(NetMsg.Quack); Send(w, true); }
            customT -= Time.unscaledDeltaTime;
            if (customT <= 0f)
            {
                customT = 0.5f;                                       // the mirror: a new look shows on the other PC too
                string c = CustomJson();
                if (c != sentCustom) { sentCustom = c; var w = Begin(NetMsg.Custom); w.WriteValueSafe(c); Send(w, true); }
            }
        }

        // ================================================================== hooks for the rest of the game (no-ops on your own)
        /// <summary>A sound word about my own duck (ComicEvents): it pops over my duck on the other PC.</summary>
        public static void SendOwnWord(string word, string hex, float size)
        {
            var s = Instance; if (s == null || !s.PeerReady) return;
            var w = s.Begin(NetMsg.Word); w.WriteValueSafe(word ?? ""); w.WriteValueSafe(hex ?? "#FF5D73"); w.WriteValueSafe(size); s.Send(w, true);
        }

        /// <summary>A toy sent flying (only the host simulates toys): POW! on both PCs.</summary>
        public static void SendPow(string word, Vector3 at, string hex)
        {
            var s = Instance; if (s == null || !s.PeerReady || s.mode != Mode.Hosting) return;
            var w = s.Begin(NetMsg.Pow); w.WriteValueSafe(word ?? ""); w.WriteValueSafe(at); w.WriteValueSafe(hex ?? "#FF8A3D"); s.Send(w, true);
        }

        public static void SendSplash(Vector3 at, float size)
        {
            var s = Instance; if (s == null || !s.PeerReady) return;
            var w = s.Begin(NetMsg.Splash); w.WriteValueSafe(at); w.WriteValueSafe(size); s.Send(w, true);
        }

        /// <summary>Joining PC: toys from the bag come from the host. False when this PC places it itself.</summary>
        public static bool RequestToy(string kind, Vector3 pos, float yawDeg)
        {
            var s = Instance; if (s == null || s.mode != Mode.Joined) return false;
            s.Props.RequestToy(kind, pos, yawDeg); return true;
        }

        /// <summary>Joining PC: the bin asks the host to take a toy away. False when this PC removes it itself.</summary>
        public static bool RequestTrash(GameObject toy)
        {
            var s = Instance; if (s == null || s.mode != Mode.Joined) return false;
            return s.Props.RequestTrash(toy);
        }

        public static bool RequestAddDuck()
        {
            var s = Instance; if (s == null || s.mode != Mode.Joined) return false;
            var w = s.Begin(NetMsg.AddDuckRequest); s.Send(w, true); return true;
        }

        public static bool RequestRemoveDuck()
        {
            var s = Instance; if (s == null || s.mode != Mode.Joined) return false;
            var w = s.Begin(NetMsg.RemoveDuckRequest); s.Send(w, true); return true;
        }

        /// <summary>Joining PC: my duck walked into a host-simulated thing (PropPusher): the host pushes it.</summary>
        public static void QueuePush(Rigidbody rb, Vector3 dir, float want, float dv, Vector3 at)
        {
            var s = Instance; if (s == null || s.mode != Mode.Joined) return;
            s.Props.QueuePush(rb, dir, want, dv, at);
        }

        // ================================================================== per frame
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            modeT += dt;
            Keys();

            if (mode == Mode.Leaving) { leaveT += dt; if (leavingGo == null || leaveT > 3f) { leavingGo = null; mode = Mode.Off; } return; }
            if (mode == Mode.Off) return;
            if (pendingEnd != null) { var why = pendingEnd; pendingEnd = null; EndSession(why); return; }

            // the network went away under us
            if (mode != Mode.Searching && (nm == null || !nm.IsListening))
            {
                EndSession(mode == Mode.Hosting ? "HOSTING STOPPED" : mode == Mode.Joined ? "HOST LEFT" : "COULD NOT CONNECT");
                return;
            }

            switch (mode)
            {
                case Mode.Hosting:
                    beacon?.Tick(Time.unscaledTime);
                    if (Remote != null && Time.unscaledTime - lastHeard > PeerTimeout) RemoteGone("PLAYER 2 LOST");
                    else if (Remote != null && remoteId.HasValue && !nm.ConnectedClientsIds.Contains(remoteId.Value)) RemoteGone("PLAYER 2 LEFT");
                    else if (Remote == null && remoteId.HasValue && modeT > 1f && !nm.ConnectedClientsIds.Contains(remoteId.Value)) remoteId = null;
                    break;
                case Mode.Searching:
                    if (listener != null && listener.Poll(out var found))
                    {
                        if (found.version != Version) lastStatus = "FOUND A HOST WITH ANOTHER VERSION";
                        else { Connect(found.address, found.port); return; }
                    }
                    break;
                case Mode.Connecting:
                    if (nm.IsConnectedClient && !helloSent)
                    {
                        helloSent = true; lastHeard = Time.unscaledTime;
                        var w = Begin(NetMsg.Hello); w.WriteValueSafe(Version); WriteMe(ref w); Send(w, true);
                    }
                    if (modeT > ConnectTimeout) { EndSession("COULD NOT CONNECT"); return; }
                    break;
                case Mode.Joined:
                    if (Time.unscaledTime - lastHeard > PeerTimeout) { EndSession("HOST LOST"); return; }
                    break;
            }

            if (Remote == null) return;
            Playback.Advance(Time.unscaledDeltaTime);
            float bodyDt = Mathf.Min(Time.deltaTime, 1f / 30f);
            Remote.Step(Playback.Now, bodyDt);
            if (mode == Mode.Joined) Flock.ClientStep(Playback.Now, bodyDt);
        }

        void LateUpdate()
        {
            if (mode != Mode.Hosting && mode != Mode.Joined) return;
            if (Remote == null) return;
            float dt = Time.unscaledDeltaTime;
            if (mode == Mode.Joined) Props.ClientLateUpdate(Playback.Now);
            else Props.HostLateUpdate();

            SendMyEvents();
            playerT -= dt;
            if (playerT <= 0f) { playerT += PlayerRate; if (playerT < 0f) playerT = 0f; SendPlayerState(); }
            if (mode == Mode.Hosting)
            {
                worldT -= dt;
                if (worldT <= 0f) { worldT += WorldRate; if (worldT < 0f) worldT = 0f; Flock.HostTick(); Props.HostTick(); }
            }
        }

        void Keys()
        {
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.f5Key.wasPressedThisFrame) HostOrStop();
            if (kb.f6Key.wasPressedThisFrame) JoinOrLeave();
            if (kb.f7Key.wasPressedThisFrame) LeaveSession();
        }
    }
}
