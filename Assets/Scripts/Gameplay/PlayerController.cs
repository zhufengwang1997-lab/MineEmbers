using UnityEngine;
using UnityEngine.InputSystem;

namespace MineEmbers.Gameplay
{
    /// <summary>
    /// Minimal third-person controller (placeholder until Starter Assets): WASD/left stick to move,
    /// mouse/right stick to orbit, Shift to run, Space to jump. Inputs are bound in code so the
    /// prototype has no asset dependency.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 3.5f, runSpeed = 7f, jumpHeight = 1.1f, gravity = -18f;
        public Transform cameraRig;

        CharacterController _cc;
        InputAction _move, _run, _jump;
        float _vy;

        public static bool InputBlocked;  // settings menu / perf run / screenshots

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                 .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");
            _run = new InputAction("Run", binding: "<Keyboard>/leftShift"); _run.AddBinding("<Gamepad>/leftStickPress");
            _jump = new InputAction("Jump", binding: "<Keyboard>/space"); _jump.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { _move.Enable(); _run.Enable(); _jump.Enable(); }
        void OnDisable() { _move.Disable(); _run.Disable(); _jump.Disable(); }

        void Update()
        {
            Vector2 m = InputBlocked ? Vector2.zero : _move.ReadValue<Vector2>();
            Transform basis = cameraRig ? cameraRig : transform;
            Vector3 fwd = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = Vector3.ClampMagnitude(fwd * m.y + right * m.x, 1f);
            float speed = !InputBlocked && _run.IsPressed() ? runSpeed : walkSpeed;

            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);

            if (_cc.isGrounded)
            {
                _vy = -1f;
                if (!InputBlocked && _jump.WasPressedThisFrame()) _vy = Mathf.Sqrt(-2f * gravity * jumpHeight);
            }
            _vy += gravity * Time.deltaTime;
            _cc.Move((dir * speed + Vector3.up * _vy) * Time.deltaTime);
        }
    }
}
