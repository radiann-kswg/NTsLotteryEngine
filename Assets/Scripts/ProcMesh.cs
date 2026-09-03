using System.Collections.Generic;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 抽選機の筒・回転床を生成する。面ごとに頂点を持つ（フラットシェーディング・Cull Off 前提）。
    /// </summary>
    public static class ProcMesh
    {
        class B
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c);
                t.AddRange(new[] { i, i + 1, i + 2 });
            }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(v); m.SetTriangles(t, 0);
                m.RecalculateNormals(); m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>
        /// 厚みのある筒（Y 軸・底が y=0）。内径は底→天で線形補間（漏斗にも使える）。
        /// gapAngleDeg &gt; 0 なら gapCenterDeg を中心に、底から gapHeight までの壁を切り欠く（排出口）。
        /// 角度は +Z を 0°、Y 軸まわり右ねじ。
        /// </summary>
        public static Mesh Tube(float rInBottom, float rInTop, float thickness, float height, int segments,
                                float gapCenterDeg = 0, float gapAngleDeg = 0, float gapHeight = 0)
        {
            var b = new B();
            float RIn(float y) => Mathf.Lerp(rInBottom, rInTop, height <= 0 ? 0 : y / height);
            Vector3 P(float deg, float r, float y) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r); }

            void Band(float y0, float y1, float a0, float a1, bool open)
            {
                int n = Mathf.Max(3, Mathf.RoundToInt(segments * (a1 - a0) / 360f));
                for (int i = 0; i < n; i++)
                {
                    float d0 = Mathf.Lerp(a0, a1, (float)i / n), d1 = Mathf.Lerp(a0, a1, (float)(i + 1) / n);
                    float ri0 = RIn(y0), ri1 = RIn(y1);
                    var ob0 = P(d0, ri0 + thickness, y0); var ob1 = P(d1, ri0 + thickness, y0);
                    var ot0 = P(d0, ri1 + thickness, y1); var ot1 = P(d1, ri1 + thickness, y1);
                    var ib0 = P(d0, ri0, y0); var ib1 = P(d1, ri0, y0);
                    var it0 = P(d0, ri1, y1); var it1 = P(d1, ri1, y1);
                    b.Quad(ob0, ot0, ot1, ob1);   // 外面
                    b.Quad(ib1, it1, it0, ib0);   // 内面
                    b.Quad(ot0, it0, it1, ot1);   // 上縁
                    b.Quad(ob1, ib1, ib0, ob0);   // 下縁
                    if (open && i == 0) b.Quad(ob0, ib0, it0, ot0);          // 切り欠きの側面
                    if (open && i == n - 1) b.Quad(ot1, it1, ib1, ob1);
                }
            }

            bool hasGap = gapAngleDeg > 0 && gapHeight > 0;
            if (!hasGap)
            {
                Band(0, height, 0, 360, false);
            }
            else
            {
                float g0 = gapCenterDeg + gapAngleDeg * 0.5f, g1 = gapCenterDeg - gapAngleDeg * 0.5f + 360f;
                float gh = Mathf.Min(gapHeight, height);
                Band(0, gh, g0, g1, true);
                if (gh < height) Band(gh, height, 0, 360, false);
            }
            return b.Build("Tube");
        }

        /// <summary>中央が centerRise だけ盛り上がった円盤（回転床・仕切り板）。上面の高さ y=0、厚みは下方向。</summary>
        public static Mesh Disc(float radius, float thickness, float centerRise, int segments)
        {
            var b = new B();
            Vector3 P(int i, float r, float y) { float a = i * Mathf.PI * 2 / segments; return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r); }
            var top = new Vector3(0, centerRise, 0); var bot = new Vector3(0, -thickness, 0);
            for (int i = 0; i < segments; i++)
            {
                b.Tri(top, P(i + 1, radius, 0), P(i, radius, 0));                                 // 上面（円錐）
                b.Tri(bot, P(i, radius, -thickness), P(i + 1, radius, -thickness));               // 下面
                b.Quad(P(i, radius, 0), P(i + 1, radius, 0), P(i + 1, radius, -thickness), P(i, radius, -thickness)); // 側面
            }
            return b.Build("Disc");
        }
    }

    /// <summary>ProcMesh.Tube をコンポーネント化。値を変えるとエディタ上でも即再生成。</summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class TubeWall : MonoBehaviour
    {
        public float innerRadiusBottom = 0.5f, innerRadiusTop = 0.5f, thickness = 0.02f, height = 0.5f;
        public int segments = 64;
        public float gapCenterDeg = 0, gapAngleDeg = 0, gapHeight = 0;

        void OnEnable() => Rebuild();
        void OnValidate() { if (isActiveAndEnabled) Rebuild(); }

        public void Rebuild()
        {
            var m = ProcMesh.Tube(innerRadiusBottom, innerRadiusTop, thickness, height, segments, gapCenterDeg, gapAngleDeg, gapHeight);
            GetComponent<MeshFilter>().sharedMesh = m;
            var mc = GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = m;
        }
    }

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
