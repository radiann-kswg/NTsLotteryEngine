using System;
using System.Collections.Generic;
using UnityEngine;

namespace NTsLotteryEngine
{
    /// <summary>球の出どころ。同じ番号でもロトマシーンの球と別ボールではキャラが違う（別ボール 2 = バイナ、ロトの 2 = ツグ）。</summary>
    public enum BallSlot { Drum, Streak }

    [Serializable]
    public class BallSkin
    {
        public BallSlot slot;
        public int number;
        [Tooltip("本体テクスチャ（LotteryBallKit の BallUV_Template に合わせた球面 UV）。null なら table.defaultTexture、それも null なら白球")]
        public Texture2D texture;
        [Tooltip("テクスチャが無いときの球の色（テクスチャありでは無視）")]
        public Color tint = Color.white;
        [Tooltip("創作DB: Primary / SemiPrimary / SelfSecondary")]
        public string db = "Primary";
        [Tooltip("創作DB の Num（空なら番号そのまま）。例: 000 / 2-alt / 10-alt / 3x11 / 64-sxp")]
        public string dbNum = "";

        public string DbNum => string.IsNullOrEmpty(dbNum) ? number.ToString() : dbNum;
    }

    /// <summary>
    /// 全球（1〜11・12〜99・別ボール 7）の 1 球ごとのテクスチャと創作DBリンク。
    /// 生成は LotoSceneBuilder（既存行は保持＝Inspector での手貼りが正）。適用は LotoDirector が球を生成した直後。
    /// </summary>
    [CreateAssetMenu(menuName = "NTsLoto/Ball Skin Table")]
    public class BallSkinTable : ScriptableObject
    {
        public Texture2D defaultTexture;
        public List<BallSkin> skins = new List<BallSkin>();

        public BallSkin Find(BallSlot slot, int number) => skins.Find(s => s.slot == slot && s.number == number);

        public void Apply(NumberBall ball, BallSlot slot)
        {
            var s = Find(slot, ball.number);
            var tex = s?.texture ? s.texture : defaultTexture;
            ball.tint = s?.tint ?? Color.white;
            // 順番が大事（2026-09-06 BallSkinViewer で発覚）:
            // 1. SetCharacterTexture が先。内部で Renderer を取り直すので、Awake 前（エディタで生成した直後）でも効く。
            //    NumberBall.Apply() は rend==null だと黙って return するため、先に Apply() を呼ぶと番号デカールが乗らない。
            // 2. そのあと Apply()。SetCharacterTexture は本体（submesh0）しか触らないので、番号デカール（submesh1）は
            //    Apply() でしか更新されない＝生成済みの球に貼り直すと番号が前のまま残る。
            ball.SetCharacterTexture(tex);   // tex==null なら内部で Apply() に落ちて白球へ戻る
            if (tex) ball.Apply();
        }

        /// <summary>球に紐づく創作DBレコード（無ければ null）。</summary>
        public NtCharacter Character(BallSlot slot, int number)
        {
            var s = Find(slot, number);
            return s == null ? null : CreationsDb.Find(s.db, s.DbNum);
        }

        /// <summary>
        /// 別ボールで「番号通り」でないキャラ。ロトマシーンの球と、ここに無い別ボールは Primary の同番号。
        /// 0→000(チトセ) / 10→ディケ / 2→バイナ / 33→トレッド / 64→ゼフィア / 81→9×9(クック)（User 指定 2026-09-03）。
        /// 9x9 は創作DB の Progress が notProceeded なので、公開されるまで CreationsDb は名前を返さない（HUD/JSON は空。テクスチャの割当は先にできる）。
        /// </summary>
        public static readonly Dictionary<int, (string db, string num)> StreakOverrides = new Dictionary<int, (string, string)>
        {
            { 0, ("Primary", "000") },
            { 10, ("Primary", "10-alt") },
            { 2, ("Primary", "2-alt") },
            { 33, ("SemiPrimary", "3x11") },
            { 64, ("SemiPrimary", "64-sxp") },
            { 81, ("SemiPrimary", "9x9") },
        };

        /// <summary>足りない行だけ追加する（冪等。既存行のテクスチャ・DB 指定は触らない）。追加した行数を返す。</summary>
        public int Populate()
        {
            int added = 0;
            void Add(BallSlot slot, int n, string db, string num)
            {
                if (Find(slot, n) != null) return;
                skins.Add(new BallSkin { slot = slot, number = n, db = db, dbNum = num });
                added++;
            }
            for (int n = LotoRules.SingleMin; n <= LotoRules.SixMax; n++) Add(BallSlot.Drum, n, "Primary", "");
            foreach (var s in LotoRules.Streaks)
            {
                var o = StreakOverrides.TryGetValue(s.ball, out var v) ? v : ("Primary", "");
                Add(BallSlot.Streak, s.ball, o.Item1, o.Item2);
            }
            return added;
        }
    }
}
