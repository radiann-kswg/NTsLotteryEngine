using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 二層式ロトマシーンの 1 段（電動攪拌式遠心力型）。
    /// 回転床で攪拌 → 減速 → 「選ばれた球だけ通れる排出口」から 1 球だけ排出する。
    /// 結果は LotoRules で確定済み。ここは誘導するだけ。
    /// </summary>
    public class LotoDrumTier : MonoBehaviour
    {
        [Header("References (built by LotoSceneBuilder)")]
        public Rotator floor;
        public BallTrigger port;          // 排出口の外にある通過検知
        public Transform spawnCenter;     // 球の投入位置（床中央の少し上）
        public float spawnRadius = 0.3f;

        [Header("Tuning")]
        public float stirRpm = 40f;       // 攪拌回転数（実機 夢ロトくんは 55rpm）
        public float drawRpm = 5f;        // 取り出し時の低速回転
        public float stirSeconds = 6f;
        public float pullAccel = 6f;      // 当選球への引き寄せ加速度 [m/s^2]。壁は摩擦ゼロ（Slick）でないと壁に張り付く
        public float timeout = 30f;       // これを超えたら引き寄せを 3 倍、2 倍超で強制排出

        public readonly List<NumberBall> balls = new List<NumberBall>();

        public NumberBall Spawn(NumberBall prefab, int number)
        {
            var offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
            var pos = spawnCenter.position + new Vector3(offset.x, UnityEngine.Random.value * 0.3f, offset.y);
            var b = Instantiate(prefab, pos, UnityEngine.Random.rotation, transform);
            b.name = $"Ball{number:00}";
            b.number = number;
            b.Apply();
            BallUtil.Prepare(b);
            balls.Add(b);
            return b;
        }

        public void Stir() => floor.targetRpm = stirRpm;
        public void Stop() => floor.targetRpm = 0f;

        /// <summary>number の球を排出する。排出後 onExit(ball)。</summary>
        public IEnumerator Draw(int number, Action<NumberBall> onExit)
        {
            var ball = balls.Find(b => b.number == number);
            if (!ball) { Debug.LogError($"[{name}] ball {number} not found"); yield break; }
            var rb = ball.GetComponent<Rigidbody>();

            floor.targetRpm = stirRpm;
            yield return new WaitForSeconds(stirSeconds);
            floor.targetRpm = drawRpm;

            bool exited = false;
            Action<NumberBall> handler = b => { if (b == ball) exited = true; };
            port.Entered += handler;
            ball.gameObject.layer = LotoLayers.ChosenBall;

            float t = 0f, nextLog = 5f;
            bool forced = false;
            while (!exited)
            {
                t += Time.fixedDeltaTime;
                var to = port.transform.position - rb.position; to.y = 0f;   // 排出口の外の通過検知へ引く（出た後も下り方向）
                float k = t > timeout ? 3f : 1f;
                rb.AddForce(to.normalized * pullAccel * k, ForceMode.Acceleration);
                if (t > nextLog)
                {
                    var lp = transform.InverseTransformPoint(rb.position);
                    Debug.Log($"[{name}] ball {number} t={t:F0}s r={new Vector2(lp.x, lp.z).magnitude:F2} deg={Mathf.Atan2(lp.x, lp.z) * Mathf.Rad2Deg:F0} y={lp.y:F2} v={rb.linearVelocity.magnitude:F2}");
                    nextLog += 5f;
                }
                if (t > timeout * 2f && !forced)
                {
                    // ponytail: 最終手段。物理で出せなければ通過検知の位置へ置く（ログに残す）
                    Debug.LogWarning($"[{name}] ball {number} forced out after {t:F0}s");
                    rb.position = port.transform.position; rb.linearVelocity = Vector3.zero; forced = true;
                }
                yield return new WaitForFixedUpdate();
            }

            port.Entered -= handler;
            ball.gameObject.layer = LotoLayers.Ball;
            balls.Remove(ball);
            onExit?.Invoke(ball);
        }
    }
}
