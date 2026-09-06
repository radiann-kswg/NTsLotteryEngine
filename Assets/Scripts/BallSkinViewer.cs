using UnityEngine;

namespace NTsLotteryEngine
{
    /// <summary>
    /// ボールテクスチャの確認用ビューア。球（NumberBall）と同じ GameObject に付ける。
    /// シーンは Tools &gt; NTsLoto &gt; Build Ball View Scene で生成（1 球だけ）。
    /// pose は生のクォータニオン（Inspector に x/y/z/w がそのまま出る）。Play 中は spinAxis まわりに
    /// spinDegPerSec で積分するので、pose が「いまの姿勢」を映す＝そのまま読み取り値になる。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(NumberBall))]
    public class BallSkinViewer : MonoBehaviour
    {
        [Tooltip("Assets/Data/BallSkins.asset。null なら白球＋番号デカールだけ")]
        public BallSkinTable skins;
        [Tooltip("Drum = ロトマシーンの球 / Streak = 別ボール（同じ番号でも別キャラ）")]
        public BallSlot slot = BallSlot.Drum;
        [Range(0, 99)] public int number = 6;

        [Tooltip("姿勢（生のクォータニオン。長さ 0 は identity 扱い・毎フレーム正規化）")]
        public Quaternion pose = Quaternion.identity;
        [Tooltip("角速度の軸（ワールド。正規化して使う）")]
        public Vector3 spinAxis = Vector3.up;
        [Tooltip("角速度 [deg/s]。0 で静止")]
        public float spinDegPerSec = 45f;

        NumberBall ball;
        int appliedNumber = -1;
        BallSlot appliedSlot;
        BallSkinTable appliedSkins;

        void OnEnable() { ApplySkin(); ApplyPose(); }
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

        void OnGUI()
        {
            if (!Application.isPlaying) return;
            var skin = skins ? skins.Find(slot, number) : null;
            string tex = skin != null && skin.texture ? skin.texture.name
                : skins && skins.defaultTexture ? skins.defaultTexture.name + "（defaultTexture）" : "なし（白球）";
            var ch = skins ? skins.Character(slot, number) : null;
            GUI.Label(new Rect(12, 12, 640, 90),
                $"{slot} #{number}   tex: {tex}\n" +
                $"{(ch != null ? $"{ch.nameJP} / {ch.shortEN}  (Num_Badge: {ch.badge})" : "創作DB 未取得")}\n" +
                $"q = ({pose.x:F3}, {pose.y:F3}, {pose.z:F3}, {pose.w:F3})   ω = {spinDegPerSec:F1} deg/s around {spinAxis}");
        }
    }
}
