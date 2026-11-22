using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MineEmbers.Rendering;
using Zfw.QualityTiers;

namespace MineEmbers.EditorTools
{
    /// <summary>
    /// Idempotent project configuration as code. Re-run any time:
    ///   Unity -batchmode -quit -projectPath . -executeMethod MineEmbers.EditorTools.ProjectSetup.Run
    /// or menu MineEmbers > Project Setup.
    /// </summary>
    public static class ProjectSetup
    {
        const string TierDir = "Assets/Settings/Tiers";
        const string BaseAsset = "Assets/Settings/PC_RPAsset.asset";
        const string ResourcesDir = "Assets/Resources";

        // FR-QT-01 initial values. Change here, re-run, and note the reason in docs/perf.
        struct TierSpec
        {
            public Tier tier; public float renderScale; public int msaa;
            /// <summary>Additional-light shadow atlas size. Must hold shadowedLights x 6 cube faces x slice^2.</summary>
            public int shadowRes;
            /// <summary>Per-face resolution of one point-light shadow (all three URP light tiers set to this).</summary>
            public int slice;
            public int shadowedLights; public float particles; public bool softParticles;
            public bool bloom; public int bloomIter; public bool fog; public bool heatHaze; public bool reflection;
        }

        static readonly TierSpec[] Specs =
        {
            new TierSpec { tier = Tier.Low,    renderScale = 0.8f, msaa = 1, shadowRes = 1024, slice = 256, shadowedLights = 2,  particles = 0.4f, softParticles = false, bloom = false, bloomIter = 0, fog = false, heatHaze = false, reflection = false },
            new TierSpec { tier = Tier.Medium, renderScale = 0.9f, msaa = 2, shadowRes = 2048, slice = 256, shadowedLights = 4,  particles = 0.7f, softParticles = true,  bloom = true,  bloomIter = 3, fog = false, heatHaze = false, reflection = false },
            new TierSpec { tier = Tier.High,   renderScale = 1.0f, msaa = 4, shadowRes = 4096, slice = 512, shadowedLights = 8,  particles = 1.0f, softParticles = true,  bloom = true,  bloomIter = 5, fog = true,  heatHaze = true,  reflection = false },
            new TierSpec { tier = Tier.Ultra,  renderScale = 1.0f, msaa = 4, shadowRes = 8192, slice = 512, shadowedLights = 16, particles = 1.0f, softParticles = true,  bloom = true,  bloomIter = 6, fog = true,  heatHaze = true,  reflection = true  },
        };

        [MenuItem("MineEmbers/Project Setup")]
        public static void Run()
        {
            ConfigurePlayer();
            var assets = CreateTierPipelines();
            var catalog = CreateTierSettings(assets);
            ConfigureQualityLevels(assets);
            GraphicsSettings.defaultRenderPipeline = assets[(int)Tier.High];
            RemoveTemplateLeftovers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ProjectSetup] done: 4 tiers, catalog={AssetDatabase.GetAssetPath(catalog)}");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "zfw";
            PlayerSettings.productName = "MineEmbers";
            // Also names the Metal shader cache folder ($DARWIN_USER_CACHE_DIR/<id>/com.apple.metal).
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.zfw.mineembers");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;          // perf runs must not throttle when unfocused
            PlayerSettings.enableFrameTimingStats = true;   // FrameTimingManager gpuFrameTime (FR-PR-02)

            // Windows: D3D12 first (default), Vulkan second (-force-vulkan). D3D11 removed on purpose.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneOSX, new[] { GraphicsDeviceType.Metal });

            // Mono for fast iteration on both; IL2CPP release builds are set by the build script.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        }

