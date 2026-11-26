using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MicroduckVR.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ------------------------------------------------------------------ identity of an item
        /// <summary>
        /// What an item is made of, relative to its pivot: parts, their placement (1 mm), meshes (vertex and index
        /// counts, size to 5 mm), materials, colliders, physics and gameplay values. Items with the same signature
        /// share one prefab; the hand-drawn wobble of each box does not count.
        /// </summary>
        static string Signature(Transform root)
        {
            var sb = new StringBuilder();
            var inv = root.worldToLocalMatrix;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                sb.Append(t == root ? "." : PathOf(t, root)).Append('|');
                var m = inv * t.localToWorldMatrix;
                for (int col = 0; col < 4; col++)
                    for (int row = 0; row < 3; row++) sb.Append(Rn(m[row, col], col == 3 ? 0.001f : 0.002f)).Append(',');
                sb.Append('L').Append(t.gameObject.layer).Append('S').Append((int)GameObjectUtility.GetStaticEditorFlags(t.gameObject)).Append(t.gameObject.activeSelf ? 'A' : 'I').Append('|');
                foreach (var comp in t.GetComponents<Component>())
                {
                    if (comp == null) { sb.Append("missing;"); continue; }
                    if (comp is Transform) continue;
                    sb.Append(comp.GetType().Name).Append(':');
                    switch (comp)
                    {
                        case MeshFilter f: sb.Append(MeshKey(f.sharedMesh)); break;
                        case MeshRenderer r:
                            sb.Append(r.enabled).Append(r.shadowCastingMode).Append(r.receiveShadows);
                            foreach (var mat in r.sharedMaterials) sb.Append('+').Append(mat != null ? mat.name : "null");
                            break;
                        case BoxCollider bc: sb.Append(V(bc.center)).Append(V(bc.size)).Append(bc.isTrigger).Append(PhysName(bc)); break;
                        case SphereCollider sc: sb.Append(V(sc.center)).Append(Rn(sc.radius, 0.001f)).Append(sc.isTrigger).Append(PhysName(sc)); break;
                        case CapsuleCollider cc: sb.Append(V(cc.center)).Append(Rn(cc.radius, 0.001f)).Append(Rn(cc.height, 0.001f)).Append(cc.direction).Append(PhysName(cc)); break;
                        case MeshCollider mc: sb.Append(mc.convex).Append(MeshKey(mc.sharedMesh)).Append(PhysName(mc)); break;
                        case Rigidbody rb:
                            sb.Append(Rn(rb.mass, 0.0005f)).Append(rb.isKinematic).Append(rb.useGravity).Append(Rn(rb.linearDamping, 0.01f)).Append(Rn(rb.angularDamping, 0.01f))
                              .Append(rb.interpolation).Append(rb.collisionDetectionMode);
                            break;
                        case Grabbable g: sb.Append(g.label).Append(Rn(g.gripWidth, 0.0005f)); break;
                        case SurfaceTag st: sb.Append(st.type).Append('/').Append(st.room); break;
                    }
                    sb.Append(';');
                }
                sb.Append('\n');
            }
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
        static string Rn(float v, float step) => Mathf.RoundToInt(v / step).ToString();
        static string V(Vector3 v) => $"({Rn(v.x, 0.001f)},{Rn(v.y, 0.001f)},{Rn(v.z, 0.001f)})";
        static string PhysName(Collider c) => c.sharedMaterial != null ? c.sharedMaterial.name : "-";
        static string MeshKey(Mesh m)
        {
            if (m == null) return "none";
            if (IsBuiltin(m)) return "builtin:" + m.name;
            long idx = 0; for (int i = 0; i < m.subMeshCount; i++) idx += m.GetIndexCount(i);
            var s = m.bounds.size; var c = m.bounds.center;
            return $"{m.vertexCount}/{m.subMeshCount}/{idx}/{Rn(s.x, 0.005f)},{Rn(s.y, 0.005f)},{Rn(s.z, 0.005f)}/{Rn(c.x, 0.005f)},{Rn(c.y, 0.005f)},{Rn(c.z, 0.005f)}";
        }

        static bool IsBuiltin(Object o)
        {
            string p = AssetDatabase.GetAssetPath(o);
            return p.StartsWith("Library/") || p.StartsWith("Resources/unity_builtin_extra");
        }

        // ------------------------------------------------------------------ saving
        /// <summary>Saves materials and meshes the item still has only in memory, then the prefab.</summary>
        static void Save(GameObject go, string category, string file, string meshAsset, Dictionary<Mesh, Mesh> sharedMeshes)
        {
            EnsureFolder($"{Root}/{category}");
            PersistMaterials(go, category);
            PersistMeshes(go, $"{MeshRoot}/{category}/{meshAsset}.asset", file, sharedMeshes);
            string path = $"{Root}/{category}/{file}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
            if (!ok) { Warn("could not save " + path); return; }
            written.Add(path);
            nPrefabs++;
            perCategory[category] = (perCategory.TryGetValue(category, out var k) ? k : 0) + 1;
        }

        static void PersistMeshes(GameObject go, string meshPath, string file, Dictionary<Mesh, Mesh> shared)
        {
            var uses = new List<(Mesh mesh, string owner)>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) uses.Add((mf.sharedMesh, mf.name));
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) uses.Add((mc.sharedMesh, mc.name + "_collider"));
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) uses.Add((smr.sharedMesh, smr.name));

            var map = new Dictionary<Mesh, Mesh>();
            var create = new List<Mesh>();
            var names = new HashSet<string>();
            foreach (var (mesh, owner) in uses)
            {
                if (mesh == null || map.ContainsKey(mesh)) continue;
                if (IsBuiltin(mesh)) { map[mesh] = mesh; continue; }               // Unity's own cube and sphere are in every project
                if (shared != null && shared.TryGetValue(mesh, out var have)) { map[mesh] = have; continue; }
                var copy = Object.Instantiate(mesh);
                string n = Sanitize(file + "_" + owner); string unique = n;
                for (int i = 2; !names.Add(unique); i++) unique = n + "_" + i;
                copy.name = unique;
                create.Add(copy); map[mesh] = copy;
                if (shared != null) shared[mesh] = copy;
            }
            if (create.Count > 0)
            {
                if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(create[0], meshPath);
                for (int i = 1; i < create.Count; i++) AssetDatabase.AddObjectToAsset(create[i], create[0]);
                AssetDatabase.SaveAssets();
                nMeshes += create.Count;
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null && map.TryGetValue(mf.sharedMesh, out var m)) mf.sharedMesh = m;
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) if (mc.sharedMesh != null && map.TryGetValue(mc.sharedMesh, out var m)) mc.sharedMesh = m;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr.sharedMesh != null && map.TryGetValue(smr.sharedMesh, out var m)) smr.sharedMesh = m;
        }

        /// <summary>Materials made at runtime (toys, ducks) and physics materials made in code get saved as assets; saved ones are kept as they are.</summary>
        static void PersistMaterials(GameObject go, string category)
        {
            string dir = $"{MatRoot}/{category}";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || AssetDatabase.Contains(m)) continue;
                    if (!savedMaterials.TryGetValue(m, out var saved)) { saved = SaveCopy(m, dir, ".mat"); savedMaterials[m] = saved; }
                    mats[i] = (Material)saved; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                var pm = col.sharedMaterial;
                if (pm == null || AssetDatabase.Contains(pm)) continue;
                if (!savedMaterials.TryGetValue(pm, out var saved)) { saved = SaveCopy(pm, dir, ".physicMaterial"); savedMaterials[pm] = saved; }
                col.sharedMaterial = (PhysicsMaterial)saved;
            }
        }

        /// <summary>A copy (so the game's runtime caches never turn into assets), written over an earlier export of the same name.</summary>
        static Object SaveCopy(Object src, string dir, string ext)
        {
            EnsureFolder(dir);
            string n = Sanitize(src.name.StartsWith("Toon ") ? src.name.Substring(5) : src.name);
            if (string.IsNullOrEmpty(n)) n = "material";
            string path = $"{dir}/{n}{ext}";
            for (int i = 2; usedMaterialPaths.Contains(path); i++) path = $"{dir}/{n}_{i}{ext}";
            usedMaterialPaths.Add(path);
            var copy = Object.Instantiate(src);
            copy.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && existing.GetType() == copy.GetType())
            {
                EditorUtility.CopySerialized(copy, existing);
                existing.name = copy.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(copy);
                nMaterials++;
                return existing;
            }
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            nMaterials++;
            return copy;
        }
    }
}
