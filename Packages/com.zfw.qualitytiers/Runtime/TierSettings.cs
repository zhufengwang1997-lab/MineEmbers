using UnityEngine;
using UnityEngine.Rendering;

namespace Zfw.QualityTiers
{
    /// <summary>Per-tier knobs the game reads (FR-QT-01). One asset per tier, paired with a URP asset.</summary>
    [CreateAssetMenu(menuName = "Zfw/Tier Settings", fileName = "TierSettings")]
    public class TierSettings : ScriptableObject
    {
        public Tier tier;
        [Tooltip("URP asset activated for this tier (render scale, MSAA, shadow atlas size live there).")]
        public RenderPipelineAsset pipeline;

        [Header("Lights")]
        [Tooltip("How many point lights may cast shadows at once; the rest have shadows off.")]
        public int maxShadowedPointLights = 8;
        public int shadowResolution = 2048;

        [Header("Particles")]
        [Range(0f, 1f)] public float particleDensity = 1f;
        public bool softParticles = true;

        [Header("Post-processing")]
        public bool bloom = true;
        [Tooltip("Bloom max iterations (URP 'Max Iterations' / downscale steps).")]
        public int bloomIterations = 6;
        public bool fog = true;
        public bool heatHaze = true;
        public bool lakeReflection = true;
    }
}
