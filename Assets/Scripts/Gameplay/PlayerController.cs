using UnityEngine;
using UnityEngine.InputSystem;

namespace MineEmbers.Gameplay
{
    /// <summary>
    /// Minimal third-person controller: WASD/left stick to move, mouse/right stick to orbit, Shift to run,
    /// Space to jump. Inputs are bound in code (no asset dependency). If an Animator is assigned (the
    /// Starter Assets armature), it drives that controller's parameters: Speed, MotionSpeed, Grounded,
    /// Jump, FreeFall.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 3.5f, runSpeed = 7f, jumpHeight = 1.1f, gravity = -18f;
        public Transform cameraRig;
        public Animator animator;

        static readonly int SpeedId = Animator.StringToHash("Speed"), MotionSpeedId = Animator.StringToHash("MotionSpeed"),
            GroundedId = Animator.StringToHash("Grounded"), JumpId = Animator.StringToHash("Jump"), FreeFallId = Animator.StringToHash("FreeFall");

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

            bool grounded = _cc.isGrounded, jumped = false;
            if (grounded)
            {
                _vy = -1f;
                if (!InputBlocked && _jump.WasPressedThisFrame()) { _vy = Mathf.Sqrt(-2f * gravity * jumpHeight); jumped = true; }
            }
            _vy += gravity * Time.deltaTime;
            _cc.Move((dir * speed + Vector3.up * _vy) * Time.deltaTime);

            if (animator)
            {
                Vector3 v = _cc.velocity; v.y = 0;
                animator.SetFloat(SpeedId, v.magnitude, 0.1f, Time.deltaTime);
                animator.SetFloat(MotionSpeedId, 1f);
                animator.SetBool(GroundedId, grounded);
                animator.SetBool(JumpId, jumped);
                animator.SetBool(FreeFallId, !grounded && _vy < -2f);
            }
        }
    }
}
