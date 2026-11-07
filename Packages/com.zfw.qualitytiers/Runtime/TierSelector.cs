using System;
using System.Collections.Generic;

namespace Zfw.QualityTiers
{
    /// <summary>
    /// Pure decision logic for auto-selection (FR-QT-02), with no Unity calls so it can be unit-tested.
    /// </summary>
    public static class TierSelector
    {
        // GPU frame-time thresholds measured at High in the furnace hall (the heaviest zone).
        public const float UltraMs = 11f, HighMs = 14f, MediumMs = 20f;
        public const long LowVramMB = 4096;

        /// <summary>Upper bound from hardware facts alone, before any benchmark.</summary>
        public static Tier CapFromHardware(long vramMB, bool discreteGpu)
        {
            if (!discreteGpu) return Tier.Low;
            if (vramMB > 0 && vramMB < LowVramMB) return Tier.Medium;
            return Tier.Ultra;
        }

        public static Tier FromBenchmark(float gpuMs)
        {
            if (gpuMs <= 0 || float.IsNaN(gpuMs)) return Tier.Medium;  // no GPU timing -> safe middle
            if (gpuMs <= UltraMs) return Tier.Ultra;
            if (gpuMs <= HighMs) return Tier.High;
            if (gpuMs <= MediumMs) return Tier.Medium;
            return Tier.Low;
        }

        public static Tier Min(Tier a, Tier b) => (Tier)Math.Min((int)a, (int)b);

        /// <summary>
        /// Discrete-GPU guess from the vendor id. Apple silicon is integrated, but its unified memory and
        /// bandwidth put it with discrete parts, so it is treated as "discrete" for capping purposes.
        /// </summary>
        public static bool IsDiscreteLike(int vendorId, string gpuName)
        {
            const int NVIDIA = 0x10DE, AMD = 0x1002, Intel = 0x8086, Apple = 0x106B;
            if (vendorId == NVIDIA || vendorId == Apple) return true;
            string n = gpuName ?? "";
            if (vendorId == AMD) return n.IndexOf("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) < 0
                                     && n.IndexOf("Vega", StringComparison.OrdinalIgnoreCase) < 0;
            if (vendorId == Intel) return n.IndexOf("Arc", StringComparison.OrdinalIgnoreCase) >= 0;
            return n.StartsWith("Apple", StringComparison.Ordinal);
        }

        /// <summary>
        /// True when the saved choice no longer applies: GPU or driver changed since it was made.
        /// </summary>
        public static bool NeedsReselect(string savedGpu, string savedDriver, string gpu, string driver)
            => string.IsNullOrEmpty(savedGpu) || savedGpu != gpu || savedDriver != driver;
    }
}
