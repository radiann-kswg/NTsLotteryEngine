using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 篩型ロトマシーン（2026-09-03 User 指示）。穴あきの回転皿を N 層重ね、球は穴を見つけるたびに下の層へ落ちる。
    /// 最下層の受け口（exit トリガー）に**到達した順**がそのまま抽選順。当選の決定に RNG は使わない。
    /// 皿は Blender 生成（Assets/Models/Sieve_Dish_*.fbx）。配置は LotoSceneBuilder.BuildSieve。
    /// </summary>
    public class SieveMachine : MonoBehaviour
    {
        [Header("References (built by LotoSceneBuilder)")]
        public Rotator[] dishes;          // 0 = 最上層
        public BallTrigger exit;          // 最下層の受け口の下（到達 = 抽選）
        public GameObject gate;           // 喉の栓（Stop で閉じる。残りの球を出さない）
        public Transform spawnCenter;     // 最上層の皿の上
        public float spawnRadius = 0.4f;

        [Header("Tuning")]
        public float dishRpm = 8f;        // 層ごとに向きを交互にする（LotoSceneBuilder）

        public readonly List<NumberBall> balls = new List<NumberBall>();
        readonly Queue<NumberBall> arrived = new Queue<NumberBall>();   // 受け口に着いた順（抽選の合間に着いた球も落とさない）

        void Awake() { if (exit) exit.Entered += b => { if (balls.Remove(b)) arrived.Enqueue(b); }; }

        public NumberBall Spawn(NumberBall prefab, int number, int index, int count)
        {
            // 最上層の皿の上に螺旋状にばらまく（同じ点に重ねると初期の押し合いで飛ぶ。RSC 罠）
            float a = index * 2.399963f;                                  // 黄金角
            float r = spawnRadius * Mathf.Sqrt((index + 0.5f) / count);
            var pos = spawnCenter.position + new Vector3(Mathf.Sin(a) * r, 0.02f * index, Mathf.Cos(a) * r);   // 高さもずらす（同じ高さに敷き詰めると初期の押し合いで飛ぶ）
            var b = Instantiate(prefab, pos, UnityEngine.Random.rotation, transform);
            b.name = $"Ball{number:00}";
            b.number = number;
            b.Apply();
            BallUtil.Prepare(b);
            balls.Add(b);
            return b;
        }

        public void Spin() { for (int i = 0; i < dishes.Length; i++) dishes[i].targetRpm = (i % 2 == 0 ? 1f : -1f) * dishRpm; }
        public void Stop() { foreach (var d in dishes) d.targetRpm = 0f; if (gate) gate.SetActive(true); }

        /// <summary>受け口に次に到達した球を返す（既に着いていればすぐ）。timeout 秒で来なければ null。</summary>
        public IEnumerator DrawNext(Action<NumberBall> onDrawn, bool last = false, float timeout = 180f)
        {
            float t = 0f, nextLog = 10f;
            while (arrived.Count == 0 && t < timeout)
            {
                t += Time.fixedDeltaTime;
                if (t > nextLog) { Debug.Log($"[{name}] waiting {t:F0}s, balls left={balls.Count}, lowest y={Lowest():F2}"); nextLog += 10f; }
                yield return new WaitForFixedUpdate();
            }
            if (arrived.Count == 0) { Debug.LogWarning($"[{name}] no ball reached the exit in {timeout}s"); onDrawn?.Invoke(null); yield break; }
            if (last && gate) gate.SetActive(true);   // 最後の 1 球が着いた瞬間に喉を塞ぐ（次の球をレールへ出さない）
            onDrawn?.Invoke(arrived.Dequeue());
        }

        float Lowest()
        {
            float y = float.MaxValue;
            foreach (var b in balls) if (b) y = Mathf.Min(y, b.transform.position.y);
            return y;
        }
    }
}
