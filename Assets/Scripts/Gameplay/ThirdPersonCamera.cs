using UnityEngine;
using UnityEngine.InputSystem;

namespace MineEmbers.Gameplay
{
    /// <summary>Orbit camera with wall collision (sphere cast) so it never clips into tunnel walls.</summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 pivotOffset = new Vector3(0, 1.6f, 0);
        public float distance = 4.5f, minPitch = -30f, maxPitch = 70f, sensitivity = 0.12f;
        public LayerMask collision = ~0;

        float _yaw, _pitch = 12f;
        InputAction _look;

        void Awake()
        {
            _look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
            _look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");
        }

        void OnEnable() { _look.Enable(); if (target) _yaw = target.eulerAngles.y; }
        void OnDisable() => _look.Disable();

        void LateUpdate()
        {
            if (!target || Zfw.PerfRunner.PerfRunner.IsRunning || Boot.ScreenshotMode.IsRunning) return;
            if (!PlayerController.InputBlocked)
            {
                Vector2 d = _look.ReadValue<Vector2>() * sensitivity;
                _yaw += d.x; _pitch = Mathf.Clamp(_pitch - d.y, minPitch, maxPitch);
            }
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0);
            Vector3 pivot = target.position + pivotOffset;
            float dist = distance;
            if (Physics.SphereCast(pivot, 0.25f, rot * Vector3.back, out var hit, distance, collision, QueryTriggerInteraction.Ignore))
                dist = Mathf.Max(0.5f, hit.distance - 0.1f);
            transform.SetPositionAndRotation(pivot + rot * Vector3.back * dist, rot);
        }
    }
}
