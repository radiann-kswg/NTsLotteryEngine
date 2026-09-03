using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NTsLotoEngine
{
    /// <summary>
    /// 進行役。起動時に LotoRules で結果を確定し、上段→下段→別ボール 7 塔の順に演出、最後に JSON を書く。
    /// 起動引数: -seed N / -out path / -speed x / -quit
    /// </summary>
    public class LotoDirector : MonoBehaviour
    {
        [Header("References (built by LotoSceneBuilder)")]
        public NumberBall ballPrefab;
        public LotoDrumTier upper;      // 1〜11
        public LotoDrumTier lower;      // 12〜99
        public KuruunTower[] towers;    // LotoRules.Streaks と同じ並び
        public Camera cam;
        public Transform camMachine, camOverview;
        public Font hudFont;            // PenchantManufacture（CJK 未収録。HUD は英数字のみ）

        [Header("Run")]
        public int seed = -1;           // -1 = 時刻から
        public string outPath = "result.json";
        public float speed = 1f;
        public bool quitWhenDone = false;
        public float pauseBetween = 1.5f;
        public bool skipDrums = false;   // デバッグ用: ロトマシーンを飛ばして塔から始める

        public LotoResult Result { get; private set; }
        readonly List<string> lines = new List<string>();
        string stage = "";

        void Start()
        {
            ParseArgs();
            if (seed < 0) seed = (int)(DateTime.Now.Ticks & 0x7fffffff);
            Time.timeScale = speed;
            Physics.IgnoreLayerCollision(LotoLayers.ChosenBall, LotoLayers.Blocker, true);

            Result = LotoRules.Draw(seed);
            Debug.Log($"[Loto] seed={seed} single={Result.single} six=[{string.Join(",", Result.six)}] streaks=[{string.Join(",", Array.ConvertAll(Result.streaks, s => $"{s.ball}:{s.wins}/{s.max}"))}]");

            for (int n = LotoRules.SingleMin; n <= LotoRules.SingleMax; n++) upper.Spawn(ballPrefab, n);
            for (int n = LotoRules.SixMin; n <= LotoRules.SixMax; n++) lower.Spawn(ballPrefab, n);
            StartCoroutine(Run());
        }

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
            if (!skipDrums)
            {
                Look(camMachine);
                stage = "1-11";
                lower.Stir(); // 下段も先に回しておくと絵が寂しくない
                yield return upper.Draw(Result.single, b => lines.Add($"1-11 : {b.number}"));
                upper.Stop();
                yield return new WaitForSeconds(pauseBetween);

                stage = "12-99";
                var drawn = new List<int>();
                foreach (int n in Result.six)
                {
                    yield return lower.Draw(n, b => { drawn.Add(b.number); });
                    lines.RemoveAll(l => l.StartsWith("12-99"));
                    lines.Add($"12-99: {string.Join(" ", drawn)}");
                    yield return new WaitForSeconds(pauseBetween);
                }
                lower.Stop();
            }

            for (int i = 0; i < towers.Length; i++)
            {
                var k = towers[i]; var s = Result.streaks[i];
                stage = $"Ball {s.ball}";
                var ball = Instantiate(ballPrefab, k.dropPoint.position, Quaternion.identity, k.transform);
                ball.name = $"Ball{s.ball:00}"; ball.number = s.ball; ball.Apply();
                int wins = 0;
                yield return k.Run(ball, s.wins, s.max, cam, (round, win) =>
                {
                    if (win) wins++;
                    lines.RemoveAll(l => l.StartsWith($"Ball {s.ball,2}"));
                    lines.Add($"Ball {s.ball,2}: {wins}/{s.max} " + (win ? "WIN" : "LOSE"));
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
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 30f), richText = true, font = hudFont ? hudFont : GUI.skin.label.font };
                Debug.Log($"[Loto] HUD font = {style.font.name}");
            }
            float pad = Screen.height / 40f;
            var text = $"<b>{stage}</b>\n" + string.Join("\n", lines);
            var size = style.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(pad, pad, size.x + pad * 2, size.y + pad * 2), GUIContent.none);
            GUI.Label(new Rect(pad * 2, pad * 2, size.x, size.y), text, style);
        }
    }
}
