using UnityEngine;

namespace MineEmbers.Rendering
{
    /// <summary>A flickering fire light. Registered with <see cref="TierLightManager"/> for shadow budgeting.</summary>
    [RequireComponent(typeof(Light))]
    public class TorchLight : MonoBehaviour
    {
        /// <summary>When true (screenshot mode), flicker is a pure function of a fixed time -> reproducible frames.</summary>
        public static bool Frozen;
        public static float FrozenTime = 1f;

        [Range(0f, 1f)] public float flicker = 0.25f;
        public float speed = 6f;
        [Tooltip("Higher = more likely to keep a shadow when the budget is tight (big braziers, furnace).")]
        public float shadowPriority = 1f;

        public Light Light { get; private set; }
        float _base, _seed;

        void Awake()
        {
            Light = GetComponent<Light>();
            _base = Light.intensity;
            _seed = Mathf.Abs(transform.position.x * 13.1f + transform.position.z * 7.7f) % 100f;
        }

        void OnEnable() => TierLightManager.Register(this);
        void OnDisable() => TierLightManager.Unregister(this);

        void Update()
        {
            float t = (Frozen ? FrozenTime : Time.time) * speed + _seed;
            float n = Mathf.PerlinNoise(t, _seed) * 0.7f + Mathf.PerlinNoise(t * 2.3f, _seed + 5f) * 0.3f;
            Light.intensity = _base * (1f - flicker + flicker * 2f * n);
        }
    }
}
