using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of the toy bag: open, hover and pick the rubber duck, see the ghost, drop it,
    /// then switch to the bin and remove it again. Shots in Logs/shots, counts in the log.</summary>
    public static class BagTest
    {
        static double t0; static int step; static BagMenu bag; static Vector3 spot;

        [MenuItem("Microduck/Debug/Bag Test")]
        public static void Run()
        {
            if (!Application.isPlaying || PlayerDuck.Instance == null) { Debug.Log("[Microduck] BagTest needs Play mode"); return; }
            bag = PlayerDuck.Instance.GetComponent<BagMenu>();
            PlayerDuck.Instance.Teleport(new Vector3(-0.2f, 0.05f, 1.5f), Mathf.PI);
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static int Toys() => Object.FindObjectsByType<ToyTag>().Length;

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var cam = bag.cam.transform;
            if (step == 0 && t > 0.5) { step = 1; bag.Open(); }
            else if (step == 1 && t > 0.9)
            {
                step = 2; var tile = bag.TileWorld("toy_duck");
                bag.SetDebugAim(new Ray(cam.position + cam.right * 0.03f - cam.up * 0.03f, (tile - (cam.position + cam.right * 0.03f - cam.up * 0.03f)).normalized));
            }
            else if (step == 2 && t > 1.3)
            {
                step = 3;
                DebugCapture.Shoot("bag_panel", cam.position, cam.position + cam.forward, 1280, 960, 90f, bag.cam.cullingMask);
                bag.DebugPress();
            }
            else if (step == 3 && t > 1.7)
            {
                step = 4;
                var f = cam.forward; f.y = 0f; f.Normalize();
                spot = new Vector3(cam.position.x, 0f, cam.position.z) + f * 0.35f;
                var o = cam.position + cam.right * 0.03f - cam.up * 0.03f;
                bag.SetDebugAim(new Ray(o, (spot - o).normalized));
            }
            else if (step == 4 && t > 2.2)
            {
                step = 5;
                DebugCapture.Shoot("bag_ghost", cam.position, cam.position + cam.forward * 0.5f + Vector3.down * 0.3f, 1280, 960, 90f, bag.cam.cullingMask);
                Debug.Log($"[Microduck] bag: state={bag.State} selected={bag.Selected} toys before={Toys()}");
                bag.DebugPress();
            }
            else if (step == 5 && t > 2.8)
            {
                step = 6;
                DebugCapture.Shoot("bag_placed", spot + new Vector3(0.25f, 0.15f, 0.1f), spot, 1280, 800, 50f);
                Debug.Log($"[Microduck] bag: placed, toys now={Toys()}");
                bag.Choose("trash");
            }
            else if (step == 6 && t > 3.2) { step = 7; bag.DebugPress(); }
            else if (step == 7 && t > 3.8)
            {
                step = 8; Debug.Log($"[Microduck] bag: after bin, toys={Toys()} state={bag.State}");
                bag.SetDebugAim(null); bag.Close(); EditorApplication.update -= Tick;
            }
        }
    }
}
