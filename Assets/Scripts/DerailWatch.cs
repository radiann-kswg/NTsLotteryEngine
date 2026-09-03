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
        public float maxSpeed = 8f;          // 水平・上向きがこれ以上速いのは挟まれ・弾け（落下チャンネルの自由落下は下向きなので除外）
        public float maxFallSpeed = 18f;     // 下向きの上限（11 段の落下チャンネル ≈ 12m → 15m/s は正常）
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
                var p = rb.position; var vel = rb.linearVelocity; float r = new Vector2(p.x, p.z).magnitude;
                float vh = new Vector2(vel.x, vel.z).magnitude;
                string why = vh > maxSpeed || vel.y > maxSpeed ? $"speed h={vh:F1} y={vel.y:F1}" : vel.y < -maxFallSpeed ? $"fall {-vel.y:F1}"
                    : p.y < minY ? $"below floor y={p.y:F2}" : r > maxR ? $"out of area r={r:F1}" : Escaped(b, p) ? $"escaped the tower y={p.y:F2}" : null;
                if (why == null) continue;
                reported.Add(b);
                var near = Physics.OverlapSphere(p, 0.08f, ~0, QueryTriggerInteraction.Ignore);
                var names = new List<string>();
                foreach (var c in near) if (c.attachedRigidbody != rb) names.Add(c.transform.parent ? c.transform.parent.name + "/" + c.name : c.name);
                string msg = $"[Derail] t={Time.time:F1}s {b.name} ({b.transform.parent?.name}) {why} at {p} v={rb.linearVelocity} touching={string.Join(",", names)}";
                events.Add(msg); Debug.LogWarning(msg);
            }
        }

        // 塔の球が地面の高さ（中心 y<0.12）で、完走トレイでも排出側（局所 z<−0.9: シュート・チャンネル・ハズレトレイ）でもない所に居る
        // = ボウルの縁を越えて落ちた等（ball 10 が投入直後に縁を越えて地面へ 2026-09-03）。1 段の塔は樋の床が y≈0.09 なので 0.2 だと誤検知した
        static bool Escaped(NumberBall b, Vector3 p)
        {
            if (p.y > 0.12f) return false;
            var k = b.GetComponentInParent<KuruunTower>(); if (!k) return false;
            if (k.transform.InverseTransformPoint(p).z < -0.9f) return false;
            return !(k.winTray && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(k.winTray.position.x, k.winTray.position.z)) < 0.25f);
        }
    }
}
