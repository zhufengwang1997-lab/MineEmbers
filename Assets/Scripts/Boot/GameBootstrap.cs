using System;
using System.Collections;
using UnityEngine;
using Zfw.QualityTiers;
using PR = Zfw.PerfRunner.PerfRunner;

namespace MineEmbers.Boot
{
    /// <summary>
    /// Start-up order: compat table -> tier (command line / saved / benchmark) -> PSO warmup -> mode
    /// (perf run, screenshots or play). Lives in the level scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Run the auto benchmark in the editor too (normally only in players).")]
        public bool benchmarkInEditor;

        public static bool Ready { get; private set; }

        IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            QualityTiers.LoadCompat();
            ShaderWarmup.StartRecordingIfRequested(args);

            bool automation = Array.IndexOf(args, "-perfrun") >= 0 || Array.IndexOf(args, "-shots") >= 0;
            string tierArg = Array.Find(args, a => a.StartsWith("-tier=", StringComparison.Ordinal));
            if (tierArg != null && Enum.TryParse(tierArg.Substring(6), true, out Tier forced))
            {
                QualityTiers.SetCommandLine(forced);
            }
            else if (QualityTiers.LoadSaved(out var mode) is Tier saved)
            {
                QualityTiers.Set(saved, manual: mode == SelectionMode.Manual);
                Debug.Log($"[Boot] saved tier {saved} ({mode}) still valid for this GPU/driver");
            }
            else if (automation || (Application.isEditor && !benchmarkInEditor))
            {
                QualityTiers.Set(Tier.High, manual: false);
            }
            else
            {
                yield return TierBenchmark.Run();
            }

            ShaderWarmup.WarmUpIfAvailable();
            PR.Started += () => Gameplay.PlayerController.InputBlocked = true;
            PR.Finished += _ => ShaderWarmup.StopRecording();
            ScreenshotMode.TryStart(args);
            Ready = true;
        }
    }
}
