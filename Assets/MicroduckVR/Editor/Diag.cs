using MicroduckVR.Player;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static class Diag
    {
        public static void MirrorSpot()
        {
            var pd = PlayerDuck.Instance; if (pd == null) return;
            pd.Teleport(new Vector3(4.0f, 0.05f, -1.85f), Mathf.PI);
            var p = new Vector3(4.0f, 0.05f, -1.85f);
            DebugCapture.Shoot("diag_side", p + new Vector3(1.0f, 0.5f, 0.3f), p + new Vector3(0f, 0.1f, -0.3f), 1280, 800, 60f);
            foreach (var c in Physics.OverlapSphere(p + new Vector3(0f, 0.25f, -0.15f), 0.2f)) Debug.Log("[Microduck] near: " + c.name + " @ " + c.transform.position.ToString("F2"));
        }
    }
}
