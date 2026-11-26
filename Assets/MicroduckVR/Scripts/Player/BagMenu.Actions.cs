using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.Player
{
    public partial class BagMenu
    {
        // the bottom rows: games, emotes, swapping duck, the view
        static readonly (string kind, string label, string col)[] Actions =
        {
            ("football", "FOOTBALL", "#FF8A3D"), ("race", "RACE", "#7B61FF"), ("swap", "SWAP DUCK", "#1FA6A0"), ("view", "VIEW", "#3A86FF"),
            ("dance", "DANCE", "#FF5D73"), ("nod", "NOD", "#FF5D73"), ("shake", "SHAKE", "#FF5D73"), ("look", "LOOK", "#FF5D73"),
            ("addduck", "ADD DUCK", "#FFC43D"), ("removeduck", "REMOVE DUCK", "#7B61FF"), ("mirror", "MY LOOK", "#FF8A3D"), ("tutorial", "TUTORIAL", "#8FD35A"),
            ("help", "HELP", "#FFC43D"), ("credits", "CREDITS", "#8FD35A"), ("host", "HOST", "#3A86FF"), ("join", "JOIN", "#1FA6A0"),
        };

        static bool IsAction(string k) { foreach (var a in Actions) if (a.kind == k) return true; return false; }

        /// <summary>Playing together (phase 1): the games and duck swapping stay off until they work for two.</summary>
        static bool CoopOnly(string kind) => MicroduckVR.Net.NetSession.InSession && (kind == "football" || kind == "race" || kind == "swap");

        void DoAction(string kind)
        {
            var pd = PlayerDuck.Instance;
            Close(); Play(clickClip);
            if (CoopOnly(kind)) { MicroduckVR.Games.GameHud.Get().Big("NOT IN CO-OP YET!", "#7B61FF", 1.3f); return; }
            switch (kind)
            {
                case "host": MicroduckVR.Net.NetSession.HostOrStop(); break;
                case "join": MicroduckVR.Net.NetSession.JoinOrLeave(); break;
                case "football": var fg = MicroduckVR.Games.FootballGame.Ensure(); if (MicroduckVR.Games.FootballGame.Active) fg.StopGame(); else fg.StartGame(); break;
                case "race": var rg = MicroduckVR.Games.RaceGame.Ensure(); if (MicroduckVR.Games.RaceGame.Active) rg.StopRace(); else rg.StartRace(); break;
                case "swap": if (pd != null) pd.SwapWithNearest(); break;
                case "view": if (pd != null) pd.SetThirdPerson(!pd.thirdPerson); break;
                case "help": TutorialCoach.ShowHelp(); break;
                case "addduck": if (!MicroduckVR.Net.NetSession.RequestAddDuck()) MicroduckVR.Flock.FlockSpawner.Add(); break;           // joined: the host adds it
                case "removeduck": if (!MicroduckVR.Net.NetSession.RequestRemoveDuck()) MicroduckVR.Flock.FlockSpawner.Remove(); break;
                case "mirror": if (pd != null) { pd.SetThirdPerson(false); pd.Teleport(MicroduckVR.Custom.MirrorStation.StandPos, Mathf.PI); pd.FaceView(Mathf.PI); if (MicroduckVR.Custom.MirrorStation.Instance != null) MicroduckVR.Custom.MirrorStation.Instance.OpenBoard(true); } break;
                case "tutorial": TutorialCoach.Restart(); break;
                case "credits": TutorialCoach.ShowSheet("CREDITS", TutorialCoach.CreditsText, "Y TO CLOSE"); break;
                default: if (pd != null) pd.Body.PlayEmote(kind); break;
            }
        }

        public void Choose(string kind)
        {
            if (IsAction(kind)) { DoAction(kind); return; }
            if (kind == "trash") { Selected = null; panel.gameObject.SetActive(false); SetMode(Mode.Trash); Play(clickClip); return; }
            Selected = kind; panel.gameObject.SetActive(false); SetMode(Mode.Place); Play(clickClip);
        }

        void NetLabels()
        {
            var st = MicroduckVR.Net.NetSession.State;
            string s = MicroduckVR.Net.NetSession.Status;
            netLine.text = string.IsNullOrEmpty(s) ? "PLAY TOGETHER: HOST OR JOIN" : s;
            if (actionLabels.TryGetValue("host", out var h)) h.text = st == MicroduckVR.Net.NetSession.Mode.Hosting ? "STOP HOST" : "HOST";
            if (actionLabels.TryGetValue("join", out var j)) j.text = st == MicroduckVR.Net.NetSession.Mode.Joined ? "LEAVE" : st == MicroduckVR.Net.NetSession.Mode.Searching || st == MicroduckVR.Net.NetSession.Mode.Connecting ? "CANCEL" : "JOIN";
        }

        string Footer(string kind)
        {
            if (CoopOnly(kind)) return "NOT IN CO-OP YET (COMING LATER)";
            if (kind == "trash") return "BIN: REMOVE TOYS";
            if (kind == "football") return MicroduckVR.Games.FootballGame.Active ? "STOP THE MATCH" : "FOOTBALL ON THE RUG";
            if (kind == "race") return MicroduckVR.Games.RaceGame.Active ? "STOP THE RACE" : "RACE ROUND THE HOUSE";
            if (kind == "swap") return "BECOME THE DUCK IN FRONT";
            if (kind == "view") return "FIRST / THIRD PERSON";
            if (kind == "help") return "HOW TO PLAY";
            if (kind == "addduck") return $"ANOTHER DUCK JOINS  ({MicroduckVR.Flock.DuckBrain.All.Count}/{MicroduckVR.Flock.FlockSpawner.Max})";
            if (kind == "removeduck") return "SEND ONE DUCK HOME";
            if (kind == "mirror") return "GO TO THE MIRROR: CHANGE YOUR DUCK";
            if (kind == "tutorial") return "PLAY THE TUTORIAL AGAIN";
            if (kind == "credits") return "WHO MADE THIS";
            if (kind == "host") return MicroduckVR.Net.NetSession.IsHost ? "STOP HOSTING" : "LET A FRIEND JOIN YOUR HOUSE";
            if (kind == "join") return MicroduckVR.Net.NetSession.State == MicroduckVR.Net.NetSession.Mode.Off ? "JOIN A FRIEND'S HOUSE" : "LEAVE";
            if (IsAction(kind)) return kind.ToUpperInvariant() + "!";
            var e = ToyCatalog.Get(kind);
            return $"{e.label.ToUpperInvariant()}  {Count(kind)}/{e.cap}" + (e.graspable ? "" : "  (KICK IT!)");
        }
    }
}
