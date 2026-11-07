using UnityEngine;

namespace MineEmbers.Rendering
{
    /// <summary>Fades heat haze in as the main camera approaches this point (furnace hall).</summary>
    public class HeatHazeZone : MonoBehaviour
    {
        public float fullRadius = 12f;
        public float fadeRadius = 30f;

        void Update()
        {
            var cam = Camera.main;
            if (!cam) return;
            float d = Vector3.Distance(cam.transform.position, transform.position);
            HeatHazeFeature.Intensity = 1f - Mathf.InverseLerp(fullRadius, fadeRadius, d);
        }

        void OnDisable() => HeatHazeFeature.Intensity = 0f;
    }
}
