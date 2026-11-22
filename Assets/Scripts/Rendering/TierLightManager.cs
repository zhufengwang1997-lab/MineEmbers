using System.Collections.Generic;
using UnityEngine;
using Zfw.QualityTiers;

namespace MineEmbers.Rendering
{
    /// <summary>
    /// Shadow budget (FR-QT-01, OPT-01): only the N most relevant point lights cast shadows, N from the tier.
    /// Every point-light shadow is 6 cube faces rendered into the additional-lights atlas, so this is the
    /// single biggest knob for GPU cost in the torch-heavy zones.
    /// </summary>
    public class TierLightManager : MonoBehaviour
    {
        static readonly List<TorchLight> All = new List<TorchLight>();
        public static void Register(TorchLight t) { if (!All.Contains(t)) All.Add(t); }
        public static void Unregister(TorchLight t) => All.Remove(t);
        public static int ShadowedCount { get; private set; }

        public float refreshInterval = 0.25f;
        float _next;
        readonly List<(float score, TorchLight t)> _scratch = new List<(float, TorchLight)>();

        sealed class ByScoreDesc : IComparer<(float score, TorchLight t)>
        {
            public static readonly ByScoreDesc Instance = new ByScoreDesc();
            public int Compare((float score, TorchLight t) a, (float score, TorchLight t) b) => b.score.CompareTo(a.score);
        }

        void OnEnable() { QualityTiers.TierChanged += OnTier; }
        void OnDisable() { QualityTiers.TierChanged -= OnTier; }
        void OnTier(Tier _) => _next = 0;

        void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + refreshInterval;
            var cam = Camera.main;
            if (!cam) return;
            int budget = QualityTiers.Settings ? QualityTiers.Settings.maxShadowedPointLights : 4;

            _scratch.Clear();
            Vector3 c = cam.transform.position, f = cam.transform.forward;
            foreach (var t in All)
            {
                if (!t || !t.isActiveAndEnabled) continue;
                Vector3 d = t.transform.position - c;
                float dist = d.magnitude;
                // Closer and in front of the camera wins; lights behind us matter less.
                float facing = Vector3.Dot(f, d / Mathf.Max(dist, 0.001f)) * 0.5f + 0.5f;
                float score = t.shadowPriority * (0.5f + facing) / (1f + dist);
                _scratch.Add((score, t));
            }
            _scratch.Sort(ByScoreDesc.Instance);  // cached comparer: Sort(Comparison) allocates a wrapper each call
            ShadowedCount = 0;
            for (int i = 0; i < _scratch.Count; i++)
            {
                bool on = i < budget;
                var l = _scratch[i].t.Light;
                var want = on ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != want) l.shadows = want;
                if (on) ShadowedCount++;
            }
        }
    }
}
