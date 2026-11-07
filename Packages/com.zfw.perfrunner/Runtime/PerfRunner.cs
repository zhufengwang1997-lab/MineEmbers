using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Zfw.GpuCaps;

namespace Zfw.PerfRunner
{
    /// <summary>
    /// -perfrun [-perf-out dir] [-perf-seconds 20] [-perf-warmup 3]
    /// Flies the main camera through every <see cref="PerfZone"/>, records one CSV row per frame, then quits.
    /// Requires Player Settings > Frame Timing Stats for gpu_ms (otherwise the column is empty).
    /// </summary>
    public class PerfRunner : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }
        /// <summary>Raised before capture starts so the game can disable player input, AI, etc.</summary>
        public static event Action Started;
        /// <summary>Path of the CSV once written.</summary>
        public static event Action<string> Finished;

        public float secondsPerZone = 20f;
        public float warmupSeconds = 3f;
        public string outDir;
        public bool quitWhenDone = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-perfrun") < 0) return;
            var go = new GameObject("[PerfRunner]");
            DontDestroyOnLoad(go);
            var r = go.AddComponent<PerfRunner>();
            r.outDir = Arg(args, "-perf-out");
            if (float.TryParse(Arg(args, "-perf-seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) r.secondsPerZone = s;
            if (float.TryParse(Arg(args, "-perf-warmup"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) r.warmupSeconds = w;
        }

        static string Arg(string[] a, string n) { int i = Array.IndexOf(a, n); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }

        ProfilerRecorder _draws, _setpass, _gfxMem;
        readonly FrameTiming[] _ft = new FrameTiming[1];

        IEnumerator Start()
        {
            IsRunning = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;  // measure the GPU, not the display

            _draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setpass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _gfxMem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory");

            yield return null;  // let the scene's own Start() run
            var zones = FindObjectsByType<PerfZone>(FindObjectsSortMode.None).OrderBy(z => z.order).ToArray();
            var cam = Camera.main;
            if (zones.Length == 0 || !cam)
            {
                Debug.LogError($"[PerfRunner] need PerfZone objects ({zones.Length}) and a MainCamera ({(cam ? "ok" : "missing")})");
                Finish(null);
                yield break;
            }
            Started?.Invoke();
            DetachCamera(cam);

            var sb = new StringBuilder(1 << 20);
            sb.Append(Header()).Append('\n');
            sb.Append("frame,zone,cpu_ms,gpu_ms,draw_calls,setpass_calls,gfx_mem_mb\n");
            int frame = 0;

            foreach (var z in zones)
            {
                // Warmup: sit at the zone start so streaming, shader compiles and particle spin-up settle.
                for (float t = 0; t < warmupSeconds; t += Time.unscaledDeltaTime)
                {
                    z.Evaluate(0, out var p, out var q); cam.transform.SetPositionAndRotation(p, q);
                    FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                }
                for (float t = 0; t < secondsPerZone; t += Time.unscaledDeltaTime)
                {
                    z.Evaluate(t / secondsPerZone, out var p, out var q); cam.transform.SetPositionAndRotation(p, q);
                    FrameTimingManager.CaptureFrameTimings();
                    // Timings lag a few frames behind (the GPU finishes later); GetLatestTimings returns
                    // the newest completed frame, which is fine for per-zone statistics.
                    string cpu = "", gpu = "";
                    if (FrameTimingManager.GetLatestTimings(1, _ft) > 0)
                    {
                        cpu = F(_ft[0].cpuFrameTime);
                        if (_ft[0].gpuFrameTime > 0) gpu = F(_ft[0].gpuFrameTime);
                    }
                    if (cpu.Length == 0) cpu = F(Time.unscaledDeltaTime * 1000.0);
                    sb.Append(frame++).Append(',').Append(z.zoneId).Append(',').Append(cpu).Append(',').Append(gpu).Append(',')
                      .Append(_draws.LastValue).Append(',').Append(_setpass.LastValue).Append(',')
                      .Append((_gfxMem.LastValue / (1024 * 1024)).ToString(CultureInfo.InvariantCulture)).Append('\n');
                    yield return null;
                }
                Debug.Log($"[PerfRunner] zone {z.zoneId} done");
            }

            string path = Write(sb.ToString());
            Finish(path);
        }

        static void DetachCamera(Camera cam)
        {
            // Stop anything else from driving the camera (Cinemachine brain, follow scripts).
            foreach (var b in cam.GetComponents<Behaviour>())
                if (b != cam && b.GetType().Name.Contains("Brain")) b.enabled = false;
            cam.transform.SetParent(null, true);
        }

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Commit()
        {
            var ta = Resources.Load<TextAsset>("build_commit");  // written by the build script
            return ta ? ta.text.Trim() : "dev";
        }

        static string Tier()
        {
            var args = Environment.GetCommandLineArgs();
            var t = args.FirstOrDefault(a => a.StartsWith("-tier=", StringComparison.Ordinal));
            return t != null ? t.Substring(6).ToLowerInvariant() : "auto";
        }

        static string Header()
        {
            var caps = GpuCaps.GpuCaps.Current;
            return $"# commit={Commit()} api={SystemInfo.graphicsDeviceType} device=\"{caps.gpu.name}\" tier={Tier()} " +
                   $"unity={Application.unityVersion} date={DateTime.Now:yyyy-MM-ddTHH:mm}";
        }

        string Write(string csv)
        {
            string dir = string.IsNullOrEmpty(outDir) ? Application.persistentDataPath : outDir;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"perf_{Commit()}_{SystemInfo.graphicsDeviceType}_{Tier()}.csv");
            File.WriteAllText(path, csv);
            Debug.Log($"[PerfRunner] wrote {path}");
            return path;
        }

        void Finish(string path)
        {
            IsRunning = false;
            _draws.Dispose(); _setpass.Dispose(); _gfxMem.Dispose();
            Finished?.Invoke(path);
            if (quitWhenDone && !Application.isEditor) Application.Quit(path != null ? 0 : 1);
        }
    }
}