        static UniversalRenderPipelineAsset[] CreateTierPipelines()
        {
            Directory.CreateDirectory(TierDir);
            var result = new UniversalRenderPipelineAsset[Specs.Length];
            foreach (var s in Specs)
            {
                string path = $"{TierDir}/URP_{s.tier}.asset";
                if (!File.Exists(path)) AssetDatabase.CopyAsset(BaseAsset, path);
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                var so = new SerializedObject(asset);
                so.FindProperty("m_RenderScale").floatValue = s.renderScale;
                so.FindProperty("m_MSAA").intValue = s.msaa;
                so.FindProperty("m_MainLightShadowmapResolution").intValue = s.shadowRes;
                so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = s.shadowRes;
                // CASE-05: point-light shadows are 6 cube faces each; the atlas must fit lights x 6 x slice^2
                // or URP silently downscales every slice (High ended up blurrier than Medium).
                foreach (var tierName in new[] { "Low", "Medium", "High" })
                    so.FindProperty($"m_AdditionalLightsShadowResolutionTier{tierName}").intValue = s.slice;
                Debug.Assert(s.shadowedLights * 6 * s.slice * s.slice <= s.shadowRes * s.shadowRes, $"{s.tier}: shadow atlas too small");
                so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
                // Soft particles sample the depth texture; Low skips the depth copy entirely.
                so.FindProperty("m_RequireDepthTexture").boolValue = s.softParticles;
                // Opaque texture feeds the heat-haze distortion.
                so.FindProperty("m_RequireOpaqueTexture").boolValue = s.heatHaze;
                so.FindProperty("m_SoftShadowQuality").intValue = s.tier == Tier.Low ? 1 : s.tier == Tier.Ultra ? 3 : 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                result[(int)s.tier] = asset;
            }
            return result;
        }

        static TierCatalog CreateTierSettings(UniversalRenderPipelineAsset[] assets)
        {
            Directory.CreateDirectory(ResourcesDir);
            string catPath = $"{ResourcesDir}/TierCatalog.asset";
            var cat = AssetDatabase.LoadAssetAtPath<TierCatalog>(catPath);
            if (!cat) { cat = ScriptableObject.CreateInstance<TierCatalog>(); AssetDatabase.CreateAsset(cat, catPath); }

            foreach (var s in Specs)
            {
                string path = $"{TierDir}/Tier_{s.tier}.asset";
                var t = AssetDatabase.LoadAssetAtPath<TierSettings>(path);
                if (!t) { t = ScriptableObject.CreateInstance<TierSettings>(); AssetDatabase.CreateAsset(t, path); }
                t.tier = s.tier;
                t.pipeline = assets[(int)s.tier];
                t.maxShadowedPointLights = s.shadowedLights;
                t.shadowResolution = s.slice;
                t.particleDensity = s.particles;
                t.softParticles = s.softParticles;
                t.bloom = s.bloom;
                t.bloomIterations = s.bloomIter;
                t.fog = s.fog;
                var mine = t.GetOrAdd<MineTierExtension>();
                mine.heatHaze = s.heatHaze;
                mine.lakeReflection = s.reflection;
                EditorUtility.SetDirty(t);
                switch (s.tier)
                {
                    case Tier.Low: cat.low = t; break;
                    case Tier.Medium: cat.medium = t; break;
                    case Tier.High: cat.high = t; break;
                    default: cat.ultra = t; break;
                }
            }
            EditorUtility.SetDirty(cat);
            return cat;
        }

        /// <summary>Replace the template's Mobile/PC levels with Low/Medium/High/Ultra.</summary>
        static void ConfigureQualityLevels(UniversalRenderPipelineAsset[] assets)
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qs);
            var levels = so.FindProperty("m_QualitySettings");
            // Use the template's "PC" level (last) as the prototype for every tier.
            int proto = levels.arraySize - 1;
            while (levels.arraySize < Specs.Length) levels.InsertArrayElementAtIndex(proto);
            while (levels.arraySize > Specs.Length) levels.DeleteArrayElementAtIndex(0);
            for (int i = 0; i < Specs.Length; i++)
            {
                var e = levels.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = Specs[i].tier.ToString();
                e.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                e.FindPropertyRelative("vSyncCount").intValue = 0;
                e.FindPropertyRelative("softParticles").boolValue = Specs[i].softParticles;
                // Required for LakeReflection: realtime probes are skipped entirely when this is off
                // (Low..High render the probe once, Ultra re-renders it time-sliced).
                e.FindPropertyRelative("realtimeReflectionProbes").boolValue = true;
            }
            so.FindProperty("m_CurrentQuality").intValue = (int)Tier.High;
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
                perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = (int)Tier.High;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RemoveTemplateLeftovers()
        {
            foreach (var p in new[] { "Assets/Settings/Mobile_RPAsset.asset", "Assets/Settings/Mobile_Renderer.asset" })
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
        }
    }
}
