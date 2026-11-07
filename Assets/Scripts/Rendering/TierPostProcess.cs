using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zfw.QualityTiers;

namespace MineEmbers.Rendering
{
    /// <summary>
    /// Applies tier post-processing (FR-QT-01): Low = tonemapping only, Medium + bloom (few iterations),
    /// High + fog, Ultra everything. Works on a runtime copy of the profile so assets on disk never change.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class TierPostProcess : MonoBehaviour
    {
        public float fogDensity = 0.035f;
        VolumeProfile _profile;

        void OnEnable()
        {
            var v = GetComponent<Volume>();
            _profile = v.profile;  // .profile instantiates a per-volume copy
            QualityTiers.TierChanged += Apply;
            Apply(QualityTiers.Current);
        }

        void OnDisable() => QualityTiers.TierChanged -= Apply;

        void Apply(Tier _)
        {
            var s = QualityTiers.Settings;
            if (!s || !_profile) return;
            bool bloom = s.bloom && !QualityTiers.IsDisabled("Bloom");
            if (_profile.TryGet(out Bloom b))
            {
                b.active = bloom;
                b.maxIterations.Override(Mathf.Clamp(s.bloomIterations, 2, 8));
                b.highQualityFiltering.Override(s.tier == Tier.Ultra);
            }
            bool fog = s.fog && !QualityTiers.IsDisabled("Fog");
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = new Color(0.05f, 0.035f, 0.03f);
        }
    }
}
