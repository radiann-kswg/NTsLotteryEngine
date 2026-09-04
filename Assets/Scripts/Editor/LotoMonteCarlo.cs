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
        public static float entryHeight = 0.59f;    // コーン面からの落下高さ（喉 −0.70 → 次段コーン面 −1.45+0.16。LotoSceneBuilder.TowerPitch と連動）
        public static float entryTangential = 0f;   // 接線速度（最上段の投入を模すとき > 0）
        // 投入方位（Unity 角 atan2(x,z)・度）。NaN = 毎試行ランダム（校正時の既定）。
        // 実機の塔は喉が段ごとに x=±0.425 交互なので方位が固定される: 最上段 135°／奇数段 270°／偶数段(≥2) 90°
        // （p10/p6of88/p18 の当たり扇形は Blender 90° = Unity 270°。奇数段は当たり扇形の真上に落ちる）。段ごとの p はこれを固定して測る
        public static float entryAzimuth = float.NaN;
        public static float maxSeconds = 25f;
        public static int stepsPerUpdate = 2500;   // エディタが非アクティブだと update が間引かれる（200 だと 10 倍遅い）。1 update ≈ 3s
        public static int seed = 1;

        const float Dt = 0.02f, WinR = 0.36f, LoseR = 0.98f, BelowY = -0.45f;   // コレクタ内縁 z=-0.36 より下で当たり確定（kuruun_params.json の collector と合わせる）
        const string BowlPath = "Assets/Models/Kuruun_Bowl.fbx";
        const string BallPath = "Packages/net.numbertales-radiann.lotteryballkit/Prefabs/NumberBall.prefab";

        class Copy { public Transform root; public Rigidbody bowl, ball; public float t, elapsed; public int outcome, index, timeouts; /* 0 running 1 win 2 lose 3 timeout */ }
        // 診断: コピー内の試行番号（5 試行ずつ）と経過時間（30s ずつ）で p を層別する（初期の窓だけ p が高い 2026-09-03）
        static readonly int[] binIdxWin = new int[40], binIdxN = new int[40], binTimeWin = new int[40], binTimeN = new int[40];
        static readonly List<Copy> copies = new List<Copy>();
        static GameObject rigRoot;
        static System.Random rng;
        static int done, win, lose, timeout, launched, lastWindowDone, lastWindowWin;
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
            done = win = lose = timeout = launched = lastWindowDone = lastWindowWin = 0; sumT = 0; timeoutNotes.Clear(); copies.Clear();
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
            Debug.Log($"[MC] start variant={variant} az={(float.IsNaN(entryAzimuth) ? "random" : entryAzimuth + "deg")} trials={trials} parallel={parallel} rpm={bowlRpm} entryR={entryR} h={entryHeight} vt={entryTangential} seed={seed}");
        }

        [MenuItem("Tools/NTsLoto/Monte Carlo/Stop")]
        public static void Stop() { queue.Clear(); Finish(true); }

        /// <summary>variant を順に回す（校正ループ用。RunCommand から呼ぶ）。各 variant の結果は Output/mc_&lt;variant&gt;.json。</summary>
        static readonly Queue<(string v, float az)> queue = new Queue<(string, float)>();
        public static void RunBatch(IEnumerable<string> variants) { foreach (var v in variants) queue.Enqueue((v, float.NaN)); Next(); }

        /// <summary>(variant × 投入方位) の総当たり。段ごとの p を測る用（方位は Unity 角・度）。結果は Output/mc_&lt;variant&gt;_a&lt;deg&gt;.json。</summary>
        public static void RunGrid(IEnumerable<string> variants, IEnumerable<float> azimuths)
        {
            foreach (var v in variants) foreach (var a in azimuths) queue.Enqueue((v, a));
            Next();
        }

        static void Next() { if (queue.Count > 0 && !running) { var q = queue.Dequeue(); variant = q.v; entryAzimuth = q.az; Run(); } }

        static string Tag => float.IsNaN(entryAzimuth) ? variant : $"{variant}_a{Mathf.RoundToInt(entryAzimuth)}";

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
            float a = (float.IsNaN(entryAzimuth) ? (float)rng.NextDouble() * 360f : entryAzimuth) * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
            var tangent = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));   // 角度増加方向（ボウル回転と同じ向き）
            float coneY = 0.28f * Mathf.Clamp01((entryR - 0.65f) / 0.35f);   // kuruun_params.json の bowl: cone (1.0,0.28)→(0.65,0)
            float jr = ((float)rng.NextDouble() - 0.5f) * 0.02f;
            var pos = c.root.position + radial * (entryR + jr) + Vector3.up * (coneY + 0.05f + entryHeight);
            // transform も動かす。rb.position だけだと Run() 直後の Physics.SyncTransforms がプレハブ初期位置（ボウル中心 y=0）で上書きし、
            // 各コピーの 1 試行目がドームの穴から真下（当たり判定）へ落ちて p が +parallel/trials 分かさ上げされていた（2026-09-03。20k 試行で +0.013）
            c.ball.transform.position = pos; c.ball.position = pos;
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
                        else if (c.t > maxSeconds)
                        {
                            c.outcome = 3; c.timeouts++;
                            var near = Physics.OverlapSphere(c.ball.position, 0.06f, ~0, QueryTriggerInteraction.Ignore);
                            var names = new List<string>(); foreach (var col in near) if (col.attachedRigidbody != c.ball) names.Add(col.name);
                            timeoutNotes.Add($"copy={copies.IndexOf(c)}(#{c.timeouts}) r={r:F2} y={lp.y:F2} v={c.ball.linearVelocity.magnitude:F2} w={c.ball.angularVelocity.magnitude:F1} touching={string.Join("/", names)}");
                        }
                        if (c.outcome != 0)
                        {
                            done++; sumT += c.t; c.elapsed += c.t;
                            if (c.outcome == 1) win++; else if (c.outcome == 2) lose++; else timeout++;
                            if (c.outcome != 3)
                            {
                                int bi = Math.Min(39, c.index / 5), bt = Math.Min(39, (int)(c.elapsed / 30f));
                                binIdxN[bi]++; binTimeN[bt]++; if (c.outcome == 1) { binIdxWin[bi]++; binTimeWin[bt]++; }
                            }
                            c.index++;
                            if (launched < trials) Launch(c);
                        }
                    }
                    if (done >= trials) { Finish(false); return; }
                }
                if (done - lastWindowDone >= 10000)
                {
                    // 窓ごとの p（時間経過で p がずれていないかを見る。累積だけだと初期の偏りに埋もれる 2026-09-03）
                    var q = copies[0].bowl.rotation; float qn = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    Debug.Log($"[MC] {done}/{trials} window p={(double)(win - lastWindowWin) / Math.Max(1, done - lastWindowDone):F4} cumulative p={(double)win / Math.Max(1, win + lose):F4} timeouts={timeout} |q|={qn:F6} scale={copies[0].bowl.transform.lossyScale.x:F5}");
                    lastWindowDone = done; lastWindowWin = win;
                }
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
                meanSeconds = done > 0 ? sumT / done : 0, bowlRpm = bowlRpm, entryR = entryR, entryHeight = entryHeight, entryTangential = entryTangential,
                entryAzimuth = entryAzimuth, seed = seed,
                aborted = aborted, timeoutSamples = timeoutNotes.GetRange(0, Math.Min(20, timeoutNotes.Count)).ToArray(),
            };
            Directory.CreateDirectory("Output");
            File.WriteAllText($"Output/mc_{Tag}.json", JsonUtility.ToJson(report, true));
            Debug.Log($"[MC] {(aborted ? "ABORTED " : "")}variant={Tag} n={n} win={win} lose={lose} timeout={timeout} p={p:F4} 95%CI=[{lo:F4},{hi:F4}] (±{(hi - lo) / 2:F4}) meanT={report.meanSeconds:F1}s");
            var sb = new System.Text.StringBuilder("[MC] p by trial index (5 each): ");
            for (int i = 0; i < 40 && binIdxN[i] > 0; i++) sb.Append($"{(double)binIdxWin[i] / binIdxN[i]:F3} ");
            sb.Append("\n[MC] p by copy elapsed (30s each): ");
            for (int i = 0; i < 40 && binTimeN[i] > 0; i++) sb.Append($"{(double)binTimeWin[i] / binTimeN[i]:F3} ");
            Debug.Log(sb.ToString());
            Array.Clear(binIdxWin, 0, 40); Array.Clear(binIdxN, 0, 40); Array.Clear(binTimeWin, 0, 40); Array.Clear(binTimeN, 0, 40);
            if (!aborted) Next();   // delayCall はエディタが非アクティブだと発火せずバッチが止まった（2026-09-03）
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
            public float bowlRpm, entryR, entryHeight, entryTangential, entryAzimuth; public int seed; public bool aborted; public string[] timeoutSamples;
        }
    }
}
