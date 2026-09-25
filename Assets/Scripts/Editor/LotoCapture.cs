using System.Collections.Generic;
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
    ///   正面の静止画 `ball_<slot>_<badge>.png` に加えて、顔と頭頂の番号が 1 枚で見える俯瞰 65° の `ball_<slot>_<badge>_top.png` と、
    ///   頭頂軸まわりに 1 周する回転 GIF `ball_<slot>_<badge>.gif` も書く（GifWriter・ffmpeg 不要）。
    ///   表は区分（ロト / 別ボール）ごとに `&lt;details&gt;` で畳む（球が増えても README が縦に伸びない）。
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

        // 回転 GIF: 36 コマ × 10° を 8cs（12.5fps・1 周 2.9 秒）。128px（表示 96px の Retina 相当・1 球 150〜200KB）。
        const int SpinFrames = 36, SpinSize = 128, SpinDelayCs = 8;
        // 俯瞰静止画: 頭頂の番号デカール（LotteryBall.fbx のローカル +Z 極）と顔（−Y）を 1 枚に収める。見下ろし角 [deg]（正面の静止画は 20°）。
        const float TopLookDown = 65f;
        const int StillSize = 320;
        // 正面・俯瞰・GIF は 2 倍で描いて縮める（RT に MSAA が無いのでジャギ取り）。シーンのカメラの画角だと球が枠の半分しかなく
        // 表の 96px 表示では小さいので、画角を tan 比で絞って球を枠いっぱいに寄せる（3 種とも同じ寄り。正面の姿勢は従来の見下ろし 20° のまま）。
        const int Super = 2;
        const float Zoom = 0.64f;

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
            var ball = viewer.transform;
            var pose = ball.rotation;
            var topPose = Quaternion.Euler(TopLookDown - 90f, 180f, 0f);   // DefaultPose = Euler(-70,180,0) が見下ろし 20°
            var rows = new Dictionary<BallSlot, StringBuilder>();
            var done = viewer.skins.skins.Where(s => s.texture).ToList();
            try
            {
                for (int n = 0; n < done.Count; n++)
                {
                    var s = done[n];
                    EditorUtility.DisplayProgressBar("Capture Ball Skins", $"{s.slot} #{s.number}", (float)n / done.Count);
                    viewer.slot = s.slot; viewer.number = s.number; viewer.ApplySkin();
                    var badge = s.Badge;   // Num_Badge（DbNum とは別。別ボール 2 = バイナは 2B）

                    ball.rotation = pose;   // 正面（姿勢は従来どおり。GIF・俯瞰で回した姿勢を毎回戻してから撮る）
                    var file = $"ball_{s.slot}_{badge}.png";
                    StillShot(cam, file);

                    ball.rotation = topPose;
                    var top = $"ball_{s.slot}_{badge}_top.png";
                    StillShot(cam, top);

                    var gif = $"ball_{s.slot}_{badge}.gif";
                    Spin(cam, ball, pose, gif);
                    ball.rotation = pose;

                    var texFile = Path.GetFileName(AssetDatabase.GetAssetPath(s.texture));   // 実際に貼られているファイル（手貼りの上書きも正しく出る）
                    if (!rows.TryGetValue(s.slot, out var sb)) rows[s.slot] = sb = new StringBuilder();
                    sb.AppendLine($"| <img src=\"{Dir}/{file}\" width=\"96\"> | <img src=\"{Dir}/{top}\" width=\"96\"> | <img src=\"{Dir}/{gif}\" width=\"96\"> | {s.number} | `{badge}` | `{texFile}` |");
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ball.rotation = pose;
                viewer.slot = (BallSlot)slot; viewer.number = number; viewer.ApplySkin();
            }

            var totals = viewer.skins.skins.GroupBy(s => s.slot).ToDictionary(g => g.Key, g => g.Count());
            WriteReadmeTable(done.Count, viewer.skins.skins.Count, totals, rows);
        }

        /// <summary>今の姿勢のまま静止画（寄り・StillSize 四方）を PNG で書く。</summary>
        static void StillShot(Camera cam, string file)
        {
            var tex = new Texture2D(StillSize, StillSize, TextureFormat.RGB24, false);
            tex.SetPixels32(Grab(cam, StillSize)); tex.Apply();
            var path = Path.Combine(Repo, Dir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[LotoCapture] {Dir}/{file} ({StillSize}x{StillSize})");
        }

        /// <summary>球を頭頂軸（LotteryBall.fbx のローカル +Z）まわりに 1 周させて GIF を書く。俯瞰角は pose のまま。</summary>
        static void Spin(Camera cam, Transform ball, Quaternion pose, string file)
        {
            var frames = new List<Color32[]>(SpinFrames);
            for (int i = 0; i < SpinFrames; i++)
            {
                ball.rotation = pose * Quaternion.AngleAxis(360f * i / SpinFrames, Vector3.forward);
                frames.Add(FlipRows(Grab(cam, SpinSize), SpinSize));
            }
            var path = Path.Combine(Repo, Dir, file);
            GifWriter.Write(path, frames, SpinSize, SpinSize, SpinDelayCs);
            Debug.Log($"[LotoCapture] {Dir}/{file} ({SpinSize}x{SpinSize}, {SpinFrames} frames, {new FileInfo(path).Length / 1024} KB)");
        }

        /// <summary>画角を Zoom で絞り、size の Super 倍で描いて箱平均で縮めた画素を返す（ReadPixels と同じく下の行から）。</summary>
        static Color32[] Grab(Camera cam, int size)
        {
            int w = size * Super;
            var rt = new RenderTexture(w, w, 24);
            var tex = new Texture2D(w, w, TextureFormat.RGB24, false);
            var prevTarget = cam.targetTexture; var prevActive = RenderTexture.active;
            float fov = cam.fieldOfView;
            try
            {
                cam.fieldOfView = 2f * Mathf.Rad2Deg * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * Zoom);
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, w), 0, 0);
                var src = tex.GetPixels32();
                var dst = new Color32[size * size]; int kk = Super * Super;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int r = 0, g = 0, b = 0;
                        for (int dy = 0; dy < Super; dy++)
                            for (int dx = 0; dx < Super; dx++)
                            { var c = src[(y * Super + dy) * w + x * Super + dx]; r += c.r; g += c.g; b += c.b; }
                        dst[y * size + x] = new Color32((byte)(r / kk), (byte)(g / kk), (byte)(b / kk), 255);
                    }
                return dst;
            }
            finally
            {
                cam.fieldOfView = fov; cam.targetTexture = prevTarget; RenderTexture.active = prevActive;
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            }
        }

        /// <summary>行を上下反転（GIF は上の行から）。</summary>
        static Color32[] FlipRows(Color32[] px, int w)
        {
            int h = px.Length / w; var dst = new Color32[px.Length];
            for (int y = 0; y < h; y++) System.Array.Copy(px, y * w, dst, (h - 1 - y) * w, w);
            return dst;
        }

        static string SlotName(BallSlot s) => s == BallSlot.Drum ? "ロト" : "別ボール";

        /// <summary>README のマーカー間を収録状況の表で置き換える（マーカーが無ければ警告だけ。README の他の行は触らない）。
        /// 表は区分ごとに &lt;details&gt; で畳む（GitHub / VS Code のプレビューで開閉できる）。</summary>
        static void WriteReadmeTable(int done, int total, Dictionary<BallSlot, int> totals, Dictionary<BallSlot, StringBuilder> rows)
        {
            var path = Path.Combine(Repo, Readme);
            var text = File.ReadAllText(path);
            int a = text.IndexOf(Begin), b = text.IndexOf(End);
            if (a < 0 || b < a) { Debug.LogWarning($"[LotoCapture] README に {Begin} / {End} が無いので表は書いていない"); return; }

            var body = new StringBuilder();
            body.AppendLine(Begin);
            body.AppendLine();
            body.AppendLine($"**収録 {done} / {total} 球**（{System.DateTime.Now:yyyy-MM-dd} 時点。`Tools > NTsLoto > Capture Ball Skins` が自動更新）。区分名をクリックで一覧を開閉。");
            body.AppendLine();
            if (done > 0)
            {
                foreach (BallSlot slot in System.Enum.GetValues(typeof(BallSlot)))
                {
                    if (!rows.TryGetValue(slot, out var sb)) continue;
                    int n = sb.ToString().Split('\n').Count(l => l.StartsWith("|"));
                    totals.TryGetValue(slot, out var t);
                    body.AppendLine("<details>");
                    body.AppendLine($"<summary><b>{SlotName(slot)}</b>（{n} / {t} 球）</summary>");
                    body.AppendLine();
                    body.AppendLine("| 正面 | 俯瞰 | 回転 | 番号 | Num_Badge | ファイル |");
                    body.AppendLine("| --- | --- | --- | --- | --- | --- |");
                    body.Append(sb);
                    body.AppendLine();
                    body.AppendLine("</details>");
                    body.AppendLine();
                }
            }
            else { body.AppendLine("> まだ 1 球も貼られていない。"); body.AppendLine(); }
            body.Append(End);

            File.WriteAllText(path, text.Substring(0, a) + body + text.Substring(b + End.Length));
            Debug.Log($"[LotoCapture] README のボールテクスチャ表を更新（{done}/{total}）");
        }
    }
}
