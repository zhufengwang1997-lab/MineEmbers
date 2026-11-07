using System.Collections.Generic;
using UnityEngine;
using Zfw.QualityTiers;

namespace MineEmbers.Rendering
{
    /// <summary>
    /// Scales emission and particle cap by the tier's particle density (FR-G-06, OPT-03), and switches
    /// soft particles. Soft particles read the camera depth texture to fade where a quad cuts into
    /// geometry; Low's URP asset does not produce that texture, so the keyword must go off there.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class TierParticles : MonoBehaviour
    {
        const string SoftKeyword = "_SOFTPARTICLES_ON";
        // One runtime clone per (shared material, soft on/off): asset files are never modified and
        // systems sharing a material still share the clone (keeps SRP Batcher-friendly batching).
        static readonly Dictionary<(Material, bool), Material> Variants = new Dictionary<(Material, bool), Material>();

        ParticleSystem _ps;
        ParticleSystemRenderer _renderer;
        Material _source;
        float _baseRate;
        int _baseMax;
        bool _captured;

        void Capture()
        {
            if (_captured) return;
            _ps = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
            _source = _renderer.sharedMaterial;
            _baseRate = _ps.emission.rateOverTimeMultiplier;
            _baseMax = _ps.main.maxParticles;
            _captured = true;
        }

        void OnEnable() { Capture(); QualityTiers.TierChanged += Apply; Apply(QualityTiers.Current); }
        void OnDisable() => QualityTiers.TierChanged -= Apply;

        void Apply(Tier _)
        {
            var s = QualityTiers.Settings;
            float k = s ? s.particleDensity : 1f;
            var em = _ps.emission; em.rateOverTimeMultiplier = _baseRate * k;
            var main = _ps.main; main.maxParticles = Mathf.Max(1, Mathf.RoundToInt(_baseMax * k));

            if (!_source || !_source.HasProperty("_SoftParticlesEnabled")) return;
            bool soft = (!s || s.softParticles) && !QualityTiers.IsDisabled("SoftParticles");
            if (!Variants.TryGetValue((_source, soft), out var m))
            {
                m = new Material(_source) { name = _source.name + (soft ? "_soft" : "_hard") };
                if (soft) m.EnableKeyword(SoftKeyword); else m.DisableKeyword(SoftKeyword);
                m.SetFloat("_SoftParticlesEnabled", soft ? 1 : 0);
                Variants[(_source, soft)] = m;
            }
            _renderer.sharedMaterial = m;
        }
    }
}
