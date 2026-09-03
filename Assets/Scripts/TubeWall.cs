using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>ProcMesh.Tube をコンポーネント化。値を変えるとエディタ上でも即再生成。</summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class TubeWall : MonoBehaviour
    {
        public float innerRadiusBottom = 0.5f, innerRadiusTop = 0.5f, thickness = 0.02f, height = 0.5f;
        public int segments = 64;
        public float gapCenterDeg = 0, gapAngleDeg = 0, gapHeight = 0;
        public bool gapAtTop = false;

        void OnEnable() => Rebuild();
        void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

        public void Rebuild()
        {
            var m = ProcMesh.Tube(innerRadiusBottom, innerRadiusTop, thickness, height, segments, gapCenterDeg, gapAngleDeg, gapHeight, gapAtTop);
            GetComponent<MeshFilter>().sharedMesh = m;
            var mc = GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = m;
        }
    }
}
