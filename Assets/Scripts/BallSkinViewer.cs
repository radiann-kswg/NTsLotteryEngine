using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace NTsLotteryEngine
{
    /// <summary>
    /// ボールテクスチャの確認用ビューア。球（NumberBall）と同じ GameObject に付ける。
    /// シーンは Tools &gt; NTsLoto &gt; Build Ball View Scene で生成（1 球だけ）。
    /// pose は生のクォータニオン（Inspector に x/y/z/w がそのまま出る）。Play 中は spinAxis まわりに
    /// spinDegPerSec で積分するので、pose が「いまの姿勢」を映す＝そのまま読み取り値になる。
    /// Play 中の IMGUI: 左＝ボール一覧（◀ ▶）、右上＝姿勢/ω フォーム（xyzw ⇄ Euler ZXY、Apply / Enter で置換）、右下＝正規化済み読み取り値。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(NumberBall))]
    public class BallSkinViewer : MonoBehaviour
    {
        /// <summary>
        /// 初期姿勢＝キャラ正面・俯瞰 20°・水平正対（実測 2026-09-06）。
        /// LotteryBall.fbx は identity で顔が −Y 極、耳（頭頂）が +Z を向く。Euler(-90,180,0) で顔がカメラ（−Z）に正対、
        /// X を +20° 戻して上から見下ろす。
        /// </summary>
        public static Quaternion DefaultPose => Quaternion.Euler(-70f, 180f, 0f);

        [Tooltip("Assets/Data/BallSkins.asset。null なら白球＋番号デカールだけ")]
        public BallSkinTable skins;
        [Tooltip("Drum = ロトマシーンの球 / Streak = 別ボール（同じ番号でも別キャラ）")]
        public BallSlot slot = BallSlot.Drum;
        [Range(0, 99)] public int number = 6;

        [Tooltip("姿勢（生のクォータニオン。長さ 0 は identity 扱い・毎フレーム正規化）")]
        public Quaternion pose = Quaternion.Euler(-70f, 180f, 0f);
        [Tooltip("角速度の軸（ワールド。正規化して使う）")]
        public Vector3 spinAxis = Vector3.up;
        [Tooltip("角速度 [deg/s]。0 で静止")]
        public float spinDegPerSec = 0f;

        [Tooltip("一覧に全 106 行を出す（エディタ／Debug ビルドでは自動で有効＝一覧に All/Skinned トグルが出る）")]
        public bool showAllBalls;
        [Tooltip("HUD フォント（PenchantManufacture。CJK 未収録なので英名表示）。null なら IMGUI 既定")]
        public Font hudFont;

        NumberBall ball;
        int appliedNumber = -1;
        BallSlot appliedSlot;
        BallSkinTable appliedSkins;

        void OnEnable()
        {
            ApplySkin(); ApplyPose();
            if (Application.isPlaying) { RebuildRows(); FillPoseBuf(); FillOmegaBuf(); }
        }
        void OnValidate() { ApplySkin(); ApplyPose(); }

        void Update()
        {
            // Inspector 以外（スクリプト・MCP）からフィールドを書き換えると OnValidate が飛ばないので毎フレーム見張る
            if (number != appliedNumber || slot != appliedSlot || skins != appliedSkins) ApplySkin();
            if (Application.isPlaying && spinDegPerSec != 0f && spinAxis.sqrMagnitude > 1e-6f)
                pose = Quaternion.AngleAxis(spinDegPerSec * Time.deltaTime, spinAxis.normalized) * pose;
            ApplyPose();
        }

        /// <summary>テーブルの行を球へ貼り直す。Inspector 経由なら OnValidate が呼ぶが、スクリプトで
        /// フィールドを差し替えたとき（シーン生成時）は自分で呼ぶ。</summary>
        public void ApplySkin()
        {
            if (!ball) ball = GetComponent<NumberBall>();
            if (!ball) return;
            ball.number = number;                       // 番号デカール（submesh1）
            if (skins) skins.Apply(ball, slot);         // 本体テクスチャ（submesh0）＝ BallSkins.asset の行
            else ball.Apply();
            appliedNumber = number; appliedSlot = slot; appliedSkins = skins;
        }

        void ApplyPose()
        {
            float len2 = pose.x * pose.x + pose.y * pose.y + pose.z * pose.z + pose.w * pose.w;
            if (len2 < 1e-6f) pose = Quaternion.identity;   // Inspector で全部 0 にされた事故を吸収
            else if (Mathf.Abs(len2 - 1f) > 1e-6f) pose = pose.normalized;
            transform.rotation = pose;
        }

        // ================= IMGUI =================
        bool ShowAll => showAllBalls || Debug.isDebugBuild;   // エディタ内は isDebugBuild が常に true

        readonly List<BallSkin> rows = new List<BallSkin>();
        readonly List<string> rowLabels = new List<string>();
        bool listAll;          // ShowAll のときだけ出るトグル。既定＝貼り済みだけ
        bool eulerMode;
        Vector2 listScroll;
        readonly string[] poseBuf = new string[4];    // 入力バッファ（正規化しない）。xyzw か Euler(x,y,z)
        readonly string[] omegaBuf = new string[4];   // axis x,y,z + deg/s
        GUIStyle label, button, field, box;
        int styleFontSize;

        void RebuildRows()
        {
            rows.Clear(); rowLabels.Clear();
            if (!skins) return;
            foreach (var s in skins.skins)
            {
                if (!listAll && !s.texture) continue;
                rows.Add(s);
                var ch = CreationsDb.Find(s.db, s.DbNum);
                rowLabels.Add($"{s.slot} #{s.number}  {ch?.shortEN ?? ""}");
            }
        }

        void FillPoseBuf()
        {
            if (eulerMode)
            {
                var e = pose.eulerAngles;
                poseBuf[0] = F(e.x); poseBuf[1] = F(e.y); poseBuf[2] = F(e.z); poseBuf[3] = "";
            }
            else { poseBuf[0] = F(pose.x); poseBuf[1] = F(pose.y); poseBuf[2] = F(pose.z); poseBuf[3] = F(pose.w); }
        }
        void FillOmegaBuf() { omegaBuf[0] = F(spinAxis.x); omegaBuf[1] = F(spinAxis.y); omegaBuf[2] = F(spinAxis.z); omegaBuf[3] = F(spinDegPerSec); }
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static bool P(string s, out float v) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        void ApplyPoseForm()
        {
            if (!P(poseBuf[0], out var a) || !P(poseBuf[1], out var b) || !P(poseBuf[2], out var c)) return;   // 数字でなければ何もしない
            if (eulerMode) pose = Quaternion.Euler(a, b, c);
            else
            {
                if (!P(poseBuf[3], out var d)) return;
                pose = new Quaternion(a, b, c, d);   // ApplyPose が正規化する（0 なら identity）
            }
        }
        void ApplyOmegaForm()
        {
            if (!P(omegaBuf[0], out var x) || !P(omegaBuf[1], out var y) || !P(omegaBuf[2], out var z) || !P(omegaBuf[3], out var w)) return;
            spinAxis = new Vector3(x, y, z); spinDegPerSec = w;
        }

        void Select(int idx)
        {
            if (rows.Count == 0) return;
            var s = rows[(idx % rows.Count + rows.Count) % rows.Count];
            slot = s.slot; number = s.number; ApplySkin();
        }

        void Styles()
        {
            int fs = Mathf.Max(10, Mathf.RoundToInt(Screen.height / 42f));
            if (label != null && styleFontSize == fs) return;
            styleFontSize = fs;
            var font = hudFont ? hudFont : GUI.skin.label.font;
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = fs, richText = true };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = fs, alignment = TextAnchor.MiddleLeft };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = fs };
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
        }

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            Styles();
            var ev = Event.current;
            bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            string focus = GUI.GetNameOfFocusedControl();
            float pad = Screen.height * 0.02f, W = Screen.width, H = Screen.height;

            // ---- 左: ボール一覧 ----
            GUILayout.BeginArea(new Rect(pad, pad, W * 0.2f, H - pad * 2), box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Balls ({rows.Count})", label);
            if (ShowAll)
            {
                bool all = GUILayout.Toggle(listAll, listAll ? "All" : "Skinned", button, GUILayout.Width(styleFontSize * 5));
                if (all != listAll) { listAll = all; RebuildRows(); }
            }
            GUILayout.EndHorizontal();
            int cur = rows.FindIndex(r => r.slot == slot && r.number == number);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", button)) Select(cur - 1);
            if (GUILayout.Button(">", button)) Select(cur + 1);
            GUILayout.EndHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll);
            for (int i = 0; i < rows.Count; i++)
                if (GUILayout.Toggle(i == cur, rowLabels[i], button) && i != cur) Select(i);
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // ---- 右上: 姿勢 / ω フォーム ----
            float rx = W * 0.72f, rw = W * 0.28f - pad;
            GUILayout.BeginArea(new Rect(rx, pad, rw, H * 0.5f), box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pose", label);
            bool em = GUILayout.Toggle(eulerMode, eulerMode ? "Euler ZXY (deg)" : "xyzw", button);
            if (em != eulerMode) { eulerMode = em; FillPoseBuf(); }   // 表示モード＝入力モード。切替時は現在姿勢で埋め直す
            GUILayout.EndHorizontal();
            Fields("pose", poseBuf, eulerMode ? new[] { "x", "y", "z" } : new[] { "x", "y", "z", "w" });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", button) || (enter && focus.StartsWith("pose"))) ApplyPoseForm();
            if (GUILayout.Button("Fill", button)) FillPoseBuf();    // 今の姿勢を欄へ
            if (GUILayout.Button("Reset", button)) { pose = DefaultPose; spinDegPerSec = 0f; FillPoseBuf(); FillOmegaBuf(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(styleFontSize);
            GUILayout.Label("Omega  (axis xyz, deg/s)", label);
            Fields("omega", omegaBuf, new[] { "x", "y", "z", "deg/s" });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", button) || (enter && focus.StartsWith("omega"))) ApplyOmegaForm();
            if (GUILayout.Button("Stop", button)) { spinDegPerSec = 0f; FillOmegaBuf(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // ---- 右下: 読み取り値（正規化済み・live）----
            GUILayout.BeginArea(new Rect(rx, pad + H * 0.5f + pad, rw, H * 0.5f - pad * 3), box);
            var e2 = pose.eulerAngles;
            var skin = skins ? skins.Find(slot, number) : null;
            string tex = skin != null && skin.texture ? skin.texture.name
                : skins && skins.defaultTexture ? skins.defaultTexture.name + " (default)" : "(none)";
            var ch = skins ? skins.Character(slot, number) : null;
            GUILayout.Label(
                $"{slot} #{number}  {ch?.shortEN ?? ""}\n" +
                $"badge {ch?.badge ?? skin?.DbNum ?? number.ToString()}   tex {tex}\n\n" +
                $"q  ({F(pose.x)}, {F(pose.y)}, {F(pose.z)}, {F(pose.w)})\n" +
                $"euler ZXY  ({F(e2.x)}, {F(e2.y)}, {F(e2.z)})\n" +
                $"omega  {F(spinDegPerSec)} deg/s  axis ({F(spinAxis.x)}, {F(spinAxis.y)}, {F(spinAxis.z)})", label);
            GUILayout.EndArea();
        }

        void Fields(string prefix, string[] buf, string[] names)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < names.Length; i++)
            {
                GUILayout.Label(names[i], label, GUILayout.Width(styleFontSize * (names[i].Length > 1 ? 3f : 1f)));
                GUI.SetNextControlName(prefix + i);
                buf[i] = GUILayout.TextField(buf[i] ?? "", field);
            }
            GUILayout.EndHorizontal();
        }
    }
}
