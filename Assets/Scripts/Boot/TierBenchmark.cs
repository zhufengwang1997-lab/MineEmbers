using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zfw.GpuCaps;
using Zfw.PerfRunner;
using Zfw.QualityTiers;

namespace MineEmbers.Boot
{
    /// <summary>
    /// Auto tier selection (FR-QT-02): hardware cap from the caps report, then a 5 s High-tier benchmark
    /// in the furnace hall (heaviest zone), result = min(benchmark, cap), compat overrides applied by
    /// QualityTiers.Set. The decision and its inputs are logged so a wrong pick can be diagnosed.
    /// </summary>
    public static class TierBenchmark
    {
        public static string Status { get; private set; } = "";
        public static bool IsRunning { get; private set; }

        public static IEnumerator Run(float warmup = 1f, float seconds = 5f)
        {
            IsRunning = true;
            var caps = GpuCaps.Current;
            bool discrete = TierSelector.IsDiscreteLike(caps.gpu.vendorId, caps.gpu.name);
            Tier cap = TierSelector.CapFromHardware(caps.gpu.dedicatedMemoryMB, discrete);

            var cam = Camera.main;
            var zone = Object.FindObjectsByType<PerfZone>(FindObjectsSortMode.None).FirstOrDefault(z => z.zoneId == "furnace");
            Vector3 oldPos = cam ? cam.transform.position : default;
            Quaternion oldRot = cam ? cam.transform.rotation : default;
            Transform oldParent = cam ? cam.transform.parent : null;

            QualityTiers.Set(Tier.High, manual: false);
            Gameplay.PlayerController.InputBlocked = true;
            var gpu = new List<double>(); var cpu = new List<double>();
            var ft = new FrameTiming[1];
            float t = 0, total = warmup + seconds;
            while (t < total)
            {
                if (cam && zone) { zone.Evaluate(Mathf.Clamp01((t - warmup) / seconds) * 0.5f, out var p, out var q); cam.transform.SetPositionAndRotation(p, q); }
                Status = $"Detecting graphics performance... {Mathf.CeilToInt(total - t)}";
                FrameTimingManager.CaptureFrameTimings();
                if (t > warmup && FrameTimingManager.GetLatestTimings(1, ft) > 0)
                {
                    if (ft[0].gpuFrameTime > 0) gpu.Add(ft[0].gpuFrameTime);
                    cpu.Add(ft[0].cpuFrameTime);
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            double ms = gpu.Count > 10 ? gpu.Average() : cpu.Count > 10 ? cpu.Average() : 0;
            string src = gpu.Count > 10 ? "gpu" : cpu.Count > 10 ? "cpu(fallback)" : "none";
            Tier bench = TierSelector.FromBenchmark((float)ms);
            Tier final = TierSelector.Min(bench, cap);
            Debug.Log($"[TierBenchmark] {caps.gpu.name} {caps.api} vram={caps.gpu.dedicatedMemoryMB}MB discrete={discrete} " +
                      $"cap={cap} | {src} mean={ms:0.00}ms over {Mathf.Max(gpu.Count, cpu.Count)} frames -> bench={bench} | final={final}");
            QualityTiers.SetAuto(final);

            if (cam) { cam.transform.SetParent(oldParent, false); cam.transform.SetPositionAndRotation(oldPos, oldRot); }
            Gameplay.PlayerController.InputBlocked = false;
            Status = "";
            IsRunning = false;
        }
    }
}
