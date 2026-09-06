using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>
    /// README 冒頭のプレビュー画像を撮り直す（`docs/captures/`。git 管轄内）。NTsMedalGame の `MedalCapture` と同じ流儀。
    /// - `Tools &gt; NTsLoto &gt; Capture Preview` … LotoScene を **Play 中**に。篩・塔の弧・段数最大の塔の 3 枚。
    /// - `Tools &gt; NTsLoto &gt; Capture Ball Skins` … **BallViewScene を開いた状態**（Edit でよい）で。貼り済みの球を 1 枚ずつ撮り、
    ///   README のマーカー間にテクスチャ収録状況の表を書き戻す（GitHub から収録状況が常時見える）。
    /// 動画は `Tools &gt; NTsLoto &gt; Play + Record`（Unity Recorder → `Recordings/*.mp4`・git 管轄外）→ ffmpeg で GIF 化。
    /// </summary>
    public static class LotoCapture
    {
        const string Dir = "docs/captures";
        const string BallViewScene = "Assets/Scenes/BallViewScene.unity";
        const string Readme = "README.md";
        const string Begin = "<!-- ballskins:start -->", End = "<!-- ballskins:end -->";

        static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>cam の現在の画角で PNG を 1 枚書く（Play 中でも可）。</summary>
        public static void Shot(Camera cam, string file, int w = 1280, int h = 720)
        {
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prevTarget;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prevActive;

            var path = Path.Combine(Repo, Dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            Debug.Log($"[LotoCapture] {Dir}/{file} ({w}x{h})");
        }

        [MenuItem("Tools/NTsLoto/Capture Preview")]
        public static void Preview()
        {
            var cam = Camera.main;
            if (!cam) { Debug.LogError("[LotoCapture] Camera.main が無い。LotoScene を Play 中に実行する"); return; }

            var pos = cam.transform.position; var rot = cam.transform.rotation; var fov = cam.fieldOfView;

            if (Aim(cam, GameObject.Find("Cam_Machine"))) Shot(cam, "preview_sieve.png");
            if (Aim(cam, GameObject.Find("Cam_Overview"))) Shot(cam, "preview_towers.png");

            // 段数が最大の塔（33 = 11 段）の上から CloseupLevels 段を寄りで撮る。
            // 全段（11×1.45m）を入れると 20m 引くことになり、弧の隣の塔が前に被る。
            var tower = Object.FindObjectsByType<KuruunTower>(FindObjectsSortMode.None)
                              .Where(t => t.levels != null && t.levels.Length > 0 && t.camAnchor)
                              .OrderByDescending(t => t.levels.Length).FirstOrDefault();
            if (tower)
            {
                const int CloseupLevels = 3;
                const float Fov = 40f;
                int last = Mathf.Min(CloseupLevels, tower.levels.Length) - 1;
                float top = tower.levels[0].bowl.position.y, bottom = tower.levels[last].bowl.position.y;
                var center = new Vector3(tower.transform.position.x, (top + bottom) * 0.5f, tower.transform.position.z);
                var dir = (tower.camAnchor.position - center); dir.y = 0f; dir.Normalize();
                float height = top - bottom + 1.6f;   // 上下に余白（コレクタ・漏斗の分）
                var eye = center + dir * (height * 0.5f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad));
                cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(center - eye));
                cam.fieldOfView = Fov;
                Shot(cam, "preview_tower.png", 720, 1280);   // 縦長（塔は高い）
            }
            else Debug.LogWarning("[LotoCapture] KuruunTower が見つからない（塔の寄りは撮っていない）");

            cam.transform.SetPositionAndRotation(pos, rot); cam.fieldOfView = fov;
        }

        static bool Aim(Camera cam, GameObject anchor)
        {
            if (!anchor) { Debug.LogWarning("[LotoCapture] カメラアンカーが見つからない（Build Loto Scene 済み？）"); return false; }
            cam.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
            cam.fieldOfView = 60f;   // LotoDirector.Look と同じ既定
            return true;
        }

        [MenuItem("Tools/NTsLoto/Capture Ball Skins")]
        public static void BallSkins()
        {
            // 別シーンを開くと未保存の変更で確認ダイアログが出る（MCP から打つと "User interactions are not supported"）。開くのは User。
            if (SceneManager.GetActiveScene().path != BallViewScene)
            { Debug.LogError($"[LotoCapture] {BallViewScene} を開いてから実行する（Tools > NTsLoto > Build Ball View Scene）"); return; }

            var viewer = Object.FindFirstObjectByType<BallSkinViewer>();
            var cam = Camera.main;
            if (!viewer || !viewer.skins || !cam) { Debug.LogError("[LotoCapture] BallSkinViewer / BallSkinTable / Camera.main が揃っていない"); return; }

            int slot = (int)viewer.slot, number = viewer.number;   // 撮り終わったら戻す
            var rows = new StringBuilder();
            var done = viewer.skins.skins.Where(s => s.texture).ToList();
            foreach (var s in done)
            {
                viewer.slot = s.slot; viewer.number = s.number; viewer.ApplySkin();
                var file = $"ball_{s.slot}_{s.DbNum}.png";
                Shot(cam, file, 320, 320);
                rows.AppendLine($"| <img src=\"{Dir}/{file}\" width=\"96\"> | {(s.slot == BallSlot.Drum ? "ロト" : "別ボール")} | {s.number} | `{s.DbNum}` | `BallTex_NTS-{s.DbNum}.png` |");
            }
            viewer.slot = (BallSlot)slot; viewer.number = number; viewer.ApplySkin();

            WriteReadmeTable(done.Count, viewer.skins.skins.Count, rows.ToString());
        }

        /// <summary>README のマーカー間を収録状況の表で置き換える（マーカーが無ければ警告だけ。README の他の行は触らない）。</summary>
        static void WriteReadmeTable(int done, int total, string rows)
        {
            var path = Path.Combine(Repo, Readme);
            var text = File.ReadAllText(path);
            int a = text.IndexOf(Begin), b = text.IndexOf(End);
            if (a < 0 || b < a) { Debug.LogWarning($"[LotoCapture] README に {Begin} / {End} が無いので表は書いていない"); return; }

            var body = new StringBuilder();
            body.AppendLine(Begin);
            body.AppendLine();
            body.AppendLine($"**収録 {done} / {total} 球**（{System.DateTime.Now:yyyy-MM-dd} 時点。`Tools > NTsLoto > Capture Ball Skins` が自動更新）");
            body.AppendLine();
            if (done > 0)
            {
                body.AppendLine("| | 区分 | 番号 | Num_Badge | ファイル |");
                body.AppendLine("| --- | --- | --- | --- | --- |");
                body.Append(rows);
            }
            else body.AppendLine("> まだ 1 球も貼られていない。");
            body.AppendLine();
            body.Append(End);

            File.WriteAllText(path, text.Substring(0, a) + body + text.Substring(b + End.Length));
            Debug.Log($"[LotoCapture] README のボールテクスチャ表を更新（{done}/{total}）");
        }
    }
}
