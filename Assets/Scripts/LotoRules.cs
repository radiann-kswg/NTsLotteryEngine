using System;
using System.Collections.Generic;

namespace NTsLotoEngine
{
    /// <summary>
    /// 抽選仕様の正本（SSOT）。結果はここで確定し、物理シーンはそれを「見せる」だけ。
    /// 仕様: docs/DESIGN.md 2章。
    /// </summary>
    public static class LotoRules
    {
        public const int SingleMin = 1, SingleMax = 11;              // 1〜11 から 1 個
        public const int SixMin = 12, SixMax = 99, SixCount = 6;     // 12〜99 から 6 個（重複なし・排出順を保持）

        /// <summary>
        /// P = 別ボール 1・10・0・2 の 4 台の抽選結果のうち最小の確率。
        /// 1: 1/11, 10: 6/88, 0: 3連勝 0.1^3, 2: 10連勝 0.5^10 → 最小は 1/1024。
        /// </summary>
        public static readonly double P = Math.Min(
            Math.Min(1.0 / 11, 6.0 / 88),
            Math.Min(Math.Pow(0.1, 3), Math.Pow(0.5, 10)));

        [Serializable]
        public class Streak
        {
            public int ball;      // ボール番号（NumberBall.number）
            public double p;      // 1 回あたりの当選率
            public int max;       // 最大連続回数（1 = 単発）
            public Streak(int ball, double p, int max) { this.ball = ball; this.p = p; this.max = max; }
        }

        /// <summary>別ボール 7 台。並び順 = 進行順。</summary>
        public static readonly Streak[] Streaks =
        {
            new Streak(1,  1.0 / 11, 1),                 // 1〜11 抽選機の各数字と同確率
            new Streak(10, 6.0 / 88, 1),                 // 12〜99 抽選機の各数字と同確率
            new Streak(0,  0.1, 3),
            new Streak(2,  0.5, 10),
            new Streak(33, Math.Pow(P, 1.0 / 11), 11),   // 11 連勝の確率 = P
            new Streak(64, Math.Pow(P, 1.0 / 6), 6),     // 6 連勝の確率 = P
            new Streak(81, Math.Pow(P, 1.0 / 4), 4),     // 4 連勝の確率 = P
        };

        public static LotoResult Draw(int seed)
        {
            var rng = new Random(seed);
            var r = new LotoResult { seed = seed, P = P, drawnAt = DateTime.Now.ToString("o") };

            r.single = rng.Next(SingleMin, SingleMax + 1);

            // 12〜99 から 6 個: 部分 Fisher–Yates（排出順がそのまま抽選順）
            var pool = new List<int>();
            for (int n = SixMin; n <= SixMax; n++) pool.Add(n);
            r.six = new int[SixCount];
            for (int i = 0; i < SixCount; i++)
            {
                int j = rng.Next(i, pool.Count);
                (pool[i], pool[j]) = (pool[j], pool[i]);
                r.six[i] = pool[i];
            }
            r.sixSorted = (int[])r.six.Clone();
            Array.Sort(r.sixSorted);

            // 別ボール: どこかで落選したら即清算 → 連続当選回数が結果
            r.streaks = new StreakResult[Streaks.Length];
            for (int i = 0; i < Streaks.Length; i++)
            {
                var s = Streaks[i];
                int wins = 0;
                while (wins < s.max && rng.NextDouble() < s.p) wins++;
                r.streaks[i] = new StreakResult { ball = s.ball, wins = wins, max = s.max, p = s.p };
            }
            return r;
        }
    }

    [Serializable]
    public class StreakResult
    {
        public int ball;
        public int wins;   // 連続当選回数（0 = 初回落選）
        public int max;
        public double p;
    }

    [Serializable]
    public class LotoResult
    {
        public int seed;
        public string drawnAt;
        public int single;          // 1〜11
        public int[] six;           // 12〜99 × 6（抽選順）
        public int[] sixSorted;
        public StreakResult[] streaks;
        public double P;
    }
}
