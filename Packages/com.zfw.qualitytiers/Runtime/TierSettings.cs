using System.Collections.Generic;
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

        [Header("Game-specific")]
        [Tooltip("Per-game extra knobs. Each game defines its own [Serializable] subclass of TierExtension.")]
        [SerializeReference] public List<TierExtension> extensions = new List<TierExtension>();

        /// <summary>Returns the first extension of type T, or null when this game did not add one.</summary>
        public T Get<T>() where T : TierExtension
        {
            if (extensions == null) return null;
            foreach (var e in extensions) if (e is T t) return t;
            return null;
        }

        /// <summary>Returns the extension of type T, adding a new one if it is missing (editor/setup code).</summary>
        public T GetOrAdd<T>() where T : TierExtension, new()
        {
            var e = Get<T>();
            if (e != null) return e;
            e = new T();
            (extensions ??= new List<TierExtension>()).Add(e);
            return e;
        }
    }

    /// <summary>
    /// Base class for game-specific tier fields. The package only knows generic knobs (lights, particles,
    /// post); a game subclasses this (e.g. MineEmbers adds heat haze / lake reflection) and stores it in
    /// <see cref="TierSettings.extensions"/>.
    /// </summary>
    [System.Serializable]
    public abstract class TierExtension { }
}
