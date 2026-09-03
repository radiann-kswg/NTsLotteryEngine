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

        /// <summary>
        /// クルーンのボウル（RSC TowerD_Kuruun の形を回転体で再現）。外周壁 → コーン → 穴リング（扇形の穴 holes 個）→ 中央ドーム。
        /// 片面（内側向き）。球は上からしか来ないので厚みは持たせない。底面 y=0。
        /// </summary>
        public static Mesh Bowl(float rimRadius, float rimHeight, float coneTopHeight, float ringOuterRadius, float ringHeight,
                                float ringInnerRadius, float domeHeight, int holes, float holeAngleDeg, int segments)
        {
            var b = new B();
            // 断面（外→内）。ring=true の区間は穴の扇形を抜く
            var prof = new[] {
                (r0: rimRadius, h0: rimHeight, r1: rimRadius, h1: coneTopHeight, ring: false),         // 外周壁
                (r0: rimRadius, h0: coneTopHeight, r1: ringOuterRadius, h1: ringHeight, ring: false),  // コーン
                (r0: ringOuterRadius, h0: ringHeight, r1: ringInnerRadius, h1: ringHeight, ring: true),// 穴リング
                (r0: ringInnerRadius, h0: ringHeight, r1: 0.0001f, h1: domeHeight, ring: false),      // 中央ドーム
            };
            Vector3 P(float deg, float r, float h) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * r, h, Mathf.Cos(a) * r); }
            float holePitch = 360f / Mathf.Max(1, holes);
            for (int i = 0; i < segments; i++)
            {
                float d0 = 360f * i / segments, d1 = 360f * (i + 1) / segments, dm = (d0 + d1) * 0.5f;
                foreach (var q in prof)
                {
                    if (q.ring && holes > 0)
                    {
                        float off = Mathf.Repeat(dm, holePitch);                       // 穴は各ピッチの先頭に置く
                        if (off < holeAngleDeg) continue;
                    }
                    var a0 = P(d0, q.r0, q.h0); var a1 = P(d1, q.r0, q.h0); var c1 = P(d1, q.r1, q.h1); var c0 = P(d0, q.r1, q.h1);
                    // 表面が内側（上＋軸向き）を向くように巻き方向をそろえる
                    var n = Vector3.Cross(a1 - a0, c1 - a0);
                    var center = (a0 + a1 + c1 + c0) * 0.25f; var radial = new Vector3(center.x, 0, center.z).normalized;
                    var want = Vector3.up - radial * 0.7f;
                    if (Vector3.Dot(n, want) < 0) b.Quad(a0, c0, c1, a1); else b.Quad(a0, a1, c1, c0);   // Cross(b-a,c-a) が表面の法線（レイキャストで確認済み）
                }
            }
            return b.Build("Bowl");
        }
    }
}
