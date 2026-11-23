using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MineEmbers.Boot;
using MineEmbers.Gameplay;
using MineEmbers.Rendering;
using Zfw.PerfRunner;

namespace MineEmbers.EditorTools
{
    /// <summary>
    /// Generates the greybox level (FR-G-01..07) as code: three zones, 20-40 fire lights each, particles,
    /// lake reflection, heat haze, perf zones and 15 shot points. Deterministic (fixed seed), so
    /// re-running produces the same scene and the same perf baseline.
    ///   Unity -batchmode -quit -projectPath . -executeMethod MineEmbers.EditorTools.LevelBuilder.Build
    /// Art pass later replaces the primitives; gameplay/perf markers stay.
    /// </summary>
    public static partial class LevelBuilder
    {
        const string ScenePath = "Assets/Scenes/Mine.unity";
        const string ArtDir = "Assets/Art/Greybox";
        static System.Random _rng;
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly Color Fire = new Color(1f, 0.55f, 0.25f);
        static int _lights;

        [MenuItem("MineEmbers/Build Greybox Level")]
        public static void Build()
        {
            _rng = new System.Random(20270104);
            _lights = 0;
            CreateMaterials();
            InitArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.075f, 0.062f, 0.058f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.fog = false;  // TierPostProcess decides at runtime

            var level = new GameObject("Level").transform;
            int a = BuildEntrance(level);
            int b = BuildLake(level);
            int c = BuildFurnace(level);
            Debug.Log($"[LevelBuilder] lights: entrance={a} lake={b} furnace={c} total={_lights}; art={_art} pieces={_pieces}");

            BuildSystems();
            BuildPlayerAndCamera();
            BuildPerfZones();
            BuildShotPoints();
            AddHeatHazeFeature();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelBuilder] saved {ScenePath}");
        }

        // ------------------------------------------------------------------ zones

        // Zone A: 60 m timbered tunnel. Wooden frames every 6 m, rails, minecarts, wall torches.
        static int BuildEntrance(Transform level)
        {
            int start = _lights;
            var z = Group(level, "Zone_Entrance");
            Room(z, "Tunnel", -3, 3, 0, 60, 5, openSouth: false, openNorth: true);
            Room(z, "Connector", -3, 3, 60, 75, 5, openSouth: true, openNorth: true);
            for (float zz = 3; zz < 74; zz += 6)
            {
                var posts = new[] {
                    Box(z, "Post", new Vector3(-2.6f, 2.2f, zz), new Vector3(0.35f, 4.4f, 0.35f), "Wood"),
                    Box(z, "Post", new Vector3(2.6f, 2.2f, zz), new Vector3(0.35f, 4.4f, 0.35f), "Wood"),
                    Box(z, "Beam", new Vector3(0, 4.55f, zz), new Vector3(5.6f, 0.4f, 0.4f), "Wood") };
                if (_art) { foreach (var p in posts) Hide(p); TimberFrame(z, zz, 2.6f, 4.35f); }
            }
            if (_art)
            {
                RailTrack(z, 0, 74);
                ArtMinecart(z, new Vector3(0, 0, 18), "Mine_Cart_AB_grp");
                ArtMinecart(z, new Vector3(0, 0, 44), "Mine_Cart_CB_grp");
            }
            else
            {
                Box(z, "Rail_L", new Vector3(-0.6f, 0.06f, 37), new Vector3(0.08f, 0.12f, 74), "Iron", collider: false);
                Box(z, "Rail_R", new Vector3(0.6f, 0.06f, 37), new Vector3(0.08f, 0.12f, 74), "Iron", collider: false);
                for (float zz = 1; zz < 74; zz += 0.9f)
                    Box(z, "Sleeper", new Vector3(0, 0.03f, zz), new Vector3(1.8f, 0.06f, 0.22f), "Wood", collider: false);
                Minecart(z, new Vector3(0, 0, 18));
                Minecart(z, new Vector3(0, 0, 44));
            }
            for (int i = 0; i < 6; i++)
                Box(z, "Crate", new Vector3(i % 2 == 0 ? -2f : 2f, 0.4f, 9 + i * 9.5f), Vector3.one * 0.8f, "Wood", rotY: R(-20, 20));
            ScatterRocks(z, -3, 3, 0, 75, 5, 40);

            // Torches: alternate sides every 7 m -> 11; frame lanterns every 12 m -> 6; braziers at the mouth.
            int k = 0;
            for (float zz = 5; zz < 74; zz += 6, k++)
                Torch(z, new Vector3(k % 2 == 0 ? -2.75f : 2.75f, 2.4f, zz), k % 2 == 0 ? 90 : -90);
            for (float zz = 9; zz < 74; zz += 12)
                Lantern(z, new Vector3(0, 4.1f, zz));
            Brazier(z, new Vector3(-1.8f, 0, 2.5f), 0.8f);
            Brazier(z, new Vector3(1.8f, 0, 2.5f), 0.8f);
            return _lights - start;
        }

