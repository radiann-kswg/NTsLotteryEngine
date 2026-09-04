using System;
using System.Collections;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 別ボール用の縦連クルーン塔。各段 = 回転ボウル（穴は一様）＋静止コレクタ（当たり扇形は軸へ／ハズレ扇形は外周の樋へ）。
    /// **当たり外れは物理が決める**（コレクタの当たり扇形の角度比 ≈ p。Tools > NTsLoto > Monte Carlo で実測して保証）。
    /// 当たりは漏斗の喉から次段のコーン面へ、ハズレは正面のシュート → 落下チャンネル → ハズレトレイへ。最下段を当たりで抜けた球は完走トレイへ。
    /// </summary>
    public class KuruunTower : MonoBehaviour
    {
        [Serializable]
        public class Level
        {
            public Transform bowl;
            public Transform throat;         // 漏斗の喉（timeout 時の強制移動先）
            public BallTrigger passTrigger;  // コレクタ中央の下（当たり）
            public BallTrigger exitTrigger;  // 排出シュート上（ハズレ）
        }

        [Header("References (built by LotoSceneBuilder)")]
        public Level[] levels;              // 0 = 最上段
        public Transform dropPoint;
        public Transform camAnchor;         // x,z を使う。y は球に追従
        public Transform loseTray, winTray; // 球が最後に居てよい場所（DerailWatch: 完走トレイ・排出側以外で地面に落ちていたら脱線）

        [Header("Tuning")]
        public float dropJitter = 0.03f;    // 投入ジッタ（RSC 罠15/18）
        public float bowlRpm = 10f;         // ボウルの回転数（LotoMonteCarlo.bowlRpm と同じ値で校正する）
        // 最上段の投入速度（塔のローカル座標）。下段と同じ条件（コーン面 r=0.85 の 0.59 上から静止落下）で校正するので既定はゼロ。
        // 2 扇形のコレクタ（p50/p53）は落差 0.39→0.59 で p が −0.01 動いた（2026-09-03 MC）ので、投入条件は MC の entryHeight/entryTangential と必ず揃える
        public Vector3 dropVelocity = Vector3.zero;
        public float angularDamping = 0.05f, linearDamping = 0f;   // 校正（LotoMonteCarlo.Launch）と同じ素の物理。変えるなら MC も変えて測り直す
        public float funnelDamping = 2.0f;  // コレクタより下（漏斗）だけ強くして喉で周回し続けない（罠5。当たり外れは確定済みなので確率に影響しない）
        public float timeout = 25f;         // 落ちないときは喉へ強制移動（警告ログ）
        public float settleSeconds = 2.5f;  // 最終落下を見せる時間
        public float camAhead = 0.35f;      // カメラ高さ = 球 + camAhead
        public float camFov = 40f;          // 追従中の画角（60 だと球が点。LotoDirector.Look が 60 に戻す）

        /// <summary>球を投入し、段ごとに当たり（次段へ）／ハズレ（排出）を物理で見届ける。onRound(段, 当たり)。</summary>
        public IEnumerator Run(NumberBall ball, int max, Camera cam, Action<int, bool> onRound)
        {
            var rb = BallUtil.Prepare(ball);
            rb.angularDamping = angularDamping; rb.linearDamping = linearDamping;
            var j = UnityEngine.Random.insideUnitCircle * dropJitter;
            // isKinematic を切り替えない（切り替えると CCD が Discrete に落ち、高速落下でボウルを突き抜ける）
            rb.position = dropPoint.position + new Vector3(j.x, 0, j.y);
            // dropVelocity は塔のローカル座標（弧の端の塔はワールドで 90° 回っている。ワールドのまま与えると接線のつもりが半径方向になり、
            // 右端の塔（ball 10）で球が縁を越えて地面に落ちた 2026-09-03 録画）
            rb.linearVelocity = transform.rotation * dropVelocity; rb.angularVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();

            int n = Mathf.Min(max, levels.Length);
            for (int i = 0; i < n; i++)
            {
                var L = levels[i];
                bool passed = false, exited = false;
                float dropAz = float.NaN;   // コレクタ天端を割った瞬間の方位（塔ローカル・Unity 角）= 当落を決める方位
                Action<NumberBall> onPass = b => { if (b == ball) passed = true; };
                Action<NumberBall> onExit = b => { if (b == ball) exited = true; };
                L.passTrigger.Entered += onPass; L.exitTrigger.Entered += onExit;
                float t = 0f, nextLog = 5f;
                while (!passed && !exited)
                {
                    t += Time.fixedDeltaTime;
                    rb.angularDamping = rb.position.y < L.bowl.position.y - 0.40f ? funnelDamping : angularDamping;   // コレクタ内縁（−0.36）より下 = 漏斗
                    if (float.IsNaN(dropAz) && rb.position.y < L.bowl.position.y - 0.20f)
                    {   // コレクタ天端（−0.20）を割った ＝ ボウルの穴を抜けた。この方位が当たり扇形に入っているかで当落が決まる
                        var d = transform.InverseTransformPoint(rb.position) - transform.InverseTransformPoint(L.bowl.position);
                        dropAz = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
                    }
                    if (t > nextLog)
                    {
                        var lp = rb.position - L.bowl.position;
                        var touching = Physics.OverlapSphere(rb.position, 0.06f, ~0, QueryTriggerInteraction.Ignore);
                        Debug.Log($"[{name}] level {i} t={t:F0}s r={new Vector2(lp.x, lp.z).magnitude:F2} y={rb.position.y:F2} (bowl@{L.bowl.position.y:F2}) v={rb.linearVelocity.magnitude:F2} touching={string.Join("/", System.Array.ConvertAll(touching, c => c.name))}");
                        nextLog += 5f;
                    }
                    if (t > timeout)
                    {
                        // 詰まり救済: この段の投入点（コーン面 r=0.85・ランダム角）へ戻してやり直す。喉へ置くと当たりを作ってしまうので絶対にしない
                        Debug.LogWarning($"[{name}] ball {ball.number} stuck at level {i} (y={rb.position.y:F2}), re-dropped on the cone");
                        float ra = UnityEngine.Random.value * Mathf.PI * 2f;
                        rb.position = L.bowl.position + new Vector3(Mathf.Sin(ra) * 0.85f, 0.16f + 0.35f, Mathf.Cos(ra) * 0.85f);
                        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; t = 0f;
                    }
                    Follow(cam, rb.position);
                    yield return new WaitForFixedUpdate();
                }
                L.passTrigger.Entered -= onPass; L.exitTrigger.Entered -= onExit;
                bool win = passed;   // 物理の結果
                // 実測ログ（エディタのみ・Output/ は git 管轄外）: 塔,段,落下方位,当落。MC（LotoMonteCarlo）の方位別 p と突き合わせる
                if (Application.isEditor)
                    try { System.IO.Directory.CreateDirectory("Output"); System.IO.File.AppendAllText("Output/drop_azimuth.csv", $"{name},{i},{dropAz:F1},{(win ? 1 : 0)}\n"); }
                    catch (Exception e) { Debug.LogWarning($"[{name}] azimuth log failed: {e.Message}"); }
                onRound?.Invoke(i, win);
                if (!win) break;
            }

            for (float s = 0; s < settleSeconds; s += Time.fixedDeltaTime)
            {
                Follow(cam, rb.position);
                yield return new WaitForFixedUpdate();
            }
        }

        void Follow(Camera cam, Vector3 p)
        {
            if (!cam || !camAnchor) return;
            cam.fieldOfView = camFov;
            var a = camAnchor.position;
            cam.transform.position = new Vector3(a.x, p.y + camAhead, a.z);
            cam.transform.LookAt(new Vector3(transform.position.x, p.y, transform.position.z));
        }
    }
}
