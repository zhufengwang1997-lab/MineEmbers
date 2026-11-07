using UnityEngine;

namespace Zfw.PerfRunner
{
    /// <summary>
    /// Marks one benchmark zone. Child transforms are camera waypoints, visited in hierarchy order and
    /// interpolated over the zone's capture time. Zones run in ascending <see cref="order"/>.
    /// </summary>
    public class PerfZone : MonoBehaviour
    {
        public string zoneId = "entrance";
        public int order;

        public Transform[] Waypoints
        {
            get
            {
                var w = new Transform[transform.childCount];
                for (int i = 0; i < w.Length; i++) w[i] = transform.GetChild(i);
                return w;
            }
        }

        /// <summary>Camera pose at normalized time t (0..1) along the waypoints.</summary>
        public void Evaluate(float t, out Vector3 pos, out Quaternion rot)
        {
            var w = Waypoints;
            if (w.Length == 0) { pos = transform.position; rot = transform.rotation; return; }
            if (w.Length == 1) { pos = w[0].position; rot = w[0].rotation; return; }
            float f = Mathf.Clamp01(t) * (w.Length - 1);
            int i = Mathf.Min((int)f, w.Length - 2);
            float u = Mathf.SmoothStep(0, 1, f - i);
            pos = Vector3.Lerp(w[i].position, w[i + 1].position, u);
            rot = Quaternion.Slerp(w[i].rotation, w[i + 1].rotation, u);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var w = Waypoints;
            Gizmos.color = new Color(1f, 0.55f, 0.1f);
            for (int i = 0; i < w.Length; i++)
            {
                Gizmos.DrawWireSphere(w[i].position, 0.25f);
                Gizmos.DrawRay(w[i].position, w[i].forward);
                if (i > 0) Gizmos.DrawLine(w[i - 1].position, w[i].position);
            }
        }
#endif
    }
}
