using UnityEngine;

namespace MicroduckVR.Core
{
    /// <summary>Where the LAN co-op session is.</summary>
    public enum CoopMode { Off, Hosting, Searching, Connecting, Joined, Leaving }

    /// <summary>The co-op session as the rest of the game sees it (the network code fills Coop.Session).</summary>
    public interface ICoopSession
    {
        /// <summary>Where the session is.</summary>
        CoopMode State { get; }
        /// <summary>The line on the toy bag.</summary>
        string Status { get; }
        /// <summary>Start hosting, or stop the session.</summary>
        void HostOrStop();
        /// <summary>Look for a host, or leave the session.</summary>
        void JoinOrLeave();
        /// <summary>Joining PC: ask the host for a toy from the bag; false when this PC places it itself.</summary>
        bool RequestToy(string kind, Vector3 pos, float yawDeg);
        /// <summary>Joining PC: ask the host to bin a toy; false when this PC removes it itself.</summary>
        bool RequestTrash(GameObject toy);
        /// <summary>Joining PC: ask the host for one more duck; false when this PC adds it itself.</summary>
        bool RequestAddDuck();
        /// <summary>Joining PC: ask the host to take a duck away; false when this PC removes it itself.</summary>
        bool RequestRemoveDuck();
        /// <summary>A sound word about my own duck, for the other PC.</summary>
        void SendOwnWord(string word, string hex, float size);
        /// <summary>Host: a toy sent flying, POW! on both PCs.</summary>
        void SendPow(string word, Vector3 at, string hex);
        /// <summary>A splash, for the other PC.</summary>
        void SendSplash(Vector3 at, float size);
        /// <summary>Joining PC: the host pushes a toy my duck walked into.</summary>
        void QueuePush(Rigidbody rb, Vector3 dir, float want, float dv, Vector3 at);
    }

    /// <summary>
    /// LAN co-op hooks for code below the network layer (the toy bag, sound words, toy nudges). Without a session
    /// object everything is a no-op: the state is Off, the status is empty and the Request calls return false, so
    /// the caller does the thing itself, as when playing on your own.
    /// </summary>
    public static class Coop
    {
        /// <summary>Filled by the network code when it loads.</summary>
        public static ICoopSession Session;

        /// <summary>The session's state; Off when there is no network code.</summary>
        public static CoopMode State => Session != null ? Session.State : CoopMode.Off;
        /// <summary>Hosting, or joined to a host: the games (football, race) and duck swapping wait for phase 2.</summary>
        public static bool InSession => State == CoopMode.Hosting || State == CoopMode.Joined;
        /// <summary>This PC joined a host: the host decides about the flock and the toys.</summary>
        public static bool Joined => State == CoopMode.Joined;
        /// <summary>This PC is hosting a session.</summary>
        public static bool IsHost => State == CoopMode.Hosting;
        /// <summary>The line on the toy bag.</summary>
        public static string Status => Session != null ? Session.Status : "";

        /// <summary>The bag's HOST button: start hosting, or stop the session.</summary>
        public static void HostOrStop() { if (Session != null) Session.HostOrStop(); }
        /// <summary>The bag's JOIN button: look for a host, or leave the session.</summary>
        public static void JoinOrLeave() { if (Session != null) Session.JoinOrLeave(); }

        /// <summary>Joining PC: toys from the bag come from the host. False when this PC places it itself.</summary>
        public static bool RequestToy(string kind, Vector3 pos, float yawDeg) => Session != null && Session.RequestToy(kind, pos, yawDeg);
        /// <summary>Joining PC: the bin asks the host to take a toy away. False when this PC removes it itself.</summary>
        public static bool RequestTrash(GameObject toy) => Session != null && Session.RequestTrash(toy);
        /// <summary>Joining PC: ask the host for one more duck. False when this PC adds it itself.</summary>
        public static bool RequestAddDuck() => Session != null && Session.RequestAddDuck();
        /// <summary>Joining PC: ask the host to take a duck away. False when this PC removes it itself.</summary>
        public static bool RequestRemoveDuck() => Session != null && Session.RequestRemoveDuck();

        /// <summary>A sound word about my own duck: it pops over my duck on the other PC.</summary>
        public static void SendOwnWord(string word, string hex, float size) { if (Session != null) Session.SendOwnWord(word, hex, size); }
        /// <summary>A toy sent flying (only the host simulates toys): POW! on both PCs.</summary>
        public static void SendPow(string word, Vector3 at, string hex) { if (Session != null) Session.SendPow(word, at, hex); }
        /// <summary>A splash in the water: it shows on the other PC too.</summary>
        public static void SendSplash(Vector3 at, float size) { if (Session != null) Session.SendSplash(at, size); }
        /// <summary>Joining PC: my duck walked into a host-simulated thing (PropPusher): the host pushes it.</summary>
        public static void QueuePush(Rigidbody rb, Vector3 dir, float want, float dv, Vector3 at) { if (Session != null) Session.QueuePush(rb, dir, want, dv, at); }
    }
}
