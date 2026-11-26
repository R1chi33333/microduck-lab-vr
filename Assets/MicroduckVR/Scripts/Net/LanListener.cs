using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MicroduckVR.Net
{
    /// <summary>The joining PC's ear for beacons.</summary>
    public sealed class LanListener : IDisposable
    {
        public struct Found { public string address, version, name; public ushort port; }

        readonly UdpClient udp;

        public LanListener(int discoveryPort)
        {
            udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
        }

        /// <summary>The next beacon heard, if any (call every frame; never blocks).</summary>
        public bool Poll(out Found found)
        {
            found = default;
            try
            {
                while (udp.Available > 0)
                {
                    IPEndPoint from = null;
                    byte[] data = udp.Receive(ref from);
                    if (data == null || from == null) continue;
                    var parts = Encoding.UTF8.GetString(data).Split('|');
                    if (parts.Length < 4 || parts[0] != LanDiscovery.Magic) continue;
                    if (!ushort.TryParse(parts[2], out ushort gp) || gp == 0) continue;
                    found = new Found { address = from.Address.ToString(), version = parts[1], port = gp, name = parts[3] };
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        public void Dispose() { try { udp.Close(); } catch (Exception) { } }
    }
}
