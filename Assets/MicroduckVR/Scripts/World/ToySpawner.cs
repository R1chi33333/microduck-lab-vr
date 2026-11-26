using System;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>Puts the house's starting toys out when the scene starts (toys are built at runtime from the catalogue).</summary>
    public class ToySpawner : MonoBehaviour
    {
        /// <summary>The toys to put out.</summary>
        [Serializable] public struct Item { public string kind; public Vector3 pos; public float yaw; }
        public Item[] items = Array.Empty<Item>();

        void Awake()
        {
            foreach (var it in items)
            {
                var go = ToyCatalog.Spawn(it.kind, it.pos, it.yaw);
                if (go != null) go.transform.SetParent(transform, true);
            }
        }
    }
}
