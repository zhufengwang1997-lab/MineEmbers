using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Zfw.GpuCaps;

namespace Zfw.QualityTiers
{
    /// <summary>
    /// Single source of truth for the active tier. Game code reads <see cref="Settings"/> and listens to
    /// <see cref="TierChanged"/>; nothing else should touch QualitySettings / the pipeline asset.
    /// </summary>
    public static class QualityTiers
    {
        const string KeyTier = "zfw.tier", KeyMode = "zfw.tier.mode", KeyGpu = "zfw.tier.gpu", KeyDriver = "zfw.tier.driver";

        static TierCatalog _catalog;
        static Tier _current = Tier.High;
        static bool _initialized;

        public static Tier Current => _current;
        public static SelectionMode Mode { get; private set; } = SelectionMode.Auto;
        public static TierSettings Settings => Catalog ? Catalog.Get(_current) : null;
        /// <summary>Features disabled by compat_overrides.json for this device (e.g. "HeatHaze").</summary>
        public static string[] Disabled { get; private set; } = Array.Empty<string>();
        /// <summary>Case ids of the compat rules that matched, for the settings screen / logs.</summary>
        public static string[] AppliedCases { get; private set; } = Array.Empty<string>();

        public static event Action<Tier> TierChanged;

        public static TierCatalog Catalog
        {
            get
            {
                if (!_catalog) _catalog = Resources.Load<TierCatalog>("TierCatalog");
                return _catalog;
            }
            set => _catalog = value;
        }

        public static bool IsDisabled(string feature) => Array.IndexOf(Disabled, feature) >= 0;

        /// <summary>Switch tier now. Manual choices are persisted and win over auto-selection.</summary>
        public static void Set(Tier t, bool manual)
        {
            Tier capped = ApplyCompatCap(t);
            Mode = manual ? SelectionMode.Manual : Mode == SelectionMode.CommandLine ? SelectionMode.CommandLine : SelectionMode.Auto;
            bool changed = !_initialized || capped != _current;
            _current = capped;
            _initialized = true;
            Apply();
            if (manual) Save();
            if (changed) TierChanged?.Invoke(_current);
        }

        /// <summary>Called after the auto benchmark (or on start with a still-valid saved result).</summary>
        public static void SetAuto(Tier t)
        {
            Mode = SelectionMode.Auto;
            Set(t, manual: false);
            Save();
        }

        internal static void SetFromCommandLine(Tier t)
        {
            Mode = SelectionMode.CommandLine;
            Set(t, manual: false);
        }

        /// <summary>Saved tier if still valid for this GPU + driver; null means "run the benchmark".</summary>
        public static Tier? LoadSaved(out SelectionMode mode)
        {
            mode = (SelectionMode)PlayerPrefs.GetInt(KeyMode, (int)SelectionMode.Auto);
            var caps = GpuCaps.GpuCaps.Current;
            if (TierSelector.NeedsReselect(PlayerPrefs.GetString(KeyGpu, ""), PlayerPrefs.GetString(KeyDriver, ""),
                                           caps.gpu.name, caps.driver.version))
                return null;
            return PlayerPrefs.HasKey(KeyTier) ? (Tier)PlayerPrefs.GetInt(KeyTier) : (Tier?)null;
        }

        static void Save()
        {
            var caps = GpuCaps.GpuCaps.Current;
            PlayerPrefs.SetInt(KeyTier, (int)_current);
            PlayerPrefs.SetInt(KeyMode, (int)Mode);
            PlayerPrefs.SetString(KeyGpu, caps.gpu.name ?? "");
            PlayerPrefs.SetString(KeyDriver, caps.driver.version ?? "");
            PlayerPrefs.Save();
        }

        public static void ClearSaved()
        {
            foreach (var k in new[] { KeyTier, KeyMode, KeyGpu, KeyDriver }) PlayerPrefs.DeleteKey(k);
        }

        static void Apply()
        {
            var s = Settings;
            if (!s) { Debug.LogWarning("[QualityTiers] no TierCatalog in Resources; tier is logical only"); return; }
            if (s.pipeline)
            {
                // Both are needed: QualitySettings wins over GraphicsSettings when set.
                QualitySettings.renderPipeline = s.pipeline;
            }
            Debug.Log($"[QualityTiers] tier={_current} mode={Mode} pipeline={(s.pipeline ? s.pipeline.name : "-")} " +
                      $"disabled=[{string.Join(",", Disabled)}]");
        }

        // ---- compat overrides ----

        static Tier? _compatCap;

        /// <summary>Load compat_overrides.json from StreamingAssets and evaluate it against the current device.</summary>
        public static void LoadCompat(string json = null)
        {
            if (json == null)
            {
                string path = Path.Combine(Application.streamingAssetsPath, "compat_overrides.json");
                json = File.Exists(path) ? File.ReadAllText(path) : "";
            }
            var caps = GpuCaps.GpuCaps.Current;
            var disabled = new System.Collections.Generic.List<string>();
            var cases = new System.Collections.Generic.List<string>();
            _compatCap = null;
            foreach (var r in CompatOverrides.Matching(CompatOverrides.Parse(json), caps.api, caps.gpu.vendorId, caps.driver.version))
            {
                if (r.disable != null) disabled.AddRange(r.disable);
                if (!string.IsNullOrEmpty(r.maxTier) && Enum.TryParse(r.maxTier, true, out Tier cap))
                    _compatCap = _compatCap.HasValue ? TierSelector.Min(_compatCap.Value, cap) : cap;
                if (!string.IsNullOrEmpty(r.@case)) cases.Add(r.@case);
            }
            Disabled = disabled.ToArray();
            AppliedCases = cases.ToArray();
            if (cases.Count > 0) Debug.Log($"[QualityTiers] compat rules applied: {string.Join(", ", cases)}");
        }

        static Tier ApplyCompatCap(Tier t) => _compatCap.HasValue ? TierSelector.Min(t, _compatCap.Value) : t;
    }
}
