using MicroduckVR.Comic;
using MicroduckVR.Core;
using Unity.Netcode;
using UnityEngine;
using Mode = MicroduckVR.Core.CoopMode;

namespace MicroduckVR.Net
{
    public partial class NetSession
    {
        // ================================================================== hooks for the rest of the game (no-ops on your own)
        /// <summary>A sound word about my own duck (ComicEvents): it pops over my duck on the other PC.</summary>
        public static void SendOwnWord(string word, string hex, float size)
        {
            var s = Instance; if (s == null || !s.PeerReady) return;
            var w = s.Begin(NetMsg.Word); w.WriteValueSafe(word ?? ""); w.WriteValueSafe(hex ?? ComicColors.Pink); w.WriteValueSafe(size); s.Send(w, true);
        }

        /// <summary>A toy sent flying (only the host simulates toys): POW! on both PCs.</summary>
        public static void SendPow(string word, Vector3 at, string hex)
        {
            var s = Instance; if (s == null || !s.PeerReady || s.mode != Mode.Hosting) return;
            var w = s.Begin(NetMsg.Pow); w.WriteValueSafe(word ?? ""); w.WriteValueSafe(at); w.WriteValueSafe(hex ?? ComicColors.Orange); s.Send(w, true);
        }

        /// <summary>A splash in the water: it shows on the other PC too.</summary>
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

        /// <summary>Joining PC: ask the host for one more duck. False when this PC adds it itself.</summary>
        public static bool RequestAddDuck()
        {
            var s = Instance; if (s == null || s.mode != Mode.Joined) return false;
            var w = s.Begin(NetMsg.AddDuckRequest); s.Send(w, true); return true;
        }

        /// <summary>Joining PC: ask the host to take a duck away. False when this PC removes it itself.</summary>
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

        // ================================================================== Coop: the same hooks for code below the network layer
        /// <summary>Points Coop at the static controls and hooks above, in play mode and (for the editor tools) in edit mode.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void RegisterCoop() { Coop.Session = new CoopHooks(); }

        /// <summary>Forwards every Coop call to the NetSession statics, which handle a missing session themselves.</summary>
        sealed class CoopHooks : ICoopSession
        {
            public CoopMode State => NetSession.State;
            public string Status => NetSession.Status;
            public void HostOrStop() => NetSession.HostOrStop();
            public void JoinOrLeave() => NetSession.JoinOrLeave();
            public bool RequestToy(string kind, Vector3 pos, float yawDeg) => NetSession.RequestToy(kind, pos, yawDeg);
            public bool RequestTrash(GameObject toy) => NetSession.RequestTrash(toy);
            public bool RequestAddDuck() => NetSession.RequestAddDuck();
            public bool RequestRemoveDuck() => NetSession.RequestRemoveDuck();
            public void SendOwnWord(string word, string hex, float size) => NetSession.SendOwnWord(word, hex, size);
            public void SendPow(string word, Vector3 at, string hex) => NetSession.SendPow(word, at, hex);
            public void SendSplash(Vector3 at, float size) => NetSession.SendSplash(at, size);
            public void QueuePush(Rigidbody rb, Vector3 dir, float want, float dv, Vector3 at) => NetSession.QueuePush(rb, dir, want, dv, at);
        }
    }
}
