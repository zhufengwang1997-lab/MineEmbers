using System;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MineEmbers.Boot
{
    /// <summary>
    /// PSO warmup with GraphicsStateCollection (FR-SH-01).
    ///
    /// Why: D3D12/Vulkan/Metal compile a full pipeline state object (shader + blend + vertex layout +
    /// render target formats) the first time a draw needs it. That is the 50-300 ms hitch when a new
    /// effect first appears. A GraphicsStateCollection records which states were actually used during a
    /// run (-gsc-record), and WarmUp() pre-creates them behind the loading screen next time.
    /// Files are per graphics API, because PSOs are API-specific.
    /// </summary>
    public static class ShaderWarmup
    {
        static GraphicsStateCollection _trace;

        public static string FileName => $"gsc_{SystemInfo.graphicsDeviceType}.graphicsstate";

        /// <summary>
        /// Warm up every collection recorded for this API (one file per tier: gsc_Metal_high.graphicsstate ...),
        /// because each tier's URP asset (MSAA, depth/opaque textures) produces different PSOs.
        /// </summary>
        public static void WarmUpIfAvailable()
        {
            string dir = Application.streamingAssetsPath;
            string[] files = Directory.Exists(dir) ? Directory.GetFiles(dir, $"gsc_{SystemInfo.graphicsDeviceType}_*.graphicsstate") : new string[0];
            if (files.Length == 0) { Debug.Log($"[Warmup] no gsc_{SystemInfo.graphicsDeviceType}_*.graphicsstate; skipping PSO warmup"); return; }
            float t0 = Time.realtimeSinceStartup;
            int states = 0;
            foreach (var path in files)
            {
                var gsc = new GraphicsStateCollection();
                if (!gsc.LoadFromFile(path)) { Debug.LogWarning($"[Warmup] cannot load {path}"); continue; }
                gsc.WarmUp().Complete();
                states += gsc.totalGraphicsStateCount;
            }
            Debug.Log($"[Warmup] {files.Length} collections, {states} states warmed in {(Time.realtimeSinceStartup - t0) * 1000f:0} ms");
        }

        /// <summary>-gsc-record: trace every PSO used until <see cref="StopRecording"/>.</summary>
        public static bool StartRecordingIfRequested(string[] args)
        {
            if (Array.IndexOf(args, "-gsc-record") < 0) return false;
            _trace = new GraphicsStateCollection();
            bool ok = _trace.BeginTrace();
            Debug.Log($"[Warmup] tracing graphics states: {(ok ? "started" : "FAILED to start")} (dev build: {Debug.isDebugBuild})");
            return true;
        }

        public static void StopRecording()
        {
            if (_trace == null || !_trace.isTracing) return;
            _trace.EndTrace();
            string path = Path.Combine(Application.persistentDataPath, FileName);
            bool ok = _trace.SaveToFile(path);
            Debug.Log($"[Warmup] saved {_trace.totalGraphicsStateCount} states / {_trace.variantCount} variants -> {path} ({(ok ? "ok" : "FAILED")})");
        }
    }
}
