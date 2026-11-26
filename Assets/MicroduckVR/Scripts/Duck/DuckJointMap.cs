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
        /// <summary>One hinge joint of the rig.</summary>
        [Serializable]
        public class Joint
        {
            /// <summary>Joint name, e.g. "left_knee".</summary>
            public string joint;
            /// <summary>GameObject name, e.g. "body_leg".</summary>
            public string node;
            /// <summary>The joint's role from the GLB extras.</summary>
            public string role;
            /// <summary>Hinge axis in the node's zero frame (Unity space, normalised).</summary>
            public Vector3 axis;
            /// <summary>Node local rotation at theta = 0 (Unity space).</summary>
            public Quaternion zero;
            /// <summary>Limits in radians.</summary>
            public float min, max;
            /// <summary>Radians, the standing pose.</summary>
            public float stand;
        }

        /// <summary>Every joint of the rig.</summary>
        public List<Joint> joints = new List<Joint>();
        /// <summary>Rest position of body_trunk in the rig's root frame (Unity space). glTFast folds the
        /// single root node into the prefab root, so the body code reads it from here.</summary>
        public Vector3 trunkTranslation;

        /// <summary>The joint with this name, or null.</summary>
        public Joint Find(string jointName)
        {
            foreach (var j in joints) if (j.joint == jointName) return j;
            return null;
        }
    }
}
