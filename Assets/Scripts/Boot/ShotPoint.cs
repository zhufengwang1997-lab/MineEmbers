using UnityEngine;

namespace MineEmbers.Boot
{
    /// <summary>Fixed camera spot for screenshot comparison (FR-PD-01). Name = file name.</summary>
    public class ShotPoint : MonoBehaviour
    {
        public string shotId = "s01";
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, 60f, 1.5f, 0.1f, 16f / 9f);
        }
#endif
    }
}
