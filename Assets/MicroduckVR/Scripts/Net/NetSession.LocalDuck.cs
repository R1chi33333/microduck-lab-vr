using MicroduckVR.Duck;
using MicroduckVR.Games;
using MicroduckVR.Player;
using Unity.Netcode;
using UnityEngine;

namespace MicroduckVR.Net
{
    public partial class NetSession
    {
        // ================================================================== my own duck, to the other PC
        void HookPlayer()
        {
            Unhook();
            var pd = PlayerDuck.Instance; if (pd == null || pd.Body == null) return;
            hookedPlayer = pd;
            emoteHook = (k, d) => { if (PeerReady) { var w = Begin(NetMsg.Emote); w.WriteValueSafe((byte)k); w.WriteValueSafe(d); Send(w, true); } };
            fallHook = dir => { if (PeerReady) { var w = Begin(NetMsg.Fall); w.WriteValueSafe(dir); Send(w, true); } };
            pd.Body.OnEmote += emoteHook;
            pd.Body.OnFall += fallHook;
            chirps = pd.ChirpCount;
        }

        void Unhook()
        {
            if (hookedPlayer != null && hookedPlayer.Body != null)
            {
                if (emoteHook != null) hookedPlayer.Body.OnEmote -= emoteHook;
                if (fallHook != null) hookedPlayer.Body.OnFall -= fallHook;
            }
            hookedPlayer = null; emoteHook = null; fallHook = null;
        }

        void SendPlayerState()
        {
            var pd = PlayerDuck.Instance; if (pd == null || pd.Body == null) return;
            var b = pd.Body;
            var sk = pd.Skates;
            bool skating = sk != null && sk.On;
            bool reach = b.ReachState.target.HasValue && b.ReachState.weight > 0.01f;
            bool laser = LaserPointer.Active;
            var w = Begin(NetMsg.PlayerState);
            w.WriteValueSafe(LocalTime);
            w.WriteValueSafe((byte)((skating ? 1 : 0) | (reach ? 2 : 0) | (laser ? 4 : 0)));
            w.WriteValueSafe(b.Pos); w.WriteValueSafe(b.Yaw);
            w.WriteValueSafe(b.Vel.x); w.WriteValueSafe(b.Vel.z); w.WriteValueSafe(b.YawRate);
            w.WriteValueSafe(b.LookAt.gazeDir.HasValue ? b.LookAt.gazeDir.Value : GltfSpace.Forward(b.Yaw));
            w.WriteValueSafe(b.Jaw); w.WriteValueSafe(b.Hop);
            if (skating) { w.WriteValueSafe(b.SkateLean); w.WriteValueSafe(b.Stroke); w.WriteValueSafe(b.StrokeSide); w.WriteValueSafe(b.Wobble); }
            if (reach) { w.WriteValueSafe(b.ReachState.target.Value); w.WriteValueSafe(b.ReachState.weight); }
            if (laser) { w.WriteValueSafe(LaserFrom(pd)); w.WriteValueSafe(LaserPointer.Point); }
            Send(w, false);
        }

        /// <summary>Where my laser starts (the right hand, or just under the eyes without controllers), as LaserPointer draws it.</summary>
        static Vector3 LaserFrom(PlayerDuck pd)
        {
            var lp = pd.GetComponent<LaserPointer>();
            var cam = pd.cam;
            if (lp != null && lp.rightHand != null && lp.rightHand.localPosition.sqrMagnitude > 1e-6f) return lp.rightHand.position;
            return cam != null ? cam.transform.position - cam.transform.up * 0.02f : pd.Body.EyePosition;
        }

        void SendMyEvents()
        {
            var pd = PlayerDuck.Instance; if (pd == null) return;
            if (chirps < 0) chirps = pd.ChirpCount;
            if (pd.ChirpCount != chirps) { chirps = pd.ChirpCount; var w = Begin(NetMsg.Quack); Send(w, true); }
            customT -= Time.unscaledDeltaTime;
            if (customT <= 0f)
            {
                customT = 0.5f;                                       // the mirror: a new look shows on the other PC too
                string c = CustomJson();
                if (c != sentCustom) { sentCustom = c; var w = Begin(NetMsg.Custom); w.WriteValueSafe(c); Send(w, true); }
            }
        }
    }
}
