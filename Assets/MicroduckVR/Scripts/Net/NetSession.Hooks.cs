using Unity.Netcode;
using UnityEngine;

namespace MicroduckVR.Net
{
    public partial class NetSession
    {
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
    }
}
