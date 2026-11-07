using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using Zfw.QualityTiers;

namespace MineEmbers.Rendering
{
    /// <summary>
    /// Heat-haze distortion (FR-G-08) written against the Render Graph API.
    ///
    /// Render Graph in one paragraph: instead of issuing commands immediately, each pass *declares* which
    /// textures it reads and writes. URP compiles the whole frame graph, culls passes whose output nobody
    /// reads, allocates transient textures from a pool, and on tile-based GPUs (Apple, mobile) can merge
    /// passes so data stays in tile memory. Our pass: read the camera color, write a distorted copy, and
    /// make that copy the new camera color, so no extra blit back is needed.
    /// </summary>
    public class HeatHazeFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Shader shader;
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            [Range(0f, 0.05f)] public float maxStrength = 0.012f;
            public float speed = 1.6f;
            public float scale = 18f;
        }

        public Settings settings = new Settings();

        /// <summary>0..1, driven by <see cref="HeatHazeZone"/> as the camera nears the furnace.</summary>
        public static float Intensity;
        /// <summary>&gt;= 0 freezes the shimmer at that time (screenshot mode).</summary>
        public static float TimeOverride = -1f;

        static readonly int StrengthId = Shader.PropertyToID("_HazeStrength");
        static readonly int ParamsId = Shader.PropertyToID("_HazeParams");

        Material _material;
        HazePass _pass;

        public override void Create()
        {
            if (!settings.shader) settings.shader = Shader.Find("Hidden/MineEmbers/HeatHaze");
            if (settings.shader) _material = CoreUtils.CreateEngineMaterial(settings.shader);
            _pass = new HazePass { renderPassEvent = settings.passEvent };
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

        public static bool EnabledByTier()
        {
            var s = QualityTiers.Settings;
            // No catalog (e.g. editor without bootstrap) -> keep the effect visible for authoring.
            return (!s || s.heatHaze) && !QualityTiers.IsDisabled("HeatHaze");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camType = renderingData.cameraData.cameraType;
            if (!_material || camType == CameraType.Reflection || camType == CameraType.Preview) return;
            if (Intensity <= 0.001f || !EnabledByTier()) return;

            _material.SetFloat(StrengthId, settings.maxStrength * Intensity);
            _material.SetVector(ParamsId, new Vector4(settings.speed, settings.scale, TimeOverride, 0));
            _pass.material = _material;
            renderer.EnqueuePass(_pass);
        }

        class HazePass : ScriptableRenderPass
        {
            public Material material;

            public HazePass()
            {
                // We sample the camera color, so it must be an intermediate texture, not the backbuffer.
                requiresIntermediateTexture = true;
                profilingSampler = new ProfilingSampler("HeatHaze");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var res = frameData.Get<UniversalResourceData>();
                if (res.isActiveTargetBackBuffer) return;

                TextureHandle src = res.activeColorTexture;
                var desc = renderGraph.GetTextureDesc(src);
                desc.name = "_HeatHazeColor";
                desc.clearBuffer = false;
                TextureHandle dst = renderGraph.CreateTexture(desc);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(src, dst, material, 0), "HeatHaze");
                res.cameraColor = dst;  // later passes (post-processing) read our output
            }
        }
    }
}