        // Zone B: 50 x 50 m cavern around a still lake (reflections), crystals, stalagmites.
        static int BuildLake(Transform level)
        {
            int start = _lights;
            var z = Group(level, "Zone_Lake");
            float x0 = -25, x1 = 25, z0 = 75, z1 = 125, h = 14;
            Room(z, "Cavern", x0, x1, z0, z1, h, openSouth: true, openNorth: true, floor: false);
            // Floor ring around a sunken basin.
            float lx0 = -15, lx1 = 15, lz0 = 88, lz1 = 112;
            Hide(Box(z, "Floor_S", new Vector3(0, -0.25f, (z0 + lz0) / 2), new Vector3(x1 - x0, 0.5f, lz0 - z0), "Rock"));
            Hide(Box(z, "Floor_N", new Vector3(0, -0.25f, (lz1 + z1) / 2), new Vector3(x1 - x0, 0.5f, z1 - lz1), "Rock"));
            Hide(Box(z, "Floor_W", new Vector3((x0 + lx0) / 2, -0.25f, 100), new Vector3(lx0 - x0, 0.5f, lz1 - lz0), "Rock"));
            Hide(Box(z, "Floor_E", new Vector3((lx1 + x1) / 2, -0.25f, 100), new Vector3(x1 - lx1, 0.5f, lz1 - lz0), "Rock"));
            if (_art)
            {
                CaveFloorRect(z, x0, x1, z0, lz0); CaveFloorRect(z, x0, x1, lz1, z1);
                CaveFloorRect(z, x0, lx0, lz0, lz1); CaveFloorRect(z, lx1, x1, lz0, lz1);
                // Shore: rock ledges drop from the floor into the basin on all four sides.
                Ledge(z, new Vector3(lx0, 0, lz0), new Vector3(lx1, 0, lz0), Vector3.forward, 2.4f);
                Ledge(z, new Vector3(lx0, 0, lz1), new Vector3(lx1, 0, lz1), Vector3.back, 2.4f);
                Ledge(z, new Vector3(lx0, 0, lz0), new Vector3(lx0, 0, lz1), Vector3.right, 2.4f);
                Ledge(z, new Vector3(lx1, 0, lz0), new Vector3(lx1, 0, lz1), Vector3.left, 2.4f);
            }
            Box(z, "Basin", new Vector3(0, -2.2f, 100), new Vector3(lx1 - lx0, 0.4f, lz1 - lz0), "RockDark");
            var water = Box(z, "Water", new Vector3(0, -0.35f, 100), new Vector3(lx1 - lx0, 0.02f, lz1 - lz0), "Water", collider: false);
            // Invisible wall so the player cannot walk into the lake.
            var guard = Box(z, "LakeGuard", new Vector3(0, 0.5f, 100), new Vector3(lx1 - lx0 - 0.5f, 1f, lz1 - lz0 - 0.5f), "Rock");
            Object.DestroyImmediate(guard.GetComponent<MeshRenderer>());

            var probeGo = new GameObject("LakeReflectionProbe");
            probeGo.transform.SetParent(z, false);
            probeGo.transform.position = new Vector3(0, 3, 100);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(x1 - x0, h, z1 - z0);
            probe.boxProjection = true;
            probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            probe.farClipPlane = 80;
            probeGo.AddComponent<LakeReflection>();

            // Stalagmites / stalactites (tapered via stacked cylinders) and glowing crystals.
            for (int i = 0; i < 26; i++)
            {
                var p = new Vector3(R(x0 + 2, x1 - 2), 0, R(z0 + 3, z1 - 3));
                if (p.x > lx0 - 1 && p.x < lx1 + 1 && p.z > lz0 - 1 && p.z < lz1 + 1) continue;
                if (Mathf.Abs(p.x) < 3.5f && (p.z < z0 + 8 || p.z > z1 - 8)) continue;  // keep doors clear
                Spike(z, p, R(1.5f, 4.5f), up: true);
            }
            for (int i = 0; i < 30; i++) Spike(z, new Vector3(R(x0 + 2, x1 - 2), h, R(z0 + 2, z1 - 2)), R(1.5f, 5f), up: false);
            ScatterRocks(z, x0, x1, z0, z1, h, 60);

            // Lights: wall torches around the perimeter, braziers on the shore, cool crystal lights.
            for (float xx = x0 + 5; xx <= x1 - 5; xx += 8)
            {
                Torch(z, new Vector3(xx, 2.6f, z0 + 0.3f), 0);
                Torch(z, new Vector3(xx, 2.6f, z1 - 0.3f), 180);
            }
            for (float zz = z0 + 6; zz <= z1 - 6; zz += 9.5f)
            {
                Torch(z, new Vector3(x0 + 0.3f, 2.6f, zz), 90);
                Torch(z, new Vector3(x1 - 0.3f, 2.6f, zz), -90);
            }
            foreach (var p in new[] { new Vector3(-17, 0, 86), new Vector3(17, 0, 86), new Vector3(-17, 0, 114), new Vector3(17, 0, 114), new Vector3(0, 0, 85.5f), new Vector3(0, 0, 114.5f) })
                Brazier(z, p, 1f);
            foreach (var p in new[] { new Vector3(-20, 0.6f, 98), new Vector3(20, 0.6f, 104), new Vector3(-8, 0.6f, 120), new Vector3(9, 0.6f, 80) })
                Crystal(z, p);
            // Glow-moss on the ceiling: cool, wide fill lights so the lake surface has something to reflect.
            for (int i = 0; i < 6; i++)
                Point(z, "MossGlow", new Vector3(-15 + 15 * (i % 3), h - 1.5f, i < 3 ? 92 : 108), new Color(0.35f, 0.8f, 0.7f), 45f, 24f, priority: 0.4f, flicker: 0.02f);
            return _lights - start;
        }

