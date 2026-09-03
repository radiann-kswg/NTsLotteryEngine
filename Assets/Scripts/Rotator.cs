using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>kinematic Rigidbody を定速回転させる（回転床）。targetRpm へ ramp で追従するので攪拌→減速が滑らか。</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Rotator : MonoBehaviour
    {
        public float rpm = 0f;
        public float targetRpm = 0f;
        public float rampRpmPerSecond = 20f;
        public Vector3 axis = Vector3.up;   // ワールド軸

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 薄い球のトンネリング対策
        }

        void FixedUpdate()
        {
            rpm = Mathf.MoveTowards(rpm, targetRpm, rampRpmPerSecond * Time.fixedDeltaTime);
            // ワールド軸で回す（AngleAxis を右から掛けるとローカル軸になり、FBX の根（X −90°）を持つボウル・皿が横倒しの車輪のように回った 2026-09-03）
            if (rpm != 0f) rb.MoveRotation(Quaternion.AngleAxis(rpm * 6f * Time.fixedDeltaTime, axis) * rb.rotation);
        }
    }

    public static class LotoLayers
    {
        public const int Ball = 8;        // 通常の球
        public const int ChosenBall = 9;  // 当選球（Blocker と衝突しない）
        public const int Blocker = 10;    // 不可視ブロッカー／穴カバー
    }

    public static class BallUtil
    {
        /// <summary>球の物理マテリアル。プレハブ（UPM・読み取り専用）には無いのでここで持つ。反発が無いと球が「死んで」見える（2026-09-03 録画で確認）。</summary>
        public static readonly PhysicsMaterial Bouncy = new PhysicsMaterial("Ball")
        {
            bounciness = 0.6f, dynamicFriction = 0.4f, staticFriction = 0.4f,
            // Average: 球同士 0.6 で賑やか、床・レール（0）とは 0.3 で落ち着く。Maximum にすると排出された球がレールを跳び越えて床に散らばる（2026-09-03 録画）
            bounceCombine = PhysicsMaterialCombine.Average,
        };

        /// <summary>クルーンのボウル・コレクタ・篩の皿（FBX）の物理マテリアル。摩擦 0.6 だと樋で外壁に擦れて止まる。MC 治具と実機で同じものを使う。</summary>
        public static readonly PhysicsMaterial Machine = new PhysicsMaterial("Machine") { dynamicFriction = 0.3f, staticFriction = 0.3f, bounciness = 0.1f };

        public static Rigidbody Prepare(NumberBall ball)
        {
            var rb = ball.GetComponent<Rigidbody>();
            rb.sleepThreshold = 0f;                       // 渋滞スリープで永久停止しない（RSC 罠2）
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.maxDepenetrationVelocity = 1f;             // 既定 10: 球の山の食い込み解消で外側の球が壁を突き抜けて射出される
            rb.solverIterations = 12;                     // 88 球の山（既定 6）
            ball.GetComponentInChildren<Collider>().sharedMaterial = Bouncy;
            ball.gameObject.layer = LotoLayers.Ball;
            return rb;
        }

        /// <summary>球を kinematic にして経路に沿って運ぶ（リフト／結果トレイへの搬送）。終点で物理へ戻す。</summary>
        public static System.Collections.IEnumerator Carry(Rigidbody rb, System.Collections.Generic.IList<Vector3> path, float speed)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
            foreach (var p in path)
            {
                while ((rb.position - p).sqrMagnitude > 1e-6f)
                {
                    rb.MovePosition(Vector3.MoveTowards(rb.position, p, speed * Time.fixedDeltaTime));
                    yield return new WaitForFixedUpdate();
                }
            }
            rb.detectCollisions = true;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
