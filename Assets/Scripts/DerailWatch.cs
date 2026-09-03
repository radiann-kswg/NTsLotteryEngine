using System.Collections.Generic;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 脱線監視（RSC SoakRecorder 式）。全球を毎 FixedUpdate 見て、異常速度・枠外・落下を球ごとに 1 回だけ警告する
    /// （位置・速度・その瞬間に触れているコライダ）。Recorder の録画と時刻で突き合わせて原因を潰す。
    /// </summary>
    public class DerailWatch : MonoBehaviour
    {
        public float maxSpeed = 8f;          // 球がこれ以上速いのは挟まれ・弾け
        public float minY = -0.5f;           // 床（y=0）より下 = 抜け
        public float maxR = 12f;             // 抽選機と塔（弧 6.2 + ボウル 1）の外
        public List<string> events = new List<string>();

        readonly HashSet<NumberBall> reported = new HashSet<NumberBall>();
        NumberBall[] balls; float nextScan;

        void FixedUpdate()
        {
            if (Time.time > nextScan) { balls = FindObjectsByType<NumberBall>(); nextScan = Time.time + 2f; }
            if (balls == null) return;
            foreach (var b in balls)
            {
                if (!b || reported.Contains(b)) continue;
                var rb = b.GetComponent<Rigidbody>(); if (!rb) continue;
                var p = rb.position; float v = rb.linearVelocity.magnitude, r = new Vector2(p.x, p.z).magnitude;
                string why = v > maxSpeed ? $"speed {v:F1}" : p.y < minY ? $"below floor y={p.y:F2}" : r > maxR ? $"out of area r={r:F1}" : null;
                if (why == null) continue;
                reported.Add(b);
                var near = Physics.OverlapSphere(p, 0.08f, ~0, QueryTriggerInteraction.Ignore);
                var names = new List<string>();
                foreach (var c in near) if (c.attachedRigidbody != rb) names.Add(c.transform.parent ? c.transform.parent.name + "/" + c.name : c.name);
                string msg = $"[Derail] t={Time.time:F1}s {b.name} ({b.transform.parent?.name}) {why} at {p} v={rb.linearVelocity} touching={string.Join(",", names)}";
                events.Add(msg); Debug.LogWarning(msg);
            }
        }
    }
}
