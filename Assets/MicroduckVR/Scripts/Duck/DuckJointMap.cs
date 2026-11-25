using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.Duck
{
    /// <summary>
    /// Hinge joints of the Microduck rig, read from the GLB node extras by the rig importer
    /// (Microduck/Import Duck Rig). Values are already converted to Unity space.
    /// </summary>
    [CreateAssetMenu(menuName = "Microduck/Duck Joint Map")]
    public class DuckJointMap : ScriptableObject
    {
        [Serializable]
        public class Joint
        {
            public string joint;       // e.g. "left_knee"
            public string node;        // GameObject name, e.g. "body_leg"
            public string role;
            public Vector3 axis;       // hinge axis in the node's zero frame (Unity space, normalised)
            public Quaternion zero;    // node local rotation at theta = 0 (Unity space)
            public float min, max;     // radians
            public float stand;        // radians, the standing pose
        }

        public List<Joint> joints = new List<Joint>();

        public Joint Find(string jointName)
        {
            foreach (var j in joints) if (j.joint == jointName) return j;
            return null;
        }
    }
}
