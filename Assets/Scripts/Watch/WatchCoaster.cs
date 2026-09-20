using System.Collections;
using UnityEngine;

namespace NTsLotteryEngine.Watch
{
    /// <summary>
    /// コースター（docs/WATCH.md M2(b)）: 螺旋樋（Assets/Models/Coaster_Helix.fbx・BlenderSources/gen_coaster.py）を転がり落ちた球を
    /// トレイで受け、リフト（BallUtil.Carry・中央の柱に沿って）で入口へ戻す。寸法は gen_coaster.py と一致させる。
    /// </summary>
    public class WatchCoaster : MonoBehaviour
    {
        // gen_coaster.py と同じ値
        public const float R = 0.55f, ZTop = 1.50f, Turns = 4f, Pitch = 0.36f, FlatDeg = 30f;
        public const float EndDeg = 360f * Turns + FlatDeg;
        /// <summary>Blender の P(θ, r, z) を Unity 座標へ（FBX 規約: Unity 角 = Blender θ + 180°）。</summary>
        public static Vector3 Pt(float deg, float r, float y) { float a = (deg + 180f) * Mathf.Deg2Rad; return new Vector3(r * Mathf.Sin(a), y, r * Mathf.Cos(a)); }
        public static Vector3 Tangent(float deg) { float a = (deg + 180f) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)); }   // θ 増加方向
        public static readonly Vector3 TrayLocal = Pt(EndDeg, R, 0f) + Tangent(EndDeg) * 0.45f;   // 出口の接線上。床の天端 y=0.01
        public static readonly Vector3 ColumnLocal = TrayLocal + new Vector3(TrayLocal.x, 0, TrayLocal.z).normalized * 0.42f;   // リフトの柱（トレイの外側）

        [Tooltip("樋を転がる間の線形減衰（空気抵抗の代わり。終端速度 ≈ g·sinθ·5/7 ÷ この値。0.5 で ≈1.5 m/s）")] public float damping = 0.5f;

        [Tooltip("リフトの搬送速度 [m/s]")] public float liftSpeed = 0.5f;
        [Tooltip("入口で樋に沿って与える初速 [m/s]")] public float entrySpeed = 0.4f;
        [Tooltip("トレイに居続けたら回収するまでの秒数")] public float settle = 3f;
        [Tooltip("これ以上経っても着かなければ詰まりとみなして回収")] public float timeout = 90f;

        /// <summary>球をリフトで入口へ運び、樋を落ちきってトレイに落ち着くまで見届ける（1 周）。</summary>
        public IEnumerator Run(NumberBall ball)
        {
            var rb = ball.GetComponent<Rigidbody>();
            var lift = TrayLocal + Vector3.up * 0.12f;   // トレイの中央から柱に沿って真上へ
            var top = lift; top.y = ZTop + 0.35f;
            var entry = Pt(8f, R, ZTop + 0.25f);
            yield return BallUtil.Carry(rb, new[] { W(lift), W(top), W(entry) }, liftSpeed);
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // isKinematic の往復で CCD が落ちる（AGENTS 罠 3）
            rb.linearDamping = damping;   // ponytail: 減衰で速度を抑える（実物は鈴やバンプで減速する）。KuruunTower.Run が自分の値に戻す
            rb.linearVelocity = transform.rotation * Tangent(8f) * entrySpeed;

            float t = 0f, inTray = 0f;
            var tray = W(TrayLocal);
            while (t < timeout)
            {
                t += Time.fixedDeltaTime;
                var d = rb.position - tray; d.y = 0;
                inTray = d.magnitude < 0.3f && rb.position.y < tray.y + 0.2f ? inTray + Time.fixedDeltaTime : 0f;
                if (inTray >= settle) yield break;
                yield return new WaitForFixedUpdate();
            }
            Debug.LogWarning($"[WatchCoaster] ball {ball.number} did not reach the tray in {timeout}s (y={rb.position.y:F2}); lifting anyway");
        }

        Vector3 W(Vector3 local) => transform.TransformPoint(local);
    }
}
