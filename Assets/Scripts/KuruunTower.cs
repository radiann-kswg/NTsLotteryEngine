using System;
using System.Collections;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 別ボール用の縦連クルーン塔。RSC TowerD_Kuruun（5 穴ボウル）を N 段重ね、
    /// 各段の下に「漏斗 → 振り分けフラップ」を置く。フラップは当たりなら退避（球は真下の次段へ落ちる）、
    /// ハズレなら喉の下へ傾いて球を正面の排出シュート → 縦の落下チャンネル → ハズレトレイへ流す。
    /// 最下段を当たりで抜けた球は塔の真下の完走トレイへ。結果は LotoRules で確定済み（フラップはそれを見せるだけ）。
    /// </summary>
    public class KuruunTower : MonoBehaviour
    {
        [Serializable]
        public class Level
        {
            public Transform bowl;
            public Transform flap;           // 振り分けフラップ（回転で切替）
            public Transform throat;         // 漏斗の喉（timeout 時の強制移動先）
            public BallTrigger passTrigger;  // フラップ下（次段へ）
            public BallTrigger exitTrigger;  // 排出シュート上
        }

        [Header("References (built by LotoSceneBuilder)")]
        public Level[] levels;              // 0 = 最上段
        public Transform dropPoint;
        public Transform camAnchor;         // x,z を使う。y は球に追従

        [Header("Tuning")]
        public float dropJitter = 0.03f;    // 投入ジッタ（RSC 罠15/18）
        public Vector3 dropVelocity = new Vector3(-0.57f, 0, -0.57f);   // 投入点（ボウル軸から角度 135°）での接線速度 0.8（コーン面を螺旋で下る）
        public float angularDamping = 1.0f, linearDamping = 0.3f;    // 転がり抵抗の代用（漏斗の喉で周回し続けない）
        public float timeout = 25f;         // 落ちないときは喉へ強制移動（警告ログ）
        public float settleSeconds = 2.5f;  // 最終落下を見せる時間
        public float camAhead = 0.35f;      // カメラ高さ = 球 + camAhead

        public static readonly Quaternion FlapPass = Quaternion.Euler(0, 180, 0);     // 退避（+Z 側へ水平）
        public static readonly Quaternion FlapDivert = Quaternion.Euler(-35, 0, 0);  // 喉の下で -Z へ下り傾斜

        public IEnumerator Run(NumberBall ball, int wins, int max, Camera cam, Action<int, bool> onRound)
        {
            var rb = BallUtil.Prepare(ball);
            rb.angularDamping = angularDamping; rb.linearDamping = linearDamping;
            var j = UnityEngine.Random.insideUnitCircle * dropJitter;
            // isKinematic を切り替えない（切り替えると CCD が Discrete に落ち、高速落下でボウルを突き抜ける）
            rb.position = dropPoint.position + new Vector3(j.x, 0, j.y);
            rb.linearVelocity = dropVelocity; rb.angularVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate();

            int n = Mathf.Min(max, levels.Length);
            for (int i = 0; i < n; i++)
            {
                var L = levels[i];
                bool win = i < wins;
                L.flap.localRotation = win ? FlapPass : FlapDivert;

                bool passed = false, exited = false;
                Action<NumberBall> onPass = b => { if (b == ball) passed = true; };
                Action<NumberBall> onExit = b => { if (b == ball) exited = true; };
                L.passTrigger.Entered += onPass; L.exitTrigger.Entered += onExit;
                float t = 0f, nextLog = 5f;
                while (!passed && !exited)
                {
                    t += Time.fixedDeltaTime;
                    if (t > nextLog)
                    {
                        var lp = rb.position - L.bowl.position;
                        var touching = Physics.OverlapSphere(rb.position, 0.06f, ~0, QueryTriggerInteraction.Ignore);
                        Debug.Log($"[{name}] level {i} t={t:F0}s r={new Vector2(lp.x, lp.z).magnitude:F2} y={rb.position.y:F2} (bowl@{L.bowl.position.y:F2}) v={rb.linearVelocity.magnitude:F2} touching={string.Join("/", System.Array.ConvertAll(touching, c => c.name))}");
                        nextLog += 5f;
                    }
                    if (t > timeout)
                    {
                        // ponytail: 最終手段。喉へ置けばフラップが振り分ける（ログに残す）
                        Debug.LogWarning($"[{name}] ball {ball.number} stuck at level {i}, moved to throat");
                        rb.position = L.throat.position; rb.linearVelocity = Vector3.zero; t = 0f;
                    }
                    Follow(cam, rb.position);
                    yield return new WaitForFixedUpdate();
                }
                L.passTrigger.Entered -= onPass; L.exitTrigger.Entered -= onExit;
                if (passed != win) Debug.LogWarning($"[{name}] level {i}: expected win={win} but passed={passed} exited={exited}");
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
            var a = camAnchor.position;
            cam.transform.position = new Vector3(a.x, p.y + camAhead, a.z);
            cam.transform.LookAt(new Vector3(transform.position.x, p.y, transform.position.z));
        }
    }
}
