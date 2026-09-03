using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>ProcMesh.Disc をコンポーネント化（回転床は convex にして kinematic Rigidbody に載せる）。</summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class DiscPlate : MonoBehaviour
    {
        public float radius = 0.5f, thickness = 0.04f, centerRise = 0.04f;
        public int segments = 64;
        public bool convex = true;

        void OnEnable() => Rebuild();
        void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

        public void Rebuild()
        {
            var m = ProcMesh.Disc(radius, thickness, centerRise, segments);
            GetComponent<MeshFilter>().sharedMesh = m;
            var mc = GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.convex = convex; mc.sharedMesh = m;
        }
    }
}
