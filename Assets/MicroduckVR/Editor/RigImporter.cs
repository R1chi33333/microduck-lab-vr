using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MicroduckVR.Core;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Decodes Reference/web/microduck_rig.glb.txt (base64 GLB, Pollen Robotics Microduck, CC BY-NC-SA 4.0)
    /// into Assets/MicroduckVR/Models/microduck_rig.glb (imported by glTFast) and reads the hinge joints
    /// from the node extras into a DuckJointMap asset, converted to Unity space.
    /// </summary>
    public static class RigImporter
    {
        const string ModelDir = "Assets/MicroduckVR/Models";
        public const string GlbPath = ModelDir + "/microduck_rig.glb";
        public const string JointMapPath = ModelDir + "/DuckJointMap.asset";

        [MenuItem("Microduck/Import Duck Rig")]
        public static void Import()
        {
            string src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Reference", "web", "microduck_rig.glb.txt"));
            byte[] glb = Convert.FromBase64String(File.ReadAllText(src).Trim());
            Directory.CreateDirectory(ModelDir);
            File.WriteAllBytes(GlbPath, glb);

            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(JointMapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<DuckJointMap>();
                AssetDatabase.CreateAsset(map, JointMapPath);
            }
            map.joints = ReadJoints(glb, out map.trunkTranslation);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GlbPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Microduck] Rig imported: {glb.Length} bytes, {map.joints.Count} hinge joints.");
        }

        public static List<DuckJointMap.Joint> ReadJoints(byte[] glb, out Vector3 trunkTranslation)
        {
            trunkTranslation = Vector3.zero;
            // GLB: 12-byte header, then chunk 0 = JSON (length, type, data)
            int jsonLen = BitConverter.ToInt32(glb, 12);
            string json = Encoding.UTF8.GetString(glb, 20, jsonLen);
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var nodes = (List<object>)root["nodes"];
            var list = new List<DuckJointMap.Joint>();
            foreach (var o in nodes)
            {
                var n = (Dictionary<string, object>)o;
                if (n.TryGetValue("name", out var nm) && (string)nm == "body_trunk" && n.TryGetValue("translation", out var tr))
                    trunkTranslation = GltfSpace.Position(MiniJson.Doubles(tr));
                if (!n.TryGetValue("extras", out var exObj) || !(exObj is Dictionary<string, object> ex)) continue;
                if (!ex.TryGetValue("joint_type", out var jt) || (string)jt != "hinge" || !ex.ContainsKey("zero_quat")) continue;
                var range = MiniJson.Doubles(ex["range"]);
                list.Add(new DuckJointMap.Joint
                {
                    joint = (string)ex["joint"],
                    node = (string)n["name"],
                    role = ex.TryGetValue("role", out var r) ? (string)r : "",
                    axis = GltfSpace.Axis(MiniJson.Doubles(ex["axis"])).normalized,
                    zero = GltfSpace.Rotation(MiniJson.Doubles(ex["zero_quat"])),
                    min = (float)range[0],
                    max = (float)range[1],
                    stand = ex.TryGetValue("stand", out var s) ? Convert.ToSingle(s) : 0f,
                });
            }
            return list;
        }
    }
}
