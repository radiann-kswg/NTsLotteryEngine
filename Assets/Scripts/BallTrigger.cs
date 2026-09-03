using UnityEngine;

namespace NTsLotoEngine
{
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
}
