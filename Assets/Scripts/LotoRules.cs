using System;

namespace NTsLotoEngine
{
    /// <summary>
    /// 抽選仕様の正本（SSOT）。**結果は物理が決める**（2026-09-03 User 指示: RNG で先に決めて見せかけるのをやめる）。
    /// ここにあるのは球の範囲・別ボールの並び・目標当選率（形状で近似し、Tools > NTsLoto > Monte Carlo で実測して保証する）。
    /// 仕様: docs/DESIGN.md 2章。
    /// </summary>
    public static class LotoRules
    {
        public const int SingleMin = 2, SingleMax = 11;              // 上段の篩（4 層）: 2〜11 の 10 球から最下層に最初に来た 1 球
        public const int SixMin = 12, SixMax = 99, SixCount = 6;     // 下段の篩（15 層）: 12〜99 の 88 球から最下層に来た順に 6 球

        [Serializable]
        public class Streak
        {
            public int ball;        // ボール番号（NumberBall.number）
            public double targetP;  // 1 段あたりの目標当選率（コレクタの当たり扇形で近似。実測値は MC の JSON が正）
            public int max;         // 段数（最大連続回数。1 = 単発）
            public string variant;  // Assets/Models/Kuruun_Collector_<variant>.fbx（BlenderSources/kuruun_params.json の variants）
            public Streak(int ball, double targetP, int max, string variant) { this.ball = ball; this.targetP = targetP; this.max = max; this.variant = variant; }
        }

        /// <summary>
        /// 別ボール 7 台。並び順 = 進行順。目標値の由来: 1 と 10 は上段・下段の各数字の当選率、0/2 は仕様値、
        /// 33/64/81 は旧仕様の「完走確率 = 1/1024」を 1 段あたりに割った値（2^(-10/11), 2^(-5/3), 2^(-5/2)）を有効数字 2 桁に丸めたもの。
        /// </summary>
        public static readonly Streak[] Streaks =
        {
            new Streak(1,  1.0 / 10, 1,  "p10"),      // 0 と同じコレクタ
            new Streak(10, 6.0 / 88, 1,  "p6of88"),
            new Streak(0,  0.10,     3,  "p10"),
            new Streak(2,  0.50,     10, "p50"),
            new Streak(33, 0.53,     11, "p53"),
            new Streak(64, 0.32,     6,  "p32"),
            new Streak(81, 0.18,     4,  "p18"),
        };
    }

    [Serializable]
    public class StreakResult
    {
        public int ball;
        public int wins;            // 連続当選回数（0 = 初回落選）
        public int max;
        public double targetP;      // 目標値（LotoRules）
        public string nameJP, nameEN;
    }

    [Serializable]
    public class LotoResult
    {
        public int seed;            // Random.InitState に使った値（投入位置のジッタ用。結果は物理なので同じ seed でも一致は保証しない）
        public string drawnAt;
        public int single;          // 2〜11
        public string singleName;
        public int[] six;           // 12〜99 × 6（到達順 = 抽選順）
        public string[] sixNames;
        public int[] sixSorted;
        public StreakResult[] streaks;
    }
}
