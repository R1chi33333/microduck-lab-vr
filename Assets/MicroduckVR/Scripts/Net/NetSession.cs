using System;
using System.Linq;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Games;
using MicroduckVR.Player;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using Mode = MicroduckVR.Core.CoopMode;

namespace MicroduckVR.Net
{
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
    public partial class NetSession : MonoBehaviour
    {
        /// <summary>Both PCs must run the same version; it is checked when joining.</summary>
        public const string Version = "mdvr-coop-2";
        const string Channel = "microduck";
        const float PlayerRate = 1f / 30f, WorldRate = 1f / 20f, PeerTimeout = 6f, ConnectTimeout = 10f;

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
        PlayerDuck hookedPlayer; Action<EmoteKind, float> emoteHook; Action<Vector3> fallHook;

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
            GameHud.Get().Big("HOSTING!", ComicColors.Blue, 1.1f);
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
