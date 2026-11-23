using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zfw.GpuCaps
{
    /// <summary>
    /// Entry point: <c>GpuCaps.Current</c>. The native plugin collects the report when Unity creates the
    /// graphics device (before any script runs); here we read it once, add SystemInfo data, and cache it.
    /// </summary>
    public static class GpuCaps
    {
        // "GpuCaps" resolves to GpuCaps.bundle on macOS and GpuCaps.dll on Windows.
        const string Lib = "GpuCaps";

        [DllImport(Lib)] static extern IntPtr GpuCaps_GetReportJson();
        [DllImport(Lib)] static extern int GpuCaps_GetRendererId();

        static GpuCapsReport _current;

        public static GpuCapsReport Current => _current ??= Collect();

        /// <summary>Re-query (e.g. after a device reset). Normally not needed.</summary>
        public static GpuCapsReport Refresh() => _current = Collect();

        static GpuCapsReport Collect()
        {
            GpuCapsReport r = null;
            try
            {
                IntPtr p = GpuCaps_GetReportJson();
                string json = p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
                if (!string.IsNullOrEmpty(json))
                {
                    r = JsonUtility.FromJson<GpuCapsReport>(json);
                    r.source = "native";
                }
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                Debug.LogWarning($"[GpuCaps] native plugin unavailable ({e.GetType().Name}); using SystemInfo only");
            }

            // No plugin, or an API the plugin does not handle (e.g. D3D11 / OpenGL in the editor).
            if (r == null || string.IsNullOrEmpty(r.api))
                r = FromSystemInfo(r?.error);

            FillUnity(r);
            if (string.IsNullOrEmpty(r.os)) r.os = SystemInfo.operatingSystem;
            FillUpscalers(r);
            return r;
        }

        static GpuCapsReport FromSystemInfo(string nativeError)
        {
            var r = new GpuCapsReport
            {
                api = SystemInfo.graphicsDeviceType.ToString(),
                source = "systeminfo",
                error = nativeError,
            };
            r.gpu.name = SystemInfo.graphicsDeviceName;
            r.gpu.vendorId = SystemInfo.graphicsDeviceVendorID;
            r.gpu.dedicatedMemoryMB = SystemInfo.graphicsMemorySize;
            r.driver.version = SystemInfo.graphicsDeviceVersion;
            r.features.bindless = new Feature(Feature.Unsupported, "unknown (no native plugin)");
            r.features.raytracing = Feature.From(SystemInfo.supportsRayTracing, "SystemInfo.supportsRayTracing");
            r.features.vrs = Feature.From(ShadingRateInfo.supportsPerImageTile, "ShadingRateInfo");
            r.features.meshShader = new Feature(Feature.Unsupported, "unknown (no native plugin)");
            r.features.shaderModel = new Feature($"shaderLevel {SystemInfo.graphicsShaderLevel}", "SystemInfo");
            return r;
        }

        /// <summary>FR-UP-05 (schema 2): which upscaler families this device can run, from hardware/OS facts.</summary>
        static void FillUpscalers(GpuCapsReport r)
        {
            var api = SystemInfo.graphicsDeviceType;
            string name = SystemInfo.graphicsDeviceName ?? "";
            bool windows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
            bool mac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
            bool nvidia = SystemInfo.graphicsDeviceVendorID == Vendors.NVIDIA;
            bool rtx = nvidia && Regex.IsMatch(name, @"\bRTX\b", RegexOptions.IgnoreCase);
            bool dxOrVk = api == GraphicsDeviceType.Direct3D12 || api == GraphicsDeviceType.Direct3D11 || api == GraphicsDeviceType.Vulkan;
            r.features.dlss = Feature.From(windows && rtx && dxOrVk, $"heuristic: Windows={windows} RTX={rtx} api={api}; NVIDIA module not queried here");
            // FSR 1 is a pair of pixel shaders (EASU + RCAS): any GPU Unity renders on
            r.features.fsr = new Feature(Feature.Supported, "FSR1 (shader-only, URP built-in); FSR2/3 not checked");
            bool appleGpu = SystemInfo.graphicsDeviceVendorID == Vendors.Apple || name.StartsWith("Apple", StringComparison.OrdinalIgnoreCase);
            r.features.metalfx = Feature.From(mac && api == GraphicsDeviceType.Metal && appleGpu, $"heuristic: macOS={mac} Metal={api == GraphicsDeviceType.Metal} AppleGPU={appleGpu} (MetalFX needs macOS 13+)");
        }

        static void FillUnity(GpuCapsReport r)
        {
            var u = r.unity;
            u.graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString();
            u.graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion;
            u.graphicsShaderLevel = SystemInfo.graphicsShaderLevel;
            u.graphicsMemorySize = SystemInfo.graphicsMemorySize;
            u.maxTextureSize = SystemInfo.maxTextureSize;
            u.supportsComputeShaders = SystemInfo.supportsComputeShaders;
            u.supportsRayTracing = SystemInfo.supportsRayTracing;
            u.unityVersion = Application.unityVersion;
        }

        /// <summary>"NVIDIA GeForce GTX 1070 Ti" -> "GTX1070Ti", "Apple M5 Max" -> "AppleM5Max".</summary>
        public static string ShortDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            string s = Regex.Replace(name, @"\(TM\)|\(R\)", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\b(NVIDIA|GeForce|AMD|Radeon|Intel|Graphics)\b", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "[^A-Za-z0-9]", "");
            return s.Length > 0 ? s : "unknown";
        }

        public static string FileName(GpuCapsReport r) => $"caps_{ShortDeviceName(r.gpu.name)}_{r.api}.json";

        /// <summary>Write the report; returns the path. Directory defaults to persistentDataPath.</summary>
        public static string Write(GpuCapsReport r, string dir = null)
        {
            dir = string.IsNullOrEmpty(dir) ? Application.persistentDataPath : dir;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, FileName(r));
            File.WriteAllText(path, JsonUtility.ToJson(r, true));
            return path;
        }
    }
}
