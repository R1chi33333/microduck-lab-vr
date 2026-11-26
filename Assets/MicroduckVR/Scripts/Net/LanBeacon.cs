using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MicroduckVR.Net
{
    /// <summary>The hosting PC's beacon.</summary>
    public sealed class LanBeacon : IDisposable
    {
        readonly UdpClient udp;
        readonly int port;
        readonly byte[] packet;
        List<IPEndPoint> targets;
        float next, refresh;

        /// <summary>Starts the beacon: it announces the game port and host name on the discovery port.</summary>
        public LanBeacon(int discoveryPort, int gamePort, string hostName)
        {
            port = discoveryPort;
            packet = Encoding.UTF8.GetBytes(LanDiscovery.Magic + "|" + NetSession.Version + "|" + gamePort + "|" + LanDiscovery.CleanName(hostName));
            udp = new UdpClient();
            udp.EnableBroadcast = true;
        }

        /// <summary>Sends the beacon when it is due (about once a second).</summary>
        public void Tick(float now)
        {
            if (now < next) return;
            next = now + 1f;
            if (targets == null || now > refresh)
            {
                refresh = now + 10f;                      // adapters come and go (hotspot reconnects)
                var bc = new List<IPAddress>();
                LanDiscovery.LocalAddresses(bc);
                targets = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, port), new IPEndPoint(IPAddress.Loopback, port) };
                foreach (var b in bc) targets.Add(new IPEndPoint(b, port));
            }
            foreach (var t in targets)
            {
                try { udp.Send(packet, packet.Length, t); }
                catch (Exception) { }                     // that network is down right now: the others still get it
            }
        }

        /// <summary>Closes the socket.</summary>
        public void Dispose() { try { udp.Close(); } catch (Exception) { } }
    }
}
