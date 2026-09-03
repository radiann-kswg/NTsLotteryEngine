using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NTsLotoEngine.EditorTools
{
    /// <summary>
    /// クルーン 1 段（回転ボウル＋静止コレクタ）のモンテカルロ治具。エディタ（非 Play）で Physics.Simulate を手回しし、
    /// 並列コピー × バッチで数千〜数万試行の当たり率 p を実測する（Wilson 95% 区間つき）。
    /// Tools > NTsLoto > Monte Carlo > Run。設定は静的フィールド（MCP の RunCommand から書き換えて再実行できる）。
    /// 結果: Console と Output/mc_&lt;variant&gt;.json。
    /// 座標規約: FBX は Unity 角 = Blender θ + 180°（LotoSceneBuilder.BlenderDegOffset。2026-09-03 レイキャスト実測）。
    /// </summary>
    public static class LotoMonteCarlo
    {
        // ---- 設定（RunCommand から変更可）----
        public static string variant = "p10";      // Kuruun_Collector_<variant>.fbx
        public static int trials = 2000;
        public static int parallel = 64;            // 同時に回すコピー数（x 方向 3m ピッチ）
        public static float bowlRpm = 10f;
        public static float entryR = 0.85f;         // 投入半径（コーン面上。喉から落ちてくる想定。LotoSceneBuilder.EntryR と同じ）
        public static float entryHeight = 0.39f;    // コーン面からの落下高さ（喉 −0.70 → 次段コーン面 −1.09）
        public static float entryTangential = 0f;   // 接線速度（最上段の投入を模すとき > 0）
        public static float maxSeconds = 25f;
        public static int stepsPerUpdate = 200;
        public static int seed = 1;

        const float Dt = 0.02f, WinR = 0.36f, LoseR = 0.98f, BelowY = -0.45f;   // コレクタ内縁 z=-0.36 より下で当たり確定（kuruun_params.json の collector と合わせる）
        const string BowlPath = "Assets/Models/Kuruun_Bowl.fbx";
        const string BallPath = "Packages/net.numbertales-radiann.lotteryballkit/Prefabs/NumberBall.prefab";

        class Copy { public Transform root; public Rigidbody bowl, ball; public float t; public int outcome; /* 0 running 1 win 2 lose 3 timeout */ }
        static readonly List<Copy> copies = new List<Copy>();
        static GameObject rigRoot;
        static System.Random rng;
        static int done, win, lose, timeout, launched;
        static double sumT;
        static readonly List<string> timeoutNotes = new List<string>();
        static SimulationMode prevMode;
        static bool running;

        [MenuItem("Tools/NTsLoto/Monte Carlo/Run")]
        public static void Run()
        {
            if (running) { Debug.LogWarning("[MC] already running"); return; }
            var bowlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BowlPath);
            var colPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Kuruun_Collector_{variant}.fbx");
            var ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPath);
            if (!bowlPrefab || !colPrefab || !ballPrefab) { Debug.LogError($"[MC] prefab missing: bowl={bowlPrefab} collector={colPrefab} ball={ballPrefab}"); return; }

            rng = new System.Random(seed);
            done = win = lose = timeout = launched = 0; sumT = 0; timeoutNotes.Clear(); copies.Clear();
            rigRoot = new GameObject("__MC");
            for (int i = 0; i < parallel; i++)
            {
                var c = new Copy();
                c.root = new GameObject($"MC{i}").transform; c.root.SetParent(rigRoot.transform, false);
                c.root.position = new Vector3(1000f + i * 3f, 0f, 0f);
                var bowl = (GameObject)PrefabUtility.InstantiatePrefab(bowlPrefab, c.root);
                bowl.transform.localPosition = Vector3.zero;
                AddMeshColliders(bowl);
                c.bowl = bowl.AddComponent<Rigidbody>(); c.bowl.isKinematic = true; c.bowl.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                bowl.transform.rotation = Quaternion.AngleAxis((float)rng.NextDouble() * 360f, Vector3.up) * bowl.transform.rotation;
                var col = (GameObject)PrefabUtility.InstantiatePrefab(colPrefab, c.root);
                col.transform.localPosition = Vector3.zero;
                AddMeshColliders(col);
                var ball = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, c.root);
                c.ball = BallUtil.Prepare(ball.GetComponent<NumberBall>());
                Launch(c);
                copies.Add(c);
            }
            Physics.SyncTransforms();
            prevMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            running = true;
            EditorApplication.update += Step;
            Debug.Log($"[MC] start variant={variant} trials={trials} parallel={parallel} rpm={bowlRpm} entryR={entryR} h={entryHeight} vt={entryTangential} seed={seed}");
        }

        [MenuItem("Tools/NTsLoto/Monte Carlo/Stop")]
        public static void Stop() => Finish(true);

        static void AddMeshColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh; mc.sharedMaterial = BallUtil.Machine;   // 実機（LotoSceneBuilder.Fbx）と同じ
            }
        }

        static void Launch(Copy c)
        {
            float a = (float)rng.NextDouble() * 360f * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            var tangent = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));   // 角度増加方向（ボウル回転と同じ向き）
            float coneY = 0.28f * Mathf.Clamp01((entryR - 0.65f) / 0.35f);   // kuruun_params.json の bowl: cone (1.0,0.28)→(0.65,0)
            float jr = ((float)rng.NextDouble() - 0.5f) * 0.02f;
            c.ball.position = c.root.position + radial * (entryR + jr) + Vector3.up * (coneY + 0.05f + entryHeight);
            c.ball.rotation = UnityEngine.Random.rotation;
            c.ball.linearVelocity = tangent * entryTangential;
            c.ball.angularVelocity = Vector3.zero;
            c.ball.angularDamping = 0.05f; c.ball.linearDamping = 0f;   // 校正は素の物理で（演出用の減衰は塔側の値に合わせて別途測る）
            c.t = 0f; c.outcome = 0; launched++;
        }

        static void Step()
        {
            try
            {
                for (int s = 0; s < stepsPerUpdate && running; s++)
                {
                    var spin = Quaternion.AngleAxis(bowlRpm * 6f * Dt, Vector3.up);
                    foreach (var c in copies) if (c.outcome == 0) c.bowl.MoveRotation(spin * c.bowl.rotation);
                    Physics.Simulate(Dt);
                    foreach (var c in copies)
                    {
                        if (c.outcome != 0) continue;
                        c.t += Dt;
                        var lp = c.ball.position - c.root.position;
                        float r = new Vector2(lp.x, lp.z).magnitude;
                        if (lp.y < BelowY && r < WinR) c.outcome = 1;
                        else if (r > LoseR || lp.y < -1f) c.outcome = 2;
                        else if (c.t > maxSeconds) { c.outcome = 3; timeoutNotes.Add($"r={r:F2} y={lp.y:F2} v={c.ball.linearVelocity.magnitude:F2}"); }
                        if (c.outcome != 0)
                        {
                            done++; sumT += c.t;
                            if (c.outcome == 1) win++; else if (c.outcome == 2) lose++; else timeout++;
                            if (launched < trials) Launch(c);
                        }
                    }
                    if (done >= trials) { Finish(false); return; }
                }
                if (done % 200 < parallel && done > 0) Debug.Log($"[MC] {done}/{trials} p={(double)win / Math.Max(1, win + lose):F4} timeouts={timeout}");
            }
            catch (Exception e) { Debug.LogException(e); Finish(true); }
        }

        static void Finish(bool aborted)
        {
            if (!running) return;
            running = false;
            EditorApplication.update -= Step;
            Physics.simulationMode = prevMode;
            if (rigRoot) UnityEngine.Object.DestroyImmediate(rigRoot);
            int n = win + lose;
            double p = n > 0 ? (double)win / n : 0;
            var (lo, hi) = Wilson(win, n);
            var report = new Report
            {
                variant = variant, trials = done, win = win, lose = lose, timeout = timeout, p = p, ci95lo = lo, ci95hi = hi,
                meanSeconds = done > 0 ? sumT / done : 0, bowlRpm = bowlRpm, entryR = entryR, entryHeight = entryHeight, entryTangential = entryTangential, seed = seed,
                aborted = aborted, timeoutSamples = timeoutNotes.GetRange(0, Math.Min(10, timeoutNotes.Count)).ToArray(),
            };
            Directory.CreateDirectory("Output");
            File.WriteAllText($"Output/mc_{variant}.json", JsonUtility.ToJson(report, true));
            Debug.Log($"[MC] {(aborted ? "ABORTED " : "")}variant={variant} n={n} win={win} lose={lose} timeout={timeout} p={p:F4} 95%CI=[{lo:F4},{hi:F4}] (±{(hi - lo) / 2:F4}) meanT={report.meanSeconds:F1}s");
        }

        static (double, double) Wilson(int k, int n)
        {
            if (n == 0) return (0, 0);
            double z = 1.96, p = (double)k / n, d = 1 + z * z / n;
            double c = (p + z * z / (2 * n)) / d, h = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n)) / d;
            return (c - h, c + h);
        }

        [Serializable]
        class Report
        {
            public string variant; public int trials, win, lose, timeout; public double p, ci95lo, ci95hi, meanSeconds;
            public float bowlRpm, entryR, entryHeight, entryTangential; public int seed; public bool aborted; public string[] timeoutSamples;
        }
    }
}
