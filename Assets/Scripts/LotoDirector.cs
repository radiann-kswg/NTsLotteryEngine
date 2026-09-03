using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 進行役。**結果は物理が決める**: 上段の篩 → 下段の篩（到達順に 6 球）→ 別ボール 7 塔（コレクタの当たり扇形）。最後に JSON を書く。
    /// 起動引数: -seed N（投入ジッタの種。結果の再現は保証しない）/ -out path / -speed x / -quit
    /// </summary>
    public class LotoDirector : MonoBehaviour
    {
        [Header("References (built by LotoSceneBuilder)")]
        public NumberBall ballPrefab;
        public SieveMachine upper;      // 2〜11（4 層）
        public SieveMachine lower;      // 12〜99（15 層）
        public KuruunTower[] towers;    // LotoRules.Streaks と同じ並び
        public Camera cam;
        public Transform camMachine, camOverview;
        public Font hudFont;            // PenchantManufacture（CJK 未収録。HUD は英数字のみ）
        public BallSkinTable skins;     // 球ごとのテクスチャと創作DBリンク（Assets/Data/BallSkins.asset）

        [Header("Run")]
        public int seed = -1;           // -1 = 時刻から
        public string outPath = "result.json";
        public float speed = 1f;
        public bool quitWhenDone = false;
        public float pauseBetween = 1.5f;
        public bool skipSieves = false;  // デバッグ用: 篩を飛ばして塔から始める

        public LotoResult Result { get; private set; }
        readonly List<string> lines = new List<string>();
        string stage = "";

        void Start()
        {
            ParseArgs();
            if (seed < 0) seed = (int)(DateTime.Now.Ticks & 0x7fffffff);
            UnityEngine.Random.InitState(seed);
            Time.timeScale = speed;
            Physics.bounceThreshold = 0.5f;   // 既定 2 m/s 未満の衝突は反発ゼロ → 球が床に貼り付いて見える

            Result = new LotoResult { seed = seed, drawnAt = DateTime.Now.ToString("o"), six = new int[0], sixNames = new string[0], sixSorted = new int[0] };
            Result.streaks = Array.ConvertAll(LotoRules.Streaks, s =>
            {
                var c = skins ? skins.Character(BallSlot.Streak, s.ball) : null;
                return new StreakResult { ball = s.ball, max = s.max, targetP = s.targetP, nameJP = c?.nameJP ?? "", nameEN = c?.nameEN ?? "" };
            });

            if (!skipSieves)
            {
                // 投入位置（螺旋の index: 外側ほど半径が大きく高い）と番号の対応を毎回シャッフルする。
                // 番号順に置くと 2〜11 の篩は最外周・最上の 11 が 4 回連続で最下層に最初に着いた（2026-09-03）。位置の有利不利は残るが番号とは無関係になる
                // 生成順も番号順にしない（位置だけシャッフルしても最後に生成した 11 が 6 回連続で勝った。PhysX の処理順に依る偏りを疑う）
                int nU = LotoRules.SingleMax - LotoRules.SingleMin + 1, nL = LotoRules.SixMax - LotoRules.SixMin + 1;
                var slotU = Shuffled(nU); var slotL = Shuffled(nL);
                for (int k = 0; k < nU; k++) Skin(upper.Spawn(ballPrefab, LotoRules.SingleMin + slotU[k], k, nU));
                for (int k = 0; k < nL; k++) Skin(lower.Spawn(ballPrefab, LotoRules.SixMin + slotL[k], k, nL));
            }
            StartCoroutine(Run());
        }

        static int[] Shuffled(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (a[i], a[j]) = (a[j], a[i]); }   // Fisher–Yates（seed は Start で InitState 済み）
            return a;
        }

        string Name(BallSlot slot, int n) => (skins ? skins.Character(slot, n) : null)?.nameJP ?? "";
        void Skin(NumberBall b, BallSlot slot = BallSlot.Drum) { if (skins) skins.Apply(b, slot); }

        void ParseArgs()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                string Next() => i + 1 < a.Length ? a[++i] : "";
                switch (a[i])
                {
                    case "-seed": int.TryParse(Next(), out seed); break;
                    case "-out": outPath = Next(); break;
                    case "-speed": float.TryParse(Next(), out speed); break;
                    case "-quit": quitWhenDone = true; break;
                }
            }
        }

        IEnumerator Run()
        {
            if (!skipSieves)
            {
                Look(camMachine);
                stage = $"{LotoRules.SingleMin}-{LotoRules.SingleMax}";
                upper.Spin(); lower.Spin();
                yield return upper.DrawNext(b => { if (b) { Result.single = b.number; Result.singleName = Name(BallSlot.Drum, b.number); lines.Add($"{stage} : {b.number}"); } }, last: true, cam: cam);
                upper.Stop();
                yield return new WaitForSeconds(pauseBetween);

                stage = $"{LotoRules.SixMin}-{LotoRules.SixMax}";
                var drawn = new List<int>();
                for (int k = 0; k < LotoRules.SixCount; k++)
                {
                    yield return lower.DrawNext(b => { if (b) drawn.Add(b.number); }, last: k == LotoRules.SixCount - 1, cam: cam);
                    lines.RemoveAll(l => l.StartsWith(stage));
                    lines.Add($"{stage}: {string.Join(" ", drawn)}");
                    yield return new WaitForSeconds(pauseBetween);
                }
                lower.Stop();
                Result.six = drawn.ToArray();
                Result.sixNames = Array.ConvertAll(Result.six, n => Name(BallSlot.Drum, n));
                Result.sixSorted = (int[])Result.six.Clone(); Array.Sort(Result.sixSorted);
            }

            for (int i = 0; i < towers.Length; i++)
            {
                var k = towers[i]; var s = Result.streaks[i];
                var c = skins ? skins.Character(BallSlot.Streak, s.ball) : null;
                string label = c == null ? $"Ball {s.ball,2}" : $"Ball {s.ball,2} {c.shortEN}";   // HUD フォントは英数字のみ
                stage = label;
                var ball = Instantiate(ballPrefab, k.dropPoint.position, Quaternion.identity, k.transform);
                ball.name = $"Ball{s.ball:00}"; ball.number = s.ball; ball.Apply(); Skin(ball, BallSlot.Streak);
                yield return k.Run(ball, s.max, cam, (round, win) =>
                {
                    if (win) s.wins++;
                    lines.RemoveAll(l => l.StartsWith(label));
                    lines.Add($"{label}: {s.wins}/{s.max} " + (win ? "WIN" : "LOSE"));
                });
                yield return new WaitForSeconds(pauseBetween);
            }

            stage = "RESULT";
            Look(camOverview);
            Write();
            if (quitWhenDone) { yield return new WaitForSeconds(3f); Application.Quit(); }
        }

        void Look(Transform anchor)
        {
            if (!anchor) return;
            cam.fieldOfView = 60f;   // 塔追従（KuruunTower.camFov）から戻す
            cam.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }

        void Write()
        {
            try
            {
                var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, JsonUtility.ToJson(Result, true));
                Debug.Log($"[Loto] wrote {Path.GetFullPath(outPath)}");
            }
            catch (Exception e) { Debug.LogError($"[Loto] write failed: {e.Message}"); }
        }

        GUIStyle style;
        void OnGUI()
        {
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 30f), richText = true, font = hudFont ? hudFont : GUI.skin.label.font };
            float pad = Screen.height / 40f;
            var text = $"<b>{stage}</b>\n" + string.Join("\n", lines);
            var size = style.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(pad, pad, size.x + pad * 2, size.y + pad * 2), GUIContent.none);
            GUI.Label(new Rect(pad * 2, pad * 2, size.x, size.y), text, style);
        }
    }
}
