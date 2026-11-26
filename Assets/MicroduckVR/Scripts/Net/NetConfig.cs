using System;
using System.IO;
using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>
    /// Optional settings file "microduck_net.json", read each time you press HOST or JOIN. Looked for next to the
    /// game's .exe first (the project folder when playing in the editor), then in Application.persistentDataPath.
    /// Set "hostIp" on the joining PC when the network blocks the automatic search (broadcasts).
    /// Example: { "hostIp": "192.168.43.17", "port": 7777, "discoveryPort": 47777, "playerName": "" }
    /// </summary>
    [Serializable]
    public class NetConfig
    {
        public string hostIp = "";
        public int port = 7777;
        public int discoveryPort = 47777;
        public string playerName = "";

        public const string FileName = "microduck_net.json";
        [NonSerialized] public string LoadedFrom = "";

        public ushort Port => (ushort)Mathf.Clamp(port, 1, 65535);
        public int DiscoveryPort => Mathf.Clamp(discoveryPort, 1, 65535);

        public static string NextToGame
        {
            get { try { return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FileName); } catch { return FileName; } }
        }
        public static string InPersistentData
        {
            get { try { return Path.Combine(Application.persistentDataPath, FileName); } catch { return FileName; } }
        }

        public static NetConfig Load()
        {
            foreach (var p in new[] { NextToGame, InPersistentData })
            {
                try
                {
                    if (!File.Exists(p)) continue;
                    var c = JsonUtility.FromJson<NetConfig>(File.ReadAllText(p));
                    if (c == null) continue;
                    c.hostIp = (c.hostIp ?? "").Trim();
                    c.LoadedFrom = p;
                    return c;
                }
                catch (Exception e) { Debug.LogWarning("[Microduck] net: could not read " + p + ": " + e.Message); }
            }
            return new NetConfig();
        }

        public static string Example() => JsonUtility.ToJson(new NetConfig(), true);
    }
}
