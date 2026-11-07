using System;
using UnityEngine;

namespace Zfw.GpuCaps
{
    // Handles the command-line flags (SRS FR-CAP-04):
    //   -caps-out <dir>   where to write caps_<device>_<api>.json (default: persistentDataPath)
    //   -caps-only        write the report and quit immediately (batch collection)
    static class GpuCapsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnLoad()
        {
            string[] args = Environment.GetCommandLineArgs();
            string outDir = Arg(args, "-caps-out");
            bool only = Array.IndexOf(args, "-caps-only") >= 0;

            var r = GpuCaps.Current;
            Debug.Log($"[GpuCaps] {r.api} | {r.gpu.name} | driver {r.driver.version} | {r.gpu.dedicatedMemoryMB} MB | " +
                      $"bindless={r.features.bindless} rt={r.features.raytracing} vrs={r.features.vrs} " +
                      $"mesh={r.features.meshShader} sm={r.features.shaderModel} (source={r.source})");

            if (Application.isEditor && outDir == null) return;  // don't litter persistentDataPath on every Play
            try
            {
                string path = GpuCaps.Write(r, outDir);
                Debug.Log($"[GpuCaps] report -> {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GpuCaps] cannot write report: {e.Message}");
            }
            if (only && !Application.isEditor) Application.Quit(0);
        }

        internal static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
