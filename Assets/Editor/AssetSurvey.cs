using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MineEmbers.EditorTools
{
    /// <summary>Dumps bounds / pivot / triangle count / materials of every prefab under a folder, so the
    /// level generator can place modular pieces by measured size instead of guesswork.
    ///   tools/unity.sh MineEmbers.EditorTools.AssetSurvey.Run</summary>
    public static class AssetSurvey
    {
        public static void Run()
        {
            var sb = new StringBuilder("name\tsize\tcenter(local)\ttris\tmaterials\tcolliders\n");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LoafbrrAssets" }).OrderBy(AssetDatabase.GUIDToAssetPath))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Object.DestroyImmediate(inst); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                int tris = inst.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length / 3);
                var mats = string.Join(",", rs.SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name).Distinct());
                var cols = string.Join(",", inst.GetComponentsInChildren<Collider>().Select(c => c.GetType().Name).Distinct());
                sb.Append($"{path.Replace("Assets/LoafbrrAssets/MInesAndCaveSet/prefabs/", "")}\t{V(b.size)}\t{V(b.center)}\t{tris}\t{mats}\t{cols}\n");
                Object.DestroyImmediate(inst);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/LoafbrrAssets" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                sb.Append($"MAT {m.name}\tshader={m.shader.name}\tsupported={m.shader.isSupported}\n");
            }
            File.WriteAllText("Logs/asset_survey.tsv", sb.ToString());
            Debug.Log("[LevelBuilder] survey written: Logs/asset_survey.tsv");
        }
        static string V(Vector3 v) => $"{v.x:0.00},{v.y:0.00},{v.z:0.00}";
    }
}
