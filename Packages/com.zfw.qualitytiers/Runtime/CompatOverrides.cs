using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zfw.QualityTiers
{
    /// <summary>
    /// compat_overrides.json (FR-QT-03): turn off or cap features for a given API + vendor (+ driver range).
    /// Every rule must cite a case id from docs/cases.
    /// </summary>
    [Serializable]
    public class CompatRule
    {
        public string api;          // "Vulkan", "Direct3D12", "Metal"; empty = any
        public string vendor;       // "NVIDIA", "AMD", "Intel", "Apple"; empty = any
        public string driverMin;    // inclusive, dotted numeric; empty = no bound
        public string driverMax;    // inclusive
        public string[] disable;    // feature names: "HeatHaze", "SoftParticles", "Bloom", "Fog", "LakeReflection"
        public string maxTier;      // optional cap: "Low" | "Medium" | "High"
        public string @case;        // e.g. "CASE-07"
    }

    [Serializable]
    public class CompatTable { public CompatRule[] rules = Array.Empty<CompatRule>(); }

    public static class CompatOverrides
    {
        public static CompatTable Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new CompatTable();
            string t = json.TrimStart();
            // Allow a bare array in the file; JsonUtility needs an object root.
            if (t.StartsWith("[")) json = "{\"rules\":" + json + "}";
            return JsonUtility.FromJson<CompatTable>(json) ?? new CompatTable();
        }

        public static string VendorName(int id) => id switch
        {
            0x10DE => "NVIDIA", 0x1002 => "AMD", 0x8086 => "Intel", 0x106B => "Apple", _ => "Other",
        };

        public static IEnumerable<CompatRule> Matching(CompatTable table, string api, int vendorId, string driver)
        {
            string vendor = VendorName(vendorId);
            foreach (var r in table.rules)
            {
                if (!string.IsNullOrEmpty(r.api) && !string.Equals(r.api, api, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(r.vendor) && !string.Equals(r.vendor, vendor, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(r.driverMin) && CompareVersion(driver, r.driverMin) < 0) continue;
                if (!string.IsNullOrEmpty(r.driverMax) && CompareVersion(driver, r.driverMax) > 0) continue;
                yield return r;
            }
        }

        /// <summary>Numeric, component-wise: "566.36" &lt; "572.16"; missing parts count as 0.</summary>
        public static int CompareVersion(string a, string b)
        {
            string[] pa = (a ?? "").Split('.'), pb = (b ?? "").Split('.');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                long x = i < pa.Length && long.TryParse(pa[i], out var vx) ? vx : 0;
                long y = i < pb.Length && long.TryParse(pb[i], out var vy) ? vy : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
