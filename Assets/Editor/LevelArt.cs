using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MineEmbers.EditorTools
{
    /// <summary>
    /// Art pass for the generated level: dresses the greybox volumes with the modular "Mines and Cave Set"
    /// (Loafbrr, CC0). The greybox boxes stay as invisible colliders, so walking, camera paths, perf zones
    /// and shot points are unchanged; only what is drawn changes. Pieces are placed by their *measured*
    /// bounds (see AssetSurvey) and their facing is derived from mesh normals, not hard-coded per prefab.
    /// Build with -greybox to regenerate the old primitive-only level for A/B perf comparison.
    /// </summary>
    public static partial class LevelBuilder
    {
        const string KitDir = "Assets/LoafbrrAssets/MInesAndCaveSet/prefabs";
        static bool _art;
        static System.Random _artRng;   // separate stream: greybox randomness (and so its layout) is unchanged
        static int _pieces;
        static readonly Dictionary<string, GameObject> Kit = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Vector3> KitFront = new Dictionary<string, Vector3>();

        static void InitArt()
        {
            _art = !System.Environment.GetCommandLineArgs().Contains("-greybox");
            _artRng = new System.Random(20271122);
            _pieces = 0;
            Kit.Clear(); KitFront.Clear();
            if (!_art) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { KitDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Kit[System.IO.Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            if (Kit.Count == 0) throw new System.Exception($"[LevelBuilder] art kit not found under {KitDir}; import it or build with -greybox");
            // Greybox props that survive the art pass (crates, torch sticks) borrow the kit's trim material.
            var trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/LoafbrrAssets/MInesAndCaveSet/Materials/A_MineTrim.mat");
            if (trim) Mats["Wood"] = trim;
        }

        static string Pick(params string[] keys) => keys[_artRng.Next(keys.Length)];
        static float AR(float a, float b) => a + (float)_artRng.NextDouble() * (b - a);

        /// <summary>Removes the visible part of a greybox box; its collider keeps doing the gameplay work.</summary>
        static void Hide(GameObject box)
        {
            if (!_art) return;
            Object.DestroyImmediate(box.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(box.GetComponent<MeshFilter>());
        }

        /// <summary>Instantiates a kit prefab, unpacked, without colliders (greybox owns collision), static.</summary>
        static GameObject Piece(Transform parent, string key, Quaternion rot, Vector3 scale, bool castShadows = true)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Kit[key], parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.Scale(go.transform.localScale, scale);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var t in go.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            _pieces++;
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Moves go so that the point (center + anchor * extents) of its world bounds lands on target.</summary>
        static void Snap(GameObject go, Vector3 target, Vector3 anchor)
        {
            var b = WorldBounds(go);
            var p = b.center + Vector3.Scale(anchor, b.extents);
            go.transform.position += target - p;
        }

        /// <summary>Which horizontal direction a wall-like prefab faces: area-weighted mean of its triangle
        /// normals, snapped to the nearest axis. Cached per prefab.</summary>
        static Vector3 Front(string key)
        {
            if (KitFront.TryGetValue(key, out var f)) return f;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(Kit[key]);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var sum = Vector3.zero;
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (!m) continue;
                var v = m.vertices; var t = m.triangles; var w = mf.transform.localToWorldMatrix;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = w.MultiplyPoint3x4(v[t[i]]), b = w.MultiplyPoint3x4(v[t[i + 1]]), c = w.MultiplyPoint3x4(v[t[i + 2]]);
                    sum += Vector3.Cross(b - a, c - a);   // length = 2 * area
                }
            }
            Object.DestroyImmediate(inst);
            sum.y = 0;
            f = Mathf.Abs(sum.x) > Mathf.Abs(sum.z) ? new Vector3(Mathf.Sign(sum.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(sum.z));
            KitFront[key] = f;
            Debug.Log($"[LevelBuilder] front({key}) = {f} (raw {sum})");
            return f;
        }

        // Yaw only: FromToRotation picks an arbitrary axis for opposite vectors and could flip a wall upside down.
        static Quaternion Facing(string key, Vector3 inward) => Quaternion.Euler(0, Vector3.SignedAngle(Front(key), inward, Vector3.up), 0);

        // ------------------------------------------------------------------ surfaces

        /// <summary>Lines a straight wall from a to b (y = 0) with stacked rows of pieces facing `inward`.
        /// The pieces' front face sits `protrude` metres in front of the wall line. Leaves a gap for a doorway.</summary>
        static void WallRun(Transform parent, string[] keys, Vector3 a, Vector3 b, Vector3 inward, float h,
                            float protrude, float rowStep, float doorWidth = 0, float doorHeight = 0)
        {
            var g = Group(parent, "ArtWall");
            Vector3 dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            float pieceW = keys.Min(k => WidthAlong(k));
            int n = Mathf.Max(1, Mathf.CeilToInt(len / (pieceW * 0.92f)));
            float step = len / n;
            Vector3 mid = (a + b) / 2;
            for (float y = 0; y < h - 1f; y += rowStep)
            for (int i = 0; i < n; i++)
            {
                Vector3 s = a + dir * (step * (i + 0.5f));
                if (doorWidth > 0 && y < doorHeight - 1f && Vector3.Distance(s, mid) < doorWidth / 2 + step * 0.45f) continue;
                var key = Pick(keys);
                var go = Piece(g, key, Facing(key, inward), Vector3.one);
                // Anchor: bottom, centred along the wall, front face on (wall line + protrude). Alternate a
                // centimetre of depth so overlapping neighbours never share a plane (no z-fighting).
                Vector3 target = s + inward * (protrude + (i % 2) * 0.02f) + Vector3.up * y;
                Snap(go, target, new Vector3(inward.x, -1, inward.z));
            }
        }

        static float WidthAlong(string key)
        {
            var inst = Piece(null, key, Facing(key, Vector3.forward), Vector3.one);
            float w = WorldBounds(inst).size.x;
            Object.DestroyImmediate(inst); _pieces--;
            return w;
        }

        /// <summary>Tiles a rectangle with floor pieces (top surface at y) or, with ceiling=true, flipped pieces
        /// hanging from y. Neither casts shadows: nothing sits below a floor or above a ceiling.</summary>
        static void Tiles(Transform parent, string[] keys, float x0, float x1, float z0, float z1, float y, bool ceiling, float stepX, float stepZ, float sink = 0)
        {
            var g = Group(parent, ceiling ? "ArtCeiling" : "ArtFloor");
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / stepX)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / stepZ));
            float sx = (x1 - x0) / nx, sz = (z1 - z0) / nz;
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var key = Pick(keys);
                var rot = Quaternion.Euler(0, _artRng.Next(2) * 180, ceiling ? 180 : 0);
                var go = Piece(g, key, rot, Vector3.one, castShadows: false);
                float jitter = ((ix + iz) % 2) * 0.01f;
                var c = new Vector3(x0 + sx * (ix + 0.5f), 0, z0 + sz * (iz + 0.5f));
                if (ceiling) Snap(go, c + Vector3.up * (y - sink - jitter), new Vector3(0, -1, 0));
                else Snap(go, c + Vector3.up * (y + sink + jitter), new Vector3(0, 1, 0));
            }
        }

        // ------------------------------------------------------------------ room styles

        static readonly string[] MineWalls = { "Wall_Mines_A", "Wall_Mines_B" };
        static readonly string[] CaveWalls = { "Wall_Caves_A", "Wall_Caves_B" };
        static readonly string[] CaveFloor = { "Ground_Cave_A", "Ground_Cave_B" };
        static readonly string[] CaveLedge = { "Cave_Wall_Rocks_E", "Cave_Wall_Rocks_G", "Cave_Wall_Rocks_D" };
        static readonly string[] Boulders = { "Cave_Rocks_A", "Cave_Rocks_B", "Cave_Rocks_C", "Cave_Rocks_D" };

        /// <summary>Timbered mine tunnel along +z: plank-lined walls, plank ceiling, rail-friendly flat floor.</summary>
        static void DressTunnel(Transform parent, float x0, float x1, float z0, float z1, float h, bool closedSouth)
        {
            if (closedSouth) WallRun(parent, MineWalls, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.forward, h, 0.1f, 4.4f);
            WallRun(parent, MineWalls, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), Vector3.right, h, 0.1f, 4.4f);
            WallRun(parent, MineWalls, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.left, h, 0.1f, 4.4f);
            Tiles(parent, new[] { "Ground_Mines_A" }, x0, x1, z0, z1, 0.02f, false, 4.1f, 4.1f);
            // Plank lagging above the timber frames (beams top out at 4.75 m).
            var g = Group(parent, "ArtLagging");
            for (float z = z0 + 2; z < z1; z += 4)
            {
                var p = Piece(g, "Panel_Wood_A", Quaternion.Euler(90, 0, 0), new Vector3((x1 - x0) / 4f, 1, 1), castShadows: false);
                Snap(p, new Vector3((x0 + x1) / 2, 4.78f, z), new Vector3(0, -1, 0));
            }
        }

        /// <summary>Natural cave: stacked rock walls (with a doorway where the greybox has one), rock ceiling.</summary>
        static void DressCave(Transform parent, float x0, float x1, float z0, float z1, float h, bool doorSouth, bool doorNorth)
        {
            const float rowStep = 4.4f, protrude = 0.2f, door = 6f, doorH = 5f;
            WallRun(parent, CaveWalls, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), Vector3.right, h, protrude, rowStep);
            WallRun(parent, CaveWalls, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.left, h, protrude, rowStep);
            WallRun(parent, CaveWalls, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.forward, h, protrude, rowStep, doorSouth ? door : 0, doorH);
            WallRun(parent, CaveWalls, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), Vector3.back, h, protrude, rowStep, doorNorth ? door : 0, doorH);
            Tiles(parent, CaveFloor, x0, x1, z0, z1, h, true, 4.4f, 4.8f, sink: 0.2f);
        }

        static void CaveFloorRect(Transform parent, float x0, float x1, float z0, float z1)
            => Tiles(parent, CaveFloor, x0, x1, z0, z1, 0.15f, false, 4.4f, 4.8f);

        /// <summary>Rock ledge along a drop (lake shore, lava channel): pieces face `inward`, tops just above y = 0.</summary>
        static void Ledge(Transform parent, Vector3 a, Vector3 b, Vector3 inward, float depth)
        {
            var g = Group(parent, "ArtLedge");
            Vector3 dir = (b - a).normalized;
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 2.2f));
            for (int i = 0; i < n; i++)
            {
                var key = Pick(CaveLedge);
                var go = Piece(g, key, Facing(key, inward), new Vector3(1, depth / 2f, 1));
                Vector3 s = a + dir * (len * (i + 0.5f) / n);
                Snap(go, s + Vector3.up * 0.15f + inward * (0.15f + (i % 2) * 0.02f), new Vector3(inward.x, 1, inward.z));
            }
        }

        static void Boulder(Transform parent, Vector3 floorPos, float size)
        {
            var go = Piece(parent, Pick(Boulders), Quaternion.Euler(0, AR(0, 360), 0), Vector3.one * size);
            Snap(go, floorPos + Vector3.down * 0.1f, new Vector3(0, -1, 0));
        }

        /// <summary>Stalagmite (up) or stalactite (hanging): a cave pillar scaled to the requested height.</summary>
        static void ArtSpike(Transform parent, Vector3 basePos, float height, bool up)
        {
            string key = up ? "Cave_Pillar_A" : "Cave_Pillar_B";
            var rot = Quaternion.Euler(up ? 0 : 180, AR(0, 360), 0);
            float s = height / 5.3f;
            var go = Piece(parent, key, rot, new Vector3(Mathf.Max(0.45f, s), s, Mathf.Max(0.45f, s)));
            Snap(go, basePos + (up ? Vector3.down : Vector3.up) * 0.2f, new Vector3(0, up ? -1 : 1, 0));
        }

        static void TimberFrame(Transform parent, float z, float halfWidth, float beamY)
        {
            var g = Group(parent, "ArtFrame");
            foreach (float sx in new[] { -1f, 1f })
            {
                var post = Piece(g, "Post_Reinforced_A", Quaternion.identity, new Vector3(1.75f, beamY / 3.8f, 1.75f));
                Snap(post, new Vector3(sx * halfWidth, 0, z), new Vector3(0, -1, 0));
            }
            var beam = Piece(g, "Beam_A", Quaternion.identity, new Vector3(halfWidth * 2.15f / 4f, 2f, 2f));
            Snap(beam, new Vector3(0, beamY, z), new Vector3(0, -1, 0));
        }

        static void RailTrack(Transform parent, float z0, float z1)
        {
            var g = Group(parent, "ArtRails");
            for (float z = z0 + 2; z <= z1 - 2 + 0.01f; z += 4)
            {
                var r = Piece(g, "Rail_A", Quaternion.Euler(0, 90, 0), Vector3.one, castShadows: false);
                Snap(r, new Vector3(0, 0.02f, z), new Vector3(0, -1, 0));
            }
        }

        static void ArtMinecart(Transform parent, Vector3 pos, string key)
        {
            var c = Piece(parent, key, Quaternion.Euler(0, 90, 0), Vector3.one * 1.15f);
            Snap(c, pos + Vector3.up * 0.14f, new Vector3(0, -1, 0));
        }

        static void WoodBridge(Transform parent, float zc, float length)
        {
            var g = Group(parent, "ArtBridge");
            int n = Mathf.CeilToInt(length / 4f);
            for (int i = 0; i < n; i++)
            {
                var p = Piece(g, "Wood_Platform_A", Quaternion.identity, new Vector3(4.5f / 4.27f, 1, 1));
                Snap(p, new Vector3(0, 0.02f, zc - length / 2 + length * (i + 0.5f) / n), new Vector3(0, 1, 0));
            }
            foreach (float sx in new[] { -1.9f, 1.9f })
            foreach (float dz in new[] { -length / 2 + 0.3f, length / 2 - 0.3f })
            {
                var post = Piece(g, "Wood_Platform_Post_D", Quaternion.identity, Vector3.one);
                Snap(post, new Vector3(sx, 0, zc + dz), new Vector3(0, 1, 0));
            }
        }

        const string ArmatureDir = "Assets/Starter Assets/Runtime/ThirdPersonController/Character";

        /// <summary>Replaces the capsule visual with the Starter Assets armature (Unity Companion License),
        /// animated by our own PlayerController; the CharacterController and inputs stay ours.</summary>
        static void AttachArmature(GameObject player, Gameplay.PlayerController pc)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArmatureDir}/Models/Armature.fbx");
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{ArmatureDir}/Animations/StarterAssetsThirdPerson.controller");
            if (!model || !ctrl) { Debug.LogWarning("[LevelBuilder] Starter Assets armature missing; keeping capsule"); return; }
            Object.DestroyImmediate(player.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(player.GetComponent<MeshFilter>());
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, player.transform);
            go.name = "Armature";
            go.transform.localPosition = new Vector3(0, -1f, 0);   // CharacterController centre -> feet
            go.transform.localRotation = Quaternion.identity;
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            go.AddComponent<Gameplay.PlayerAnimEvents>();
            pc.animator = anim;
        }

        static void RockColumn(Transform parent, Vector3 floorPos, float h)
        {
            var g = Group(parent, "ArtColumn");
            for (float y = -0.3f; y < h; y += 4.8f)
            {
                var p = Piece(g, "Cave_Pillar_A", Quaternion.Euler(0, AR(0, 360), 0), new Vector3(1.5f, 1, 1.5f));
                Snap(p, floorPos + Vector3.up * y, new Vector3(0, -1, 0));
            }
        }
    }
}