        // Zone C: 44 x 60 m hall, lava channel with a bridge, giant furnace. Heaviest zone (benchmark).
        static int BuildFurnace(Transform level)
        {
            int start = _lights;
            var z = Group(level, "Zone_Furnace");
            Room(z, "Connector", -3, 3, 125, 140, 5, openSouth: true, openNorth: true);
            float x0 = -22, x1 = 22, z0 = 140, z1 = 200, h = 20;
            Room(z, "Hall", x0, x1, z0, z1, h, openSouth: true, openNorth: false, floor: false);
            float cz0 = 160, cz1 = 166;
            Hide(Box(z, "Floor_S", new Vector3(0, -0.25f, (z0 + cz0) / 2), new Vector3(x1 - x0, 0.5f, cz0 - z0), "Rock"));
            Hide(Box(z, "Floor_N", new Vector3(0, -0.25f, (cz1 + z1) / 2), new Vector3(x1 - x0, 0.5f, z1 - cz1), "Rock"));
            if (_art)
            {
                CaveFloorRect(z, x0, x1, z0, cz0); CaveFloorRect(z, x0, x1, cz1, z1);
                Ledge(z, new Vector3(x0, 0, cz0), new Vector3(x1, 0, cz0), Vector3.forward, 2.6f);
                Ledge(z, new Vector3(x0, 0, cz1), new Vector3(x1, 0, cz1), Vector3.back, 2.6f);
                WoodBridge(z, 163, cz1 - cz0 + 1);
            }
            Box(z, "ChannelBed", new Vector3(0, -2.5f, 163), new Vector3(x1 - x0, 0.5f, cz1 - cz0), "RockDark");
            Box(z, "Lava", new Vector3(0, -1.2f, 163), new Vector3(x1 - x0, 0.05f, cz1 - cz0), "Lava", collider: false);
            Hide(Box(z, "Bridge", new Vector3(0, -0.15f, 163), new Vector3(4.5f, 0.3f, cz1 - cz0 + 1), "Stone"));
            Box(z, "Rail_W", new Vector3(-2.2f, 0.5f, 163), new Vector3(0.2f, 1f, cz1 - cz0 + 1), "Iron");
            Box(z, "Rail_E", new Vector3(2.2f, 0.5f, 163), new Vector3(0.2f, 1f, cz1 - cz0 + 1), "Iron");
            // Invisible guards on both sides of the bridge keep the player out of the lava.
            foreach (float sx in new[] { -1f, 1f })
            {
                var g = Box(z, "LavaGuard", new Vector3(sx * 12.6f, 0.5f, 163), new Vector3(18.8f, 1f, cz1 - cz0 - 0.4f), "Rock");
                Object.DestroyImmediate(g.GetComponent<MeshRenderer>());
            }

            // The furnace: stacked blocks, glowing mouth, chimney into the ceiling.
            Box(z, "Furnace_Base", new Vector3(0, 3, 190), new Vector3(16, 6, 10), "Stone");
            Box(z, "Furnace_Mid", new Vector3(0, 8.5f, 190.5f), new Vector3(12, 5, 8), "Stone");
            Box(z, "Furnace_Top", new Vector3(0, 12.5f, 191), new Vector3(8, 3, 6), "Iron");
            Box(z, "Chimney", new Vector3(0, 17, 191), new Vector3(3.5f, 6, 3.5f), "Iron");
            Box(z, "Furnace_Mouth", new Vector3(0, 2.2f, 184.95f), new Vector3(6, 3.4f, 0.1f), "FurnaceGlow", collider: false);
            foreach (float sx in new[] { -10f, 10f })
            {
                Box(z, "Crucible", new Vector3(sx, 1.2f, 180), new Vector3(3, 2.4f, 3), "Iron");
                Box(z, "Crucible_Melt", new Vector3(sx, 2.42f, 180), new Vector3(2.4f, 0.05f, 2.4f), "Lava", collider: false);
            }
            for (int i = 0; i < 4; i++)
            {
                var pos = new Vector3(i < 2 ? -16 : 16, h / 2, i % 2 == 0 ? 152 : 178);
                var pillar = Box(z, "Pillar", pos, new Vector3(2.5f, h, 2.5f), "Stone");
                if (_art) { Hide(pillar); RockColumn(z, new Vector3(pos.x, 0, pos.z), h); }
            }
            ScatterRocks(z, x0, x1, z0, z1, h, 50);

            // Lights: furnace mouth + crucibles, lava glow along the channel, braziers, wall torches.
            Point(z, "FurnaceLight", new Vector3(-2, 2.5f, 183.5f), new Color(1f, 0.45f, 0.15f), 8f, 16f, priority: 4);
            Point(z, "FurnaceLight", new Vector3(2, 2.5f, 183.5f), new Color(1f, 0.45f, 0.15f), 8f, 16f, priority: 4);
            Point(z, "FurnaceTop", new Vector3(0, 14.5f, 187.5f), new Color(1f, 0.5f, 0.2f), 4f, 12f, priority: 2);
            foreach (float sx in new[] { -10f, 10f }) Point(z, "CrucibleLight", new Vector3(sx, 3.4f, 180), new Color(1f, 0.5f, 0.15f), 5f, 9f, priority: 2);
            for (float xx = x0 + 3; xx <= x1 - 3; xx += 6.5f)
                Point(z, "LavaGlow", new Vector3(xx, 0.2f, 163), new Color(1f, 0.35f, 0.08f), 6f, 9f, priority: 1.5f, flicker: 0.12f);
            foreach (var p in new[] { new Vector3(-6, 0, 146), new Vector3(6, 0, 146), new Vector3(-6, 0, 157), new Vector3(6, 0, 157),
                                      new Vector3(-6, 0, 170), new Vector3(6, 0, 170), new Vector3(-17, 0, 186), new Vector3(17, 0, 186) })
                Brazier(z, p, 1.1f);
            for (float zz = z0 + 4; zz <= z1 - 4; zz += 8)
            {
                Torch(z, new Vector3(x0 + 0.3f, 3f, zz), 90);
                Torch(z, new Vector3(x1 - 0.3f, 3f, zz), -90);
            }

            // Particles: lava embers along the channel, furnace sparks, chimney smoke.
            var embers = Particles(z, "LavaEmbers", new Vector3(0, -1.1f, 163), ParticleKind.Embers, 1f);
            var sh = embers.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(x1 - x0, 0.1f, cz1 - cz0);
            var em = embers.emission; em.rateOverTime = 60;
            var lavaBubbles = Particles(z, "LavaBubbles", new Vector3(0, -1.1f, 163), ParticleKind.Fire, 1.4f);
            var sh2 = lavaBubbles.shape; sh2.shapeType = ParticleSystemShapeType.Box; sh2.scale = new Vector3(x1 - x0, 0.1f, cz1 - cz0);
            var em2 = lavaBubbles.emission; em2.rateOverTime = 90;
            Particles(z, "FurnaceSparks", new Vector3(0, 1.5f, 184.6f), ParticleKind.Sparks, 3f).transform.rotation = Quaternion.Euler(-60, 180, 0);
            Particles(z, "FurnaceFire", new Vector3(0, 0.6f, 185.6f), ParticleKind.Fire, 3.5f);
            Particles(z, "ChimneySmoke", new Vector3(0, 19.5f, 191), ParticleKind.Smoke, 2.5f);

            var haze = new GameObject("HeatHazeZone");
            haze.transform.SetParent(z, false);
            haze.transform.position = new Vector3(0, 2, 178);
            var hz = haze.AddComponent<HeatHazeZone>(); hz.fullRadius = 14; hz.fadeRadius = 34;
            return _lights - start;
        }

