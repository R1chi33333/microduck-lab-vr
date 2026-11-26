using MicroduckVR.Custom;
using MicroduckVR.Duck;
using MicroduckVR.Games;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of everything the player has to read: each board, card and HUD is photographed from the
    /// player's own eyes at the headset's pixel density (20 px per degree), in first and third person, so text that is
    /// too small or too far shows up as it would in the headset.</summary>
    public static class UiAudit
    {
        static double t0; static int step;
        const int W = 1800, H = 1600; const float FOV = 80f;        // 80 degrees over 1600 px = 20 px per degree

        [MenuItem("Microduck/Debug/UI Audit")]
        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            TutorialCoach.HideAll(); pd.SetThirdPerson(false);
            pd.Teleport(MirrorStation.StandPos, Mathf.PI);
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        /// <summary>From the player's eyes, turned by some degrees from straight ahead.</summary>
        static void Eye(string n, float yawOff = 0f, float pitch = 0f)
        {
            var c = PlayerDuck.Instance.cam.transform;
            var f = c.forward; f.y = 0f; f.Normalize();
            var d = Quaternion.Euler(0f, yawOff, 0f) * (Quaternion.AngleAxis(pitch, Vector3.Cross(Vector3.up, f)) * f);
            DebugCapture.Shoot("ui_" + n, c.position, c.position + d, W, H, FOV, PlayerDuck.Instance.cam.cullingMask);
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var bag = pd.GetComponent<BagMenu>();
            switch (step)
            {
                case 0 when t > 1.5: step++; Eye("mirror_fwd"); Eye("mirror_board", 38f); pd.SetThirdPerson(true); break;
                case 1 when t > 3.0: step++; Eye("mirror_3p_fwd", 0f, 15f); Eye("mirror_3p_board", 38f, 10f); pd.SetThirdPerson(false); TutorialCoach.Restart(); break;
                case 2 when t > 4.5: step++; Eye("tut_intro", -36f, 8f); break;
                case 3 when t > 9.5: step++; Eye("tut_mirror", 0f, 8f); Eye("tut_mirror_left", -36f, 8f); Eye("tut_mirror_board", 38f, 5f); pd.Teleport(new Vector3(-1.0f, 0.05f, 1.2f), Mathf.PI); TutorialCoach.DebugGo(2); break;
                case 4 when t > 10.7: step++; Eye("tut_waddle", 0f, 10f); TutorialCoach.DebugGo(5); break;
                case 5 when t > 11.7: step++; Eye("tut_grab", 0f, 12f); TutorialCoach.DebugGo(15); break;
                case 6 when t > 12.12: step++; Eye("tut_drift", 0f, 10f); pd.SetThirdPerson(true); TutorialCoach.DebugGo(19); break;
                case 7 when t > 14.0: step++; Eye("tut_3p", 0f, 12f); pd.SetThirdPerson(false); pd.Teleport(new Vector3(-1.0f, 0.05f, -2.22f), Mathf.PI); TutorialCoach.DebugGo(17); break;
                case 8 when t > 15.2: step++; Eye("tut_wall", 0f, 10f); pd.Teleport(new Vector3(-1.0f, 0.05f, 1.2f), Mathf.PI); TutorialCoach.DebugGo(8); break;
                case 9 when t > 16.0: step++; bag.Open(); break;
                case 10 when t > 17.3: step++; Eye("tut_bag", 0f, 5f); Eye("tut_bag_left", -40f, 5f); TutorialCoach.HideAll(); break;
                case 11 when t > 17.9: step++; Eye("bag", 0f, 5f); bag.Close(); pd.SetThirdPerson(true); break;
                case 12 when t > 19.0: step++; bag.Open(); break;
                case 13 when t > 19.8: step++; Eye("bag_3p", 0f, 8f); pd.SetThirdPerson(false); bag.Choose("help"); break;
                case 14 when t > 20.8: step++; Eye("help", 0f, 5f); bag.Choose("credits"); break;
                case 15 when t > 21.6: step++; Eye("credits", 0f, 5f); TutorialCoach.HideAll(); bag.Close(); pd.Teleport(new Vector3(-2.2f, 0f, 1.78f), Mathf.PI); break;
                case 16 when t > 23.6: step++; Eye("garage_fwd", 0f, 15f); Eye("garage_board", 42f, 8f); pd.Teleport(new Vector3(2.4f, 0f, 3.22f), -Mathf.PI / 2f); pd.SetThirdPerson(false); break;
                case 17 when t > 24.6: step++; RaceGame.Ensure().StartRace(); break;
                case 18 when t > 25.4: step++; Eye("race_a", 0f, -8f); break;
                case 19 when t > 29.2: step++; Eye("race_b", 0f, -8f); RaceGame.Ensure().StopRace(); pd.Teleport(new Vector3(1.93f, 0f, 1.3f), Mathf.PI); FootballGame.Ensure().StartGame(); break;
                case 20 when t > 29.7: step++; Eye("football", 0f, -6f); break;
                case 21 when t > 31.2: step++; GameHud.Get().Big("GOAL!", "#FFC43D", 1.5f); break;
                case 22 when t > 31.8: step++; Eye("big", 0f, -6f); FootballGame.Ensure().StopGame(); pd.Teleport(new Vector3(4.3f, 0.05f, -0.2f), Mathf.PI); break;
                case 23 when t > 33.0: step++; Eye("sign_mirror", 0f, -12f); pd.Teleport(new Vector3(-2.2f, 0f, 2.75f), Mathf.PI); break;
                case 24 when t > 34.5: step++; Eye("sign_garage", 0f, -10f); pd.Teleport(new Vector3(-0.6f, 0.05f, 1.6f), -Mathf.PI / 2f); break;
                case 25 when t > 36.0: step++; Eye("sign_garage_room", 0f, -8f); pd.Teleport(new Vector3(1.3f, 0f, 5.7f), 0f); pd.Body.PlayEmote(EmoteKind.Dance); MicroduckVR.Comic.ComicPop.Spawn("POW!", new Vector3(1.3f, 0.2f, 8.2f), "#FF8A3D", 1.2f * 2.5f); break;
                case 26 when t > 36.6: step++; Eye("pop_far", 0f, -5f); pd.Teleport(new Vector3(0.6f, 0.05f, 1.2f), Mathf.PI);
                    EditorApplication.update -= Tick; Debug.Log("[Microduck] ui audit done"); break;
            }
        }
    }
}
