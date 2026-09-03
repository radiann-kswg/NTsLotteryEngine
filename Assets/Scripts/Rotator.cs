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
        public Vector3 axis = Vector3.up;

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
            if (rpm != 0f) rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(rpm * 6f * Time.fixedDeltaTime, axis));
        }
    }

    /// <summary>球が入ったら通知するトリガー。</summary>
    [RequireComponent(typeof(Collider))]
    public class BallTrigger : MonoBehaviour
    {
        public event System.Action<NumberBall> Entered;
        void Awake() => GetComponent<Collider>().isTrigger = true;
        void OnTriggerEnter(Collider c)
        {
            var b = c.GetComponentInParent<NumberBall>();
            if (b) Entered?.Invoke(b);
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
        public static Rigidbody Prepare(NumberBall ball)
        {
            var rb = ball.GetComponent<Rigidbody>();
            rb.sleepThreshold = 0f;                       // 渋滞スリープで永久停止しない（RSC 罠2）
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
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
