using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>ProcMesh.Bowl をコンポーネント化（クルーンのボウル。RSC TowerD_Kuruun の寸法が既定）。</summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class KuruunBowl : MonoBehaviour
    {
        public float rimRadius = 0.86f, rimHeight = 0.56f, coneTopHeight = 0.46f;
        public float ringOuterRadius = 0.35f, ringHeight = 0.12f, ringInnerRadius = 0.13f, domeHeight = 0.25f;
        public int holes = 5, segments = 80;
        public float holeAngleDeg = 40f;   // r=0.24 で弦 ≈ 0.17 = 1.7d

        void OnEnable() => Rebuild();
        void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

        public void Rebuild()
        {
            var m = ProcMesh.Bowl(rimRadius, rimHeight, coneTopHeight, ringOuterRadius, ringHeight, ringInnerRadius, domeHeight, holes, holeAngleDeg, segments);
            GetComponent<MeshFilter>().sharedMesh = m;
            var mc = GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = m;
        }
    }
}
