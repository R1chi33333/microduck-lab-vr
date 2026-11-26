using System.IO;
using MicroduckVR.Net;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Playing together (LAN co-op): host / join from the menu while in Play mode, and the optional settings file.</summary>
    public static class NetMenu
    {
        [MenuItem("Microduck/Co-op/Host or Stop (F5)")]
        static void Host() => NetSession.HostOrStop();
        [MenuItem("Microduck/Co-op/Host or Stop (F5)", true)]
        static bool HostOk() => Application.isPlaying;

        [MenuItem("Microduck/Co-op/Join or Leave (F6)")]
        static void Join() => NetSession.JoinOrLeave();
        [MenuItem("Microduck/Co-op/Join or Leave (F6)", true)]
        static bool JoinOk() => Application.isPlaying;

        [MenuItem("Microduck/Co-op/Leave (F7)")]
        static void Leave() => NetSession.LeaveSession();
        [MenuItem("Microduck/Co-op/Leave (F7)", true)]
        static bool LeaveOk() => Application.isPlaying;

        /// <summary>Writes microduck_net.json in the project folder (used when playing in the editor) if there is none yet.</summary>
        [MenuItem("Microduck/Co-op/Create Settings File (manual host IP)")]
        static void CreateSettings()
        {
            string path = NetConfig.NextToGame;
            if (!File.Exists(path)) File.WriteAllText(path, NetConfig.Example());
            Debug.Log("[Microduck] co-op settings: " + Path.GetFullPath(path) + "  (set \"hostIp\" on the joining PC; leave it empty to search automatically)");
            EditorUtility.RevealInFinder(path);
        }
    }
}
