using UnityEngine;
using UnityEngine.InputSystem;
using Zfw.GpuCaps;
using Zfw.QualityTiers;
using MineEmbers.Rendering;

namespace MineEmbers.Boot
{
    /// <summary>
    /// Esc: settings (FR-G-09) - manual tier, plus device / API / driver / tier source.
    /// F3: performance HUD (CPU/GPU ms from FrameTimingManager, shadowed lights).
    /// IMGUI on purpose: zero assets, works identically on every API, easy to strip.
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        bool _open, _hud = true;
        readonly FrameTiming[] _ft = new FrameTiming[1];
        double _cpu, _gpu;
        GUIStyle _box, _label, _button;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Toggle();
            if (kb != null && kb.f3Key.wasPressedThisFrame) _hud = !_hud;
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) Toggle();

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _ft) > 0)
            {
                _cpu = Lerp(_cpu, _ft[0].cpuFrameTime); _gpu = Lerp(_gpu, _ft[0].gpuFrameTime);
            }
        }

        static double Lerp(double a, double b) => a <= 0 ? b : a + (b - a) * 0.05;

        void Toggle()
        {
            _open = !_open;
            Gameplay.PlayerController.InputBlocked = _open;
            Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _open;
        }

        void Styles()
        {
            if (_box != null) return;
            float s = Mathf.Max(1f, Screen.height / 900f);
            _box = new GUIStyle(GUI.skin.box) { fontSize = (int)(14 * s), alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = (int)(15 * s) };
            _button = new GUIStyle(GUI.skin.button) { fontSize = (int)(16 * s), fixedHeight = 34 * s };
        }

        void OnGUI()
        {
            if (Zfw.PerfRunner.PerfRunner.IsRunning || ScreenshotMode.IsRunning) return;
            Styles();
            if (TierBenchmark.IsRunning)
            {
                GUI.Box(new Rect(Screen.width / 2f - 220, Screen.height - 90, 440, 50), TierBenchmark.Status, _box);
                return;
            }
            if (_hud)
            {
                string gpu = _gpu > 0 ? $"{_gpu:0.0} ms" : "n/a";
                GUI.Box(new Rect(10, 10, 330, 76),
                    $"CPU {_cpu:0.0} ms   GPU {gpu}\n{SystemInfo.graphicsDeviceType} · {QualityTiers.Current} ({QualityTiers.Mode})\n" +
                    $"shadowed lights {TierLightManager.ShadowedCount}   [Esc] settings  [F3] HUD", _box);
            }
            if (!_open) return;

            var caps = GpuCaps.Current;
            float w = Mathf.Min(560, Screen.width - 40), h = 470;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), GUI.skin.window);
            GUILayout.Label("Settings", _label);
            GUILayout.Space(6);
            GUILayout.Label("Quality tier", _label);
            GUILayout.BeginHorizontal();
            foreach (Tier t in System.Enum.GetValues(typeof(Tier)))
            {
                bool sel = QualityTiers.Current == t;
                if (GUILayout.Toggle(sel, t.ToString(), _button) && !sel) QualityTiers.Set(t, manual: true);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Re-detect automatically (restart)", _button)) { QualityTiers.ClearSaved(); }
            GUILayout.Space(10);
            GUILayout.Label($"Selection: {QualityTiers.Mode}", _label);
            GUILayout.Label($"Graphics API: {SystemInfo.graphicsDeviceType}  ({SystemInfo.graphicsDeviceVersion})", _label);
            GUILayout.Label($"GPU: {caps.gpu.name}  ·  {caps.gpu.dedicatedMemoryMB} MB", _label);
            GUILayout.Label($"Driver: {caps.driver.version}", _label);
            GUILayout.Label($"Ray tracing: {caps.features.raytracing}   VRS: {caps.features.vrs?.value}", _label);
            if (QualityTiers.AppliedCases.Length > 0)
                GUILayout.Label($"Compatibility rules: {string.Join(", ", QualityTiers.AppliedCases)} (disabled: {string.Join(", ", QualityTiers.Disabled)})", _label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Resume", _button)) Toggle();
            if (GUILayout.Button("Quit", _button)) Application.Quit();
            GUILayout.EndArea();
        }

        void Start() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }
}
