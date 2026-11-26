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
        /// <summary>Joining PC: the host's address; empty searches the network.</summary>
        public string hostIp = "";
        /// <summary>Game port.</summary>
        public int port = 7777;
        /// <summary>Port of the LAN search beacons.</summary>
        public int discoveryPort = 47777;
        /// <summary>A player name in the settings file (the game does not read it yet).</summary>
        public string playerName = "";

        /// <summary>The settings file's name.</summary>
        public const string FileName = "microduck_net.json";
        [NonSerialized] public string LoadedFrom = "";

        /// <summary>The game port, clamped to a valid port.</summary>
        public ushort Port => (ushort)Mathf.Clamp(port, 1, 65535);
        /// <summary>The beacon port, clamped to a valid port.</summary>
        public int DiscoveryPort => Mathf.Clamp(discoveryPort, 1, 65535);

        /// <summary>The settings file next to the game's .exe (the project folder in the editor).</summary>
        public static string NextToGame
        {
            get { try { return Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FileName); } catch { return FileName; } }
        }
        /// <summary>The settings file in Application.persistentDataPath.</summary>
        public static string InPersistentData
        {
            get { try { return Path.Combine(Application.persistentDataPath, FileName); } catch { return FileName; } }
        }

        /// <summary>Reads the first settings file found; defaults when there is none.</summary>
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

        /// <summary>The default settings as JSON, for writing an example file.</summary>
        public static string Example() => JsonUtility.ToJson(new NetConfig(), true);
    }
}
