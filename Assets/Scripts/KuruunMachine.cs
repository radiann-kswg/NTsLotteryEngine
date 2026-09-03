using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 別ボール用クルーン（RSC TowerD_Kuruun: 5 穴ボウル）。
    /// 起動時に穴を自動検出し、各穴に「不可視カバー（Blocker 層）」と「落下検知トリガー」を置く。
    /// 当たり／ハズレは LotoRules で確定済み → カバーで通れる穴を切り替えるだけ。
    /// 当たりならリフト経路でボウル上へ再投入、ハズレ or 上限到達で結果トレイへ搬送。
    /// </summary>
    public class KuruunMachine : MonoBehaviour
    {
        [Header("References (built by LotoSceneBuilder)")]
        public Transform bowl;             // FBX インスタンスの根
        public Transform dropPoint;        // 投入点（ボウル中央の上）
        public Transform[] liftPath;       // 穴の下 → 外 → 上 → dropPoint
        public Transform[] exitPath;       // 穴の下 → 結果トレイ
        public Transform camAnchor;

        [Header("Tuning")]
        public float probeStep = 0.02f;
        public float dropJitter = 0.03f;   // 対称性を崩す投入ジッタ（RSC 罠15/18）
        public float carrySpeed = 1.2f;
        public float pullAccel = 2f;       // timeout 超過後、目標穴へ引き寄せる加速度
        public float timeout = 25f;
        public float settleSeconds = 0.8f; // 落下検知後、搬送に移るまでの間

        public struct Hole { public Vector3 center; public float diameter; public float floorY; public GameObject cover; public BallTrigger trigger; }
        public readonly List<Hole> holes = new List<Hole>();
        public int winHole = 0;

        NumberBall lastFallen;
        int lastHole = -1;

        void Start() => ProbeHoles();

        // ---- 穴の自動検出: 真上から格子状にレイを落とし、ボウル面に当たらない格子を連結成分に束ねる ----
        public void ProbeHoles()
        {
            foreach (var h in holes) { if (h.cover) Destroy(h.cover); if (h.trigger) Destroy(h.trigger.gameObject); }
            holes.Clear();

            var rends = bowl.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Debug.LogError($"[{name}] bowl has no renderer"); return; }
            var bb = rends[0].bounds; foreach (var r in rends) bb.Encapsulate(r.bounds);
            float top = bb.max.y + 0.5f, bottom = bb.min.y - 0.005f, R = Mathf.Min(bb.extents.x, bb.extents.z) * 0.97f;
            var bowlCols = new HashSet<Collider>(bowl.GetComponentsInChildren<Collider>());

            int n = Mathf.CeilToInt(R * 2 / probeStep) + 1;
            var isHole = new bool[n, n]; var floorY = new float[n, n];
            for (int ix = 0; ix < n; ix++) for (int iz = 0; iz < n; iz++)
            {
                float x = bb.center.x - R + ix * probeStep, z = bb.center.z - R + iz * probeStep;
                if ((x - bb.center.x) * (x - bb.center.x) + (z - bb.center.z) * (z - bb.center.z) > R * R) { isHole[ix, iz] = false; continue; }
                var hits = Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, top - bottom + 0.01f, ~0, QueryTriggerInteraction.Ignore);
                float y = float.NegativeInfinity;
                foreach (var h in hits) if (bowlCols.Contains(h.collider) && h.point.y > y) y = h.point.y;
                isHole[ix, iz] = float.IsNegativeInfinity(y);
                floorY[ix, iz] = y;
            }

            // 連結成分（4 近傍）
            var seen = new bool[n, n];
            for (int ix = 0; ix < n; ix++) for (int iz = 0; iz < n; iz++)
            {
                if (!isHole[ix, iz] || seen[ix, iz]) continue;
                var stack = new Stack<(int, int)>(); stack.Push((ix, iz)); seen[ix, iz] = true;
                var cells = new List<(int, int)>(); float edgeY = 0; int edgeN = 0;
                while (stack.Count > 0)
                {
                    var (cx, cz) = stack.Pop(); cells.Add((cx, cz));
                    foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                        if (!isHole[nx, nz]) { if (!float.IsNegativeInfinity(floorY[nx, nz])) { edgeY += floorY[nx, nz]; edgeN++; } continue; }
                        if (!seen[nx, nz]) { seen[nx, nz] = true; stack.Push((nx, nz)); }
                    }
                }
                if (cells.Count < 4 || edgeN == 0) continue; // ノイズ（縁の外側など）
                var c = Vector3.zero; foreach (var (cx, cz) in cells) c += new Vector3(bb.center.x - R + cx * probeStep, 0, bb.center.z - R + cz * probeStep);
                c /= cells.Count;
                float d = 2f * Mathf.Sqrt(cells.Count * probeStep * probeStep / Mathf.PI);
                if (d < 0.08f) continue; // 球（径 0.1）が通れない隙間は穴ではない
                var hole = new Hole { center = c, diameter = d, floorY = edgeY / edgeN };
                MakeGates(ref hole);
                holes.Add(hole);
            }

            // 当たり穴 = 中心に最も近い穴（同距離なら角度順の先頭）
            var center = new Vector3(bb.center.x, 0, bb.center.z);
            holes.Sort((a, b) =>
            {
                int byR = (a.center - center).sqrMagnitude.CompareTo((b.center - center).sqrMagnitude);
                if (Mathf.Abs((a.center - center).magnitude - (b.center - center).magnitude) > 0.02f) return byR;
                return Mathf.Atan2(a.center.x - center.x, a.center.z - center.z).CompareTo(Mathf.Atan2(b.center.x - center.x, b.center.z - center.z));
            });
            winHole = 0;
            Debug.Log($"[{name}] holes={holes.Count} " + string.Join(", ", holes.ConvertAll(h => $"d={h.diameter:F2}@({h.center.x - center.x:F2},{h.center.z - center.z:F2}) y={h.floorY:F2}")));
            if (holes.Count < 2) Debug.LogError($"[{name}] クルーンの穴が {holes.Count} 個しか検出できない。probeStep / ボウル位置を確認");
        }

        void MakeGates(ref Hole h)
        {
            var cover = new GameObject("HoleCover") { layer = LotoLayers.Blocker };
            cover.transform.SetParent(transform, false);
            cover.transform.position = new Vector3(h.center.x, h.floorY + 0.005f, h.center.z);
            var bc = cover.AddComponent<BoxCollider>(); bc.size = new Vector3(h.diameter + 0.06f, 0.01f, h.diameter + 0.06f);
            cover.SetActive(false);
            h.cover = cover;

            var trig = new GameObject("HoleTrigger");
            trig.transform.SetParent(transform, false);
            trig.transform.position = new Vector3(h.center.x, h.floorY - 0.12f, h.center.z);
            var tb = trig.AddComponent<BoxCollider>(); tb.size = new Vector3(h.diameter, 0.1f, h.diameter); tb.isTrigger = true;
            var bt = trig.AddComponent<BallTrigger>();
            var self = this; int idx = holes.Count;
            bt.Entered += b => { self.lastFallen = b; self.lastHole = idx; };
            h.trigger = bt;
        }

        void SetOutcome(bool win)
        {
            for (int i = 0; i < holes.Count; i++) holes[i].cover.SetActive(win ? i != winHole : i == winHole);
        }

        /// <summary>ball を投入して wins 回当たり・その後落選（または max 到達）まで回す。onRound(round, win) を各回に通知。</summary>
        public IEnumerator Run(NumberBall ball, int wins, int max, Action<int, bool> onRound)
        {
            var rb = BallUtil.Prepare(ball);
            for (int round = 0; ; round++)
            {
                bool win = round < wins;
                SetOutcome(win);

                // 投入
                rb.isKinematic = true;
                rb.position = dropPoint.position + (Vector3)(UnityEngine.Random.insideUnitCircle * dropJitter);
                yield return new WaitForFixedUpdate();
                rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;

                // 落下待ち
                lastFallen = null; lastHole = -1;
                float t = 0f;
                while (lastFallen != ball)
                {
                    t += Time.fixedDeltaTime;
                    if (t > timeout)
                    {
                        int target = win ? winHole : (winHole + 1) % holes.Count;
                        var to = holes[target].center - rb.position; to.y = 0f;
                        rb.AddForce(to.normalized * pullAccel, ForceMode.Acceleration);
                        if (t > timeout * 2f)
                        {
                            // ponytail: 最終手段。穴の真上へ置く（ログに残す）
                            Debug.LogWarning($"[{name}] ball {ball.number} forced into hole {target} after {t:F0}s");
                            rb.position = new Vector3(holes[target].center.x, holes[target].floorY + 0.06f, holes[target].center.z);
                            rb.linearVelocity = Vector3.zero;
                        }
                    }
                    yield return new WaitForFixedUpdate();
                }
                if ((lastHole == winHole) != win) Debug.LogWarning($"[{name}] outcome mismatch: expected win={win}, fell into hole {lastHole}");
                onRound?.Invoke(round, win);
                yield return new WaitForSeconds(settleSeconds);

                bool last = !(win && round + 1 < max);
                var path = new List<Vector3>();
                foreach (var p in last ? exitPath : liftPath) path.Add(p.position);
                yield return BallUtil.Carry(rb, path, carrySpeed);
                if (last) break;
            }
        }
    }
}
