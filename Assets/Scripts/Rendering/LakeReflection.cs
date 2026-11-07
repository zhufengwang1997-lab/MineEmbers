using UnityEngine;
using UnityEngine.Rendering;
using Zfw.QualityTiers;

namespace MineEmbers.Rendering
{
    /// <summary>
    /// Lake reflections (FR-G-07). Ultra: realtime reflection probe re-rendered time-sliced every few frames
    /// (torches flicker, particles move). Other tiers: rendered once after load, then frozen.
    /// </summary>
    [RequireComponent(typeof(ReflectionProbe))]
    public class LakeReflection : MonoBehaviour
    {
        public int ultraIntervalFrames = 4;
        ReflectionProbe _probe;
        int _renderId = -1;
        bool _realtime;

        void OnEnable()
        {
            _probe = GetComponent<ReflectionProbe>();
            _probe.mode = ReflectionProbeMode.Realtime;
            _probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            QualityTiers.TierChanged += Apply;
            Apply(QualityTiers.Current);
        }

        void OnDisable() => QualityTiers.TierChanged -= Apply;

        void Apply(Tier _)
        {
            var s = QualityTiers.Settings;
            _realtime = s && s.lakeReflection && !QualityTiers.IsDisabled("LakeReflection");
            _probe.resolution = _realtime ? 256 : 128;
            _renderId = _probe.RenderProbe();
        }

        void Update()
        {
            if (!_realtime || Time.frameCount % ultraIntervalFrames != 0) return;
            if (_renderId < 0 || _probe.IsFinishedRendering(_renderId)) _renderId = _probe.RenderProbe();
        }
    }
}