        // ------------------------------------------------------------------ systems / player / markers

        static void BuildSystems()
        {
            var sys = new GameObject("Systems");
            sys.AddComponent<GameBootstrap>();
            sys.AddComponent<SettingsMenu>();
            sys.AddComponent<TierLightManager>();

            var vol = new GameObject("GlobalVolume");
            var v = vol.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = VolumeProfileAsset();
            vol.AddComponent<TierPostProcess>();
        }

        static VolumeProfile VolumeProfileAsset()
        {
            const string path = "Assets/Settings/MineVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
            var bl = p.Add<Bloom>(true);
            bl.threshold.Override(1.0f); bl.intensity.Override(0.6f); bl.scatter.Override(0.72f);
            bl.tint.Override(new Color(1f, 0.85f, 0.7f));
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.4f); ca.contrast.Override(12f); ca.saturation.Override(6f);
            var vg = p.Add<Vignette>(true); vg.intensity.Override(0.28f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            return p;
        }

        static void BuildPlayerAndCamera()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.GetComponent<MeshRenderer>().sharedMaterial = Mats["Player"];
            player.transform.position = new Vector3(0, 1.05f, 5);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2; cc.radius = 0.4f; cc.center = Vector3.zero;
            var pc = player.AddComponent<PlayerController>();
            if (_art) AttachArmature(player, pc);
            // A small lantern the miner carries, so dark stretches are readable.
            Point(player.transform, "CarryLantern", player.transform.position + new Vector3(0.35f, 0.4f, 0.3f), new Color(1f, 0.7f, 0.45f), 1.2f, 6f, priority: 0.5f, flicker: 0.08f)
                .transform.SetParent(player.transform, true);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 160f; cam.fieldOfView = 60;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;  // MSAA from the tier's URP asset
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0, 2.6f, 0.6f);
            var tpc = camGo.AddComponent<ThirdPersonCamera>();
            tpc.target = player.transform;
            pc.cameraRig = camGo.transform;
        }

        static void BuildPerfZones()
        {
            var root = new GameObject("PerfZones").transform;
            Zone(root, "entrance", 0, new[] { P(0, 1.7f, 3, 0, 0), P(0.8f, 1.7f, 20, 0, 5), P(-0.6f, 1.8f, 40, 0, -5), P(0, 1.7f, 58, 0, 0), P(0, 1.7f, 72, 0, 0) });
            Zone(root, "lake", 1, new[] { P(0, 2.2f, 78, 10, 0), P(-19, 2.5f, 86, 8, 45), P(-20, 2.5f, 112, 8, 135), P(0, 3f, 121, 12, 180), P(19, 2.5f, 112, 8, 225), P(19, 2.5f, 88, 8, 315), P(0, 2f, 123, 0, 0) });
            Zone(root, "furnace", 2, new[] { P(0, 2f, 142, 0, 0), P(0, 2f, 155, 4, 0), P(0, 2.2f, 163, 10, 0), P(-12, 3f, 172, 0, 30), P(12, 4f, 176, 5, -30), P(0, 2.5f, 178, 5, 0) });
        }

        static void BuildShotPoints()
        {
            var root = new GameObject("ShotPoints").transform;
            var shots = new (string id, Pose p)[]
            {
                ("e01", P(0, 1.7f, 9, 3, 0)), ("e02", P(-1.5f, 1.4f, 16, 5, 25)), ("e03", P(1.6f, 3.5f, 30, 25, -20)),
                ("e04", P(0, 1.7f, 50, 0, 180)), ("e05", P(0, 1.7f, 66, 0, 0)),
                ("l01", P(0, 2.5f, 79, 8, 0)), ("l02", P(-20, 3f, 86, 12, 50)), ("l03", P(0, 1f, 87, 2, 0)),
                ("l04", P(18, 6f, 118, 25, 220)), ("l05", P(-8, 1.2f, 117, 15, 150)),
                ("f01", P(0, 2.5f, 142, 4, 0)), ("f02", P(-3, 3f, 158, 28, 20)), ("f03", P(-15, 6f, 150, 15, 45)),
                ("f04", P(0, 2f, 176, 0, 0)), ("f05", P(16, 12f, 196, 35, 220)),
            };
            foreach (var (id, pose) in shots)
            {
                var go = new GameObject("Shot_" + id);
                go.transform.SetParent(root, false);
                go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                go.AddComponent<ShotPoint>().shotId = id;
            }
        }

        static void Zone(Transform root, string id, int order, Pose[] waypoints)
        {
            var go = new GameObject("PerfZone_" + id);
            go.transform.SetParent(root, false);
            go.transform.position = waypoints[0].position;
            var z = go.AddComponent<PerfZone>(); z.zoneId = id; z.order = order;
            for (int i = 0; i < waypoints.Length; i++)
            {
                var w = new GameObject($"wp{i}");
                w.transform.SetParent(go.transform, true);
                w.transform.SetPositionAndRotation(waypoints[i].position, waypoints[i].rotation);
            }
        }

        static Pose P(float x, float y, float z, float pitch, float yaw) => new Pose(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, 0));

        /// <summary>Adds the heat-haze Renderer Feature to the URP renderer (once).</summary>
        static void AddHeatHazeFeature()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer.rendererFeatures.Exists(f => f is HeatHazeFeature)) return;
            var feat = ScriptableObject.CreateInstance<HeatHazeFeature>();
            feat.name = "HeatHaze";
            feat.settings.shader = Shader.Find("Hidden/MineEmbers/HeatHaze");
            AssetDatabase.AddObjectToAsset(feat, renderer);
            renderer.rendererFeatures.Add(feat);
            // URP keeps a parallel list of local file ids; keep it in sync or the feature is dropped on reload.
            var so = new SerializedObject(renderer);
            var map = so.FindProperty("m_RendererFeatureMap");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out _, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ building blocks

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, string mat, bool collider = true, float rotY = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats[mat];
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        /// <summary>Axis-aligned room: floor, ceiling, four walls; optional 6 m doorway in the south/north wall.</summary>
        static void Room(Transform parent, string name, float x0, float x1, float z0, float z1, float h, bool openSouth, bool openNorth, bool floor = true)
        {
            var g = Group(parent, name);
            const float t = 1f, door = 6f, doorH = 5f;
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, w = x1 - x0, d = z1 - z0;
            if (floor) Box(g, "Floor", new Vector3(cx, -0.25f, cz), new Vector3(w, 0.5f, d), "Rock");
            Box(g, "Ceiling", new Vector3(cx, h + t / 2, cz), new Vector3(w + 2 * t, t, d + 2 * t), "RockDark");
            Box(g, "Wall_W", new Vector3(x0 - t / 2, h / 2, cz), new Vector3(t, h, d), "Rock");
            Box(g, "Wall_E", new Vector3(x1 + t / 2, h / 2, cz), new Vector3(t, h, d), "Rock");
            EndWall(g, "Wall_S", z0 - t / 2, x0, x1, h, openSouth, door, doorH, t);
            EndWall(g, "Wall_N", z1 + t / 2, x0, x1, h, openNorth, door, doorH, t);
            if (!_art) return;
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) Hide(r.gameObject);
            if (w <= door + 0.01f) DressTunnel(g, x0, x1, z0, z1, h, closedSouth: !openSouth);
            else DressCave(g, x0, x1, z0, z1, h, doorSouth: openSouth, doorNorth: openNorth);
        }

        /// <summary>open = this end connects to a neighbour: narrow tunnels stay fully open, wide rooms get a doorway.</summary>
        static void EndWall(Transform g, string name, float z, float x0, float x1, float h, bool open, float door, float doorH, float t)
        {
            float w = x1 - x0, cx = (x0 + x1) / 2;
            if (!open)
            {
                Box(g, name, new Vector3(cx, h / 2, z), new Vector3(w + 2 * t, h, t), "Rock");
                return;
            }
            if (w <= door + 0.01f) return;
            float side = (w - door) / 2;
            Box(g, name + "_L", new Vector3(x0 + side / 2, h / 2, z), new Vector3(side, h, t), "Rock");
            Box(g, name + "_R", new Vector3(x1 - side / 2, h / 2, z), new Vector3(side, h, t), "Rock");
            if (h > doorH) Box(g, name + "_Top", new Vector3(cx, (h + doorH) / 2, z), new Vector3(door, h - doorH, t), "Rock");
        }

        static void ScatterRocks(Transform parent, float x0, float x1, float z0, float z1, float h, int count)
        {
            var g = Group(parent, "Rocks");
            for (int i = 0; i < count; i++)
            {
                bool west = _rng.Next(2) == 0;
                float x = west ? x0 + R(0.1f, 0.6f) : x1 - R(0.1f, 0.6f);
                var pos = new Vector3(x, R(0, h * 0.9f), R(z0 + 1, z1 - 1));
                var size = new Vector3(R(0.6f, 1.6f), R(0.6f, 2.2f), R(0.8f, 2.5f));
                bool dark = _rng.Next(3) == 0;
                var rot = Quaternion.Euler(R(-25, 25), R(0, 360), R(-25, 25));  // same draws in both modes
                if (_art)
                {
                    // Art walls already have relief; instead put a boulder at the wall foot when there is floor.
                    if (x1 - x0 <= 6.01f || i % 2 == 1) continue;
                    var foot = new Vector3(west ? x0 + 1.2f : x1 - 1.2f, 3f, pos.z);
                    Physics.SyncTransforms();
                    if (Physics.Raycast(foot, Vector3.down, out var hit, 4f) && hit.point.y > -0.3f && !hit.collider.name.Contains("Guard"))
                        Boulder(g, new Vector3(foot.x, 0, foot.z), AR(0.7f, 1.3f));
                    continue;
                }
                var go = Box(g, "Rock", pos, size, dark ? "RockDark" : "Rock", collider: false);
                go.transform.rotation = rot;
            }
        }

        static void Spike(Transform parent, Vector3 basePos, float height, bool up)
        {
            if (_art)
            {
                ArtSpike(parent, basePos, height, up);
                if (up)  // keep the greybox base segment as an invisible collider
                {
                    var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    col.name = "StalagmiteCollider";
                    col.transform.SetParent(parent, false);
                    col.transform.position = basePos + Vector3.up * height / 6f;
                    col.transform.localScale = new Vector3(height * 0.36f, height / 6f, height * 0.36f);
                    Hide(col);
                }
                return;
            }
            float r = height * 0.18f;
            int segs = 3;
            for (int i = 0; i < segs; i++)
            {
                float segH = height / segs, rad = r * (1f - i / (float)segs);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = up ? "Stalagmite" : "Stalactite";
                go.transform.SetParent(parent, false);
                float y = up ? basePos.y + segH * (i + 0.5f) : basePos.y - segH * (i + 0.5f);
                go.transform.position = new Vector3(basePos.x, y, basePos.z);
                go.transform.localScale = new Vector3(rad * 2, segH / 2, rad * 2);
                go.GetComponent<MeshRenderer>().sharedMaterial = Mats["Rock"];
                if (!up || i > 0) Object.DestroyImmediate(go.GetComponent<Collider>());
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        static void Minecart(Transform parent, Vector3 pos)
        {
            var g = Group(parent, "Minecart");
            g.position = pos;
            Box(g, "Body", pos + new Vector3(0, 0.75f, 0), new Vector3(1.3f, 0.8f, 1.9f), "Iron");
            Box(g, "Ore", pos + new Vector3(0, 1.2f, 0), new Vector3(1.1f, 0.25f, 1.7f), "RockDark", collider: false);
            foreach (float sx in new[] { -0.6f, 0.6f })
            foreach (float sz in new[] { -0.6f, 0.6f })
                Box(g, "Wheel", pos + new Vector3(sx, 0.25f, sz), new Vector3(0.12f, 0.4f, 0.4f), "Iron", collider: false);
        }

        static void Torch(Transform parent, Vector3 pos, float yaw)
        {
            var g = Group(parent, "Torch");
            g.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var stick = Box(g, "Stick", pos, new Vector3(0.08f, 0.6f, 0.08f), "Wood", collider: false);
            stick.transform.rotation = Quaternion.Euler(25, yaw, 0);
            Vector3 flame = pos + g.forward * 0.15f + Vector3.up * 0.32f;
            Particles(g, "Flame", flame, ParticleKind.Fire, 0.5f);
            Point(g, "Light", flame + g.forward * 0.25f, Fire, 4.5f, 9f, priority: 1f);
        }

        static void Lantern(Transform parent, Vector3 pos)
        {
            var g = Group(parent, "Lantern");
            g.position = pos;
            Box(g, "Cage", pos, new Vector3(0.25f, 0.35f, 0.25f), "Glow", collider: false);
            Point(g, "Light", pos - Vector3.up * 0.2f, new Color(1f, 0.7f, 0.4f), 3f, 7f, priority: 0.7f, flicker: 0.1f);
        }

        static void Brazier(Transform parent, Vector3 floorPos, float scale)
        {
            var g = Group(parent, "Brazier");
            g.position = floorPos;
            Box(g, "Stand", floorPos + Vector3.up * 0.45f * scale, new Vector3(0.25f, 0.9f, 0.25f) * scale, "Iron");
            Box(g, "Bowl", floorPos + Vector3.up * 0.95f * scale, new Vector3(0.9f, 0.25f, 0.9f) * scale, "Iron");
            Box(g, "Coals", floorPos + Vector3.up * 1.08f * scale, new Vector3(0.7f, 0.05f, 0.7f) * scale, "Lava", collider: false);
            Vector3 top = floorPos + Vector3.up * 1.1f * scale;
            Particles(g, "Fire", top, ParticleKind.Fire, 1.2f * scale);
            Particles(g, "Sparks", top, ParticleKind.Sparks, 0.8f * scale);
            Particles(g, "Smoke", top + Vector3.up * 0.8f, ParticleKind.Smoke, 0.7f * scale);
            Point(g, "Light", top + Vector3.up * 0.5f, Fire, 6f * scale, 12f * scale, priority: 2f);
        }

        static void Crystal(Transform parent, Vector3 pos)
        {
            var g = Group(parent, "Crystal");
            g.position = pos;
            for (int i = 0; i < 4; i++)
            {
                var c = Box(g, "Shard", pos + new Vector3(R(-0.4f, 0.4f), R(0, 0.5f), R(-0.4f, 0.4f)), new Vector3(0.25f, R(0.8f, 1.6f), 0.25f), "Crystal", collider: false);
                c.transform.rotation = Quaternion.Euler(R(-25, 25), R(0, 90), R(-25, 25));
            }
            Point(g, "Light", pos + Vector3.up * 1.2f, new Color(0.35f, 0.75f, 1f), 3.5f, 9f, priority: 0.6f, flicker: 0.03f);
        }

        static Light Point(Transform parent, string name, Vector3 pos, Color color, float intensity, float range, float priority, float flicker = 0.25f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;  // TierLightManager hands out the shadow budget at runtime
            l.shadowNearPlane = 0.2f;
            var t = go.AddComponent<TorchLight>();
            t.shadowPriority = priority; t.flicker = flicker;
            _lights++;
            return l;
        }

        enum ParticleKind { Fire, Sparks, Smoke, Embers }

        static ParticleSystem Particles(Transform parent, string name, Vector3 pos, ParticleKind kind, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(-90, 0, 0));  // emit upward
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            var main = ps.main; var em = ps.emission; var sh = ps.shape; var col = ps.colorOverLifetime; var size = ps.sizeOverLifetime;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true; main.loop = true; main.prewarm = true;
            col.enabled = true; size.enabled = true;
            switch (kind)
            {
                case ParticleKind.Fire:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.3f * scale);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.7f * scale);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.85f, 0.4f));
                    main.maxParticles = Mathf.CeilToInt(60 * scale);
                    em.rateOverTime = 40 * scale;
                    sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8; sh.radius = 0.1f * scale;
                    col.color = Fade(new Color(1f, 0.75f, 0.35f), new Color(0.9f, 0.25f, 0.05f));
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
                    r.sharedMaterial = Mats["FxFire"];
                    break;
                case ParticleKind.Sparks:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * scale, 3.5f * scale);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                    main.startColor = new Color(1f, 0.7f, 0.3f);
                    main.gravityModifier = 0.25f;
                    main.maxParticles = Mathf.CeilToInt(40 * scale);
                    em.rateOverTime = 12 * scale;
                    sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25; sh.radius = 0.15f * scale;
                    col.color = Fade(new Color(1f, 0.85f, 0.5f), new Color(1f, 0.3f, 0.05f));
                    r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.5f;
                    r.sharedMaterial = Mats["FxFire"];
                    break;
                case ParticleKind.Embers:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                    main.startColor = new Color(1f, 0.55f, 0.2f);
                    main.gravityModifier = -0.05f;
                    main.maxParticles = 400;
                    var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.6f;
                    col.color = Fade(new Color(1f, 0.7f, 0.3f), new Color(1f, 0.2f, 0.02f));
                    r.sharedMaterial = Mats["FxFire"];
                    break;
                case ParticleKind.Smoke:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * scale, 0.7f * scale);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f * scale, 1.6f * scale);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = new Color(0.12f, 0.1f, 0.09f, 0.5f);
                    main.maxParticles = Mathf.CeilToInt(30 * scale);
                    em.rateOverTime = 5 * scale;
                    sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 0.2f * scale;
                    col.color = Fade(new Color(0.2f, 0.17f, 0.15f), new Color(0.1f, 0.1f, 0.1f));
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
                    r.sharedMaterial = Mats["FxSmoke"];
                    break;
            }
            go.AddComponent<TierParticles>();
            return ps;
        }

        static ParticleSystem.MinMaxGradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0, 1) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ materials

        static void CreateMaterials()
        {
            Directory.CreateDirectory(ArtDir);
            Mats.Clear();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Lit(lit, "Rock", new Color(0.30f, 0.26f, 0.23f), 0.15f);
            Lit(lit, "RockDark", new Color(0.16f, 0.14f, 0.13f), 0.1f);
            Lit(lit, "Stone", new Color(0.36f, 0.33f, 0.31f), 0.2f);
            Lit(lit, "Wood", new Color(0.36f, 0.22f, 0.12f), 0.25f);
            Lit(lit, "Iron", new Color(0.25f, 0.24f, 0.24f), 0.45f, metallic: 0.85f);
            Lit(lit, "Player", new Color(0.55f, 0.45f, 0.35f), 0.3f);
            Lit(lit, "Water", new Color(0.01f, 0.03f, 0.045f), 0.96f);
            Lit(lit, "Lava", new Color(0.2f, 0.05f, 0.01f), 0.6f, emission: new Color(1f, 0.32f, 0.06f) * 1.6f);
            Lit(lit, "FurnaceGlow", Color.black, 0.5f, emission: new Color(1f, 0.45f, 0.12f) * 2.2f);
            Lit(lit, "Glow", new Color(0.3f, 0.2f, 0.1f), 0.4f, emission: new Color(1f, 0.65f, 0.3f) * 2.5f);
            Lit(lit, "Crystal", new Color(0.1f, 0.25f, 0.35f), 0.9f, emission: new Color(0.3f, 0.7f, 1f) * 2.2f);
            var soft = SoftTexture("fx_soft.png", 64, smoke: false);
            var smoke = SoftTexture("fx_smoke.png", 128, smoke: true);
            Particle("FxFire", soft, additive: true);
            Particle("FxSmoke", smoke, additive: false);
        }

        static Material Save(string name, Material m)
        {
            string path = $"{ArtDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); existing.shaderKeywords = m.shaderKeywords; existing.renderQueue = m.renderQueue; m = existing; EditorUtility.SetDirty(m); }
            else AssetDatabase.CreateAsset(m, path);
            Mats[name] = m;
            return m;
        }

        static void Lit(Shader s, string name, Color c, float smooth, float metallic = 0, Color? emission = null)
        {
            var m = new Material(s) { name = name };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            Save(name, m);
        }

        /// <summary>URP Particles/Unlit, transparent. Render states set by hand (no material inspector in batch mode).</summary>
        static void Particle(string name, Texture2D tex, bool additive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1);                   // transparent
            m.SetFloat("_Blend", additive ? 2 : 0);      // URP BlendMode: Alpha=0, Additive=2
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Soft particles: fade where the quad intersects geometry. Needs the camera depth texture,
            // so TierParticles turns the keyword off on Low (whose URP asset skips the depth copy).
            m.SetFloat("_SoftParticlesEnabled", 1);
            m.SetFloat("_SoftParticlesNearFadeDistance", 0);
            m.SetFloat("_SoftParticlesFarFadeDistance", 0.6f);
            m.SetVector("_SoftParticleFadeParams", new Vector4(0, 1f / 0.6f, 0, 0));
            m.EnableKeyword("_SOFTPARTICLES_ON");
            Save(name, m);
        }

        static Texture2D SoftTexture(string file, int n, bool smoke)
        {
            string path = $"{ArtDir}/{file}";
            var rng = new System.Random(7);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01(1 - d);
                a = a * a * (3 - 2 * a);
                if (smoke) a *= 0.55f + 0.45f * Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f);
                px[y * n + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
