using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using MineEmbers.Rendering;

namespace MineEmbers.Boot
{
    /// <summary>
    /// -shots &lt;dir&gt;: visit every ShotPoint, freeze time-dependent effects, capture PNG, quit (FR-PD-01).
    /// Determinism: fixed particle seeds + fixed simulation time, frozen torch flicker, fixed haze time.
    /// </summary>
    public class ScreenshotMode : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }
        public const float SimTime = 2.0f;

        string _dir;

        public static bool TryStart(string[] args)
        {
            int i = Array.IndexOf(args, "-shots");
            if (i < 0 || i + 1 >= args.Length) return false;
            var go = new GameObject("[ScreenshotMode]");
            go.AddComponent<ScreenshotMode>()._dir = args[i + 1];
            return true;
        }

        IEnumerator Start()
        {
            IsRunning = true;
            Gameplay.PlayerController.InputBlocked = true;
            TorchLight.Frozen = true;
            HeatHazeFeature.TimeOverride = SimTime;
            Directory.CreateDirectory(_dir);
            var cam = Camera.main;
            // Hide the player so shots depend only on the level and the renderer.
            var player = FindFirstObjectByType<Gameplay.PlayerController>();
            if (player) foreach (var r in player.GetComponentsInChildren<Renderer>()) r.enabled = false;
            cam.transform.SetParent(null, true);
            var particles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
            var shots = FindObjectsByType<ShotPoint>(FindObjectsSortMode.None).OrderBy(s => s.shotId).ToArray();
            Debug.Log($"[Shots] {shots.Length} shot points -> {_dir}");

            // Let tier switching, reflection probes and shader warmup settle.
            for (int f = 0; f < 30; f++) yield return null;

            foreach (var s in shots)
            {
                cam.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                foreach (var ps in particles)
                {
                    if (!ps.main.loop && !ps.isPlaying) continue;
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = 1234u + (uint)(Mathf.Abs(ps.transform.position.GetHashCode()) % 1000);
                    ps.Simulate(SimTime, true, true, true);  // deterministic, then paused
                }
                for (int f = 0; f < 3; f++) yield return null;  // lights + shadows update, TAA-free so 3 is enough
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(_dir, s.shotId + ".png"), tex.EncodeToPNG());
                Destroy(tex);
            }
            Debug.Log($"[Shots] done ({shots.Length})");
            IsRunning = false;
            if (!Application.isEditor) Application.Quit(0);
        }
    }
}
