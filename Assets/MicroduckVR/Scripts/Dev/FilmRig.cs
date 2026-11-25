using System;
using UnityEngine;

namespace MicroduckVR.Dev
{
    /// <summary>Calls back at the very end of every frame; the editor's film director hangs its camera work on this.</summary>
    [DefaultExecutionOrder(32000)]
    public class FilmRig : MonoBehaviour
    {
        public Action OnFrame;
        void LateUpdate() { OnFrame?.Invoke(); }
    }
}
