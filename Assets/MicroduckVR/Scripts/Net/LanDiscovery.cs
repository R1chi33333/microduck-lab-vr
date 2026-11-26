using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>
    /// Finding the other PC on the same network without typing anything: the hosting PC shouts a short UDP
    /// beacon once a second ("MDUCK|version|game port|name") to the broadcast address of every network it is on;
    /// a joining PC listens on the same port and connects to the first matching host it hears.
    /// </summary>
    public static class LanDiscovery
    {
        public const string Magic = "MDUCK";

        /// <summary>This PC's IPv4 addresses (not loopback), and the broadcast address of each network.</summary>
        public static List<IPAddress> LocalAddresses(List<IPAddress> broadcasts = null)
        {
            var ips = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                        ips.Add(ua.Address);
                        if (broadcasts == null) continue;
                        try
                        {
                            var mask = ua.IPv4Mask; if (mask == null) continue;
                            byte[] a = ua.Address.GetAddressBytes(), m = mask.GetAddressBytes();
                            if (a.Length != 4 || m.Length != 4) continue;
                            var b = new byte[4];
                            for (int i = 0; i < 4; i++) b[i] = (byte)(a[i] | ~m[i]);
                            var bip = new IPAddress(b);
                            if (!broadcasts.Contains(bip)) broadcasts.Add(bip);
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Microduck] net: could not list network adapters: " + e.Message); }
            return ips;
        }

        /// <summary>Best guess at the address the other PC should use (shown on the bag's status line).</summary>
        public static string MainAddress()
        {
            var ips = LocalAddresses();
            foreach (var ip in ips) { var s = ip.ToString(); if (!s.StartsWith("169.254.")) return s; }
            return ips.Count > 0 ? ips[0].ToString() : "";
        }

        public static string CleanName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "DUCK";
            var sb = new StringBuilder();
            foreach (char c in s) if (c != '|' && !char.IsControl(c)) sb.Append(c);
            var r = sb.ToString().Trim();
            if (r.Length > 24) r = r.Substring(0, 24);
            return r.Length > 0 ? r : "DUCK";
        }
    }
}
