using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Player
{
    public partial class BagMenu
    {
        // the bottom rows: games, emotes, swapping duck, the view
        static readonly (string kind, string label, string col)[] Actions =
        {
            (BagActions.Football, "FOOTBALL", ComicColors.Orange), (BagActions.Race, "RACE", ComicColors.Purple), (BagActions.Swap, "SWAP DUCK", "#1FA6A0"), (BagActions.View, "VIEW", ComicColors.Blue),
            (BagActions.Dance, "DANCE", ComicColors.Pink), (BagActions.Nod, "NOD", ComicColors.Pink), (BagActions.Shake, "SHAKE", ComicColors.Pink), (BagActions.Look, "LOOK", ComicColors.Pink),
            (BagActions.AddDuck, "ADD DUCK", ComicColors.Yellow), (BagActions.RemoveDuck, "REMOVE DUCK", ComicColors.Purple), (BagActions.Mirror, "MY LOOK", ComicColors.Orange), (BagActions.Tutorial, "TUTORIAL", ComicColors.Green),
            (BagActions.Help, "HELP", ComicColors.Yellow), (BagActions.Credits, "CREDITS", ComicColors.Green), (BagActions.Host, "HOST", ComicColors.Blue), (BagActions.Join, "JOIN", "#1FA6A0"),
        };

        static bool IsAction(string k) { foreach (var a in Actions) if (a.kind == k) return true; return false; }

        /// <summary>Playing together (phase 1): the games and duck swapping stay off until they work for two.</summary>
        static bool CoopOnly(string kind) => MicroduckVR.Net.NetSession.InSession && (kind == BagActions.Football || kind == BagActions.Race || kind == BagActions.Swap);

        void DoAction(string kind)
        {
            var pd = PlayerDuck.Instance;
            Close(); Play(clickClip);
            if (CoopOnly(kind)) { MicroduckVR.Games.GameHud.Get().Big("NOT IN CO-OP YET!", ComicColors.Purple, 1.3f); return; }
            switch (kind)
            {
                case BagActions.Host: MicroduckVR.Net.NetSession.HostOrStop(); break;
                case BagActions.Join: MicroduckVR.Net.NetSession.JoinOrLeave(); break;
                case BagActions.Football: var fg = MicroduckVR.Games.FootballGame.Ensure(); if (MicroduckVR.Games.FootballGame.Active) fg.StopGame(); else fg.StartGame(); break;
                case BagActions.Race: var rg = MicroduckVR.Games.RaceGame.Ensure(); if (MicroduckVR.Games.RaceGame.Active) rg.StopRace(); else rg.StartRace(); break;
                case BagActions.Swap: if (pd != null) pd.SwapWithNearest(); break;
                case BagActions.View: if (pd != null) pd.SetThirdPerson(!pd.thirdPerson); break;
                case BagActions.Help: TutorialCoach.ShowHelp(); break;
                case BagActions.AddDuck: if (!MicroduckVR.Net.NetSession.RequestAddDuck()) MicroduckVR.Flock.FlockSpawner.Add(); break;           // joined: the host adds it
                case BagActions.RemoveDuck: if (!MicroduckVR.Net.NetSession.RequestRemoveDuck()) MicroduckVR.Flock.FlockSpawner.Remove(); break;
                case BagActions.Mirror: if (pd != null) { pd.SetThirdPerson(false); pd.Teleport(MicroduckVR.Custom.MirrorStation.StandPos, Mathf.PI); pd.FaceView(Mathf.PI); if (MicroduckVR.Custom.MirrorStation.Instance != null) MicroduckVR.Custom.MirrorStation.Instance.OpenBoard(true); } break;
                case BagActions.Tutorial: TutorialCoach.Restart(); break;
                case BagActions.Credits: TutorialCoach.ShowSheet("CREDITS", TutorialCoach.CreditsText, "Y TO CLOSE"); break;
                default: if (pd != null) pd.Body.PlayEmote(EmoteFor(kind)); break;
            }
        }

        /// <summary>The emote a tile plays (DANCE, NOD, SHAKE, LOOK).</summary>
        static EmoteKind EmoteFor(string kind) => kind switch
        {
            BagActions.Dance => EmoteKind.Dance, BagActions.Nod => EmoteKind.Nod, BagActions.Shake => EmoteKind.Shake, BagActions.Look => EmoteKind.Look, _ => EmoteKind.None,
        };

        public void Choose(string kind)
        {
            if (IsAction(kind)) { DoAction(kind); return; }
            if (kind == BagActions.Trash) { Selected = null; panel.gameObject.SetActive(false); SetMode(Mode.Trash); Play(clickClip); return; }
            Selected = kind; panel.gameObject.SetActive(false); SetMode(Mode.Place); Play(clickClip);
        }

        void NetLabels()
        {
            var st = MicroduckVR.Net.NetSession.State;
            string s = MicroduckVR.Net.NetSession.Status;
            netLine.text = string.IsNullOrEmpty(s) ? "PLAY TOGETHER: HOST OR JOIN" : s;
            if (actionLabels.TryGetValue(BagActions.Host, out var h)) h.text = st == MicroduckVR.Net.NetSession.Mode.Hosting ? "STOP HOST" : "HOST";
            if (actionLabels.TryGetValue(BagActions.Join, out var j)) j.text = st == MicroduckVR.Net.NetSession.Mode.Joined ? "LEAVE" : st == MicroduckVR.Net.NetSession.Mode.Searching || st == MicroduckVR.Net.NetSession.Mode.Connecting ? "CANCEL" : "JOIN";
        }

        string Footer(string kind)
        {
            if (CoopOnly(kind)) return "NOT IN CO-OP YET (COMING LATER)";
            if (kind == BagActions.Trash) return "BIN: REMOVE TOYS";
            if (kind == BagActions.Football) return MicroduckVR.Games.FootballGame.Active ? "STOP THE MATCH" : "FOOTBALL ON THE RUG";
            if (kind == BagActions.Race) return MicroduckVR.Games.RaceGame.Active ? "STOP THE RACE" : "RACE ROUND THE HOUSE";
            if (kind == BagActions.Swap) return "BECOME THE DUCK IN FRONT";
            if (kind == BagActions.View) return "FIRST / THIRD PERSON";
            if (kind == BagActions.Help) return "HOW TO PLAY";
            if (kind == BagActions.AddDuck) return $"ANOTHER DUCK JOINS  ({MicroduckVR.Flock.DuckBrain.All.Count}/{MicroduckVR.Flock.FlockSpawner.Max})";
            if (kind == BagActions.RemoveDuck) return "SEND ONE DUCK HOME";
            if (kind == BagActions.Mirror) return "GO TO THE MIRROR: CHANGE YOUR DUCK";
            if (kind == BagActions.Tutorial) return "PLAY THE TUTORIAL AGAIN";
            if (kind == BagActions.Credits) return "WHO MADE THIS";
            if (kind == BagActions.Host) return MicroduckVR.Net.NetSession.IsHost ? "STOP HOSTING" : "LET A FRIEND JOIN YOUR HOUSE";
            if (kind == BagActions.Join) return MicroduckVR.Net.NetSession.State == MicroduckVR.Net.NetSession.Mode.Off ? "JOIN A FRIEND'S HOUSE" : "LEAVE";
            if (IsAction(kind)) return kind.ToUpperInvariant() + "!";
            var e = ToyCatalog.Get(kind);
            return $"{e.label.ToUpperInvariant()}  {Count(kind)}/{e.cap}" + (e.graspable ? "" : "  (KICK IT!)");
        }
    }
}
