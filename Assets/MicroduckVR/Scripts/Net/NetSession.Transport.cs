using System;
using System.Text;
using MicroduckVR.Comic;
using MicroduckVR.Games;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Mode = MicroduckVR.Core.CoopMode;

namespace MicroduckVR.Net
{
    public partial class NetSession
    {
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
            if (!tearingDown && !string.IsNullOrEmpty(why) && why != "LEFT") GameHud.Get().Big(why, ComicColors.Purple, 1.3f);
            Debug.Log("[Microduck] net: session ended" + (why != "" ? " (" + why + ")" : ""));
        }

        /// <summary>Hosting PC: the other player went; keep hosting for the next one.</summary>
        void RemoteGone(string why)
        {
            if (Remote != null) { Props.ReleaseJoiner(Remote); Remote.Despawn(); Remote = null; }
            Flock.HostEnd(); Props.HostEnd(); Unhook();
            remoteId = null; Playback.Reset();
            GameHud.Get().Big(why, ComicColors.Purple, 1.2f);
            Debug.Log("[Microduck] net: " + why.ToLowerInvariant());
        }
    }
}
