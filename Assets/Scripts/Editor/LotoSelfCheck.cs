using System;
using UnityEditor;
using UnityEngine;

namespace NTsLotoEngine.EditorTools
{
    /// <summary>LotoRules の自己検査（モンテカルロ）。仕様表の確率と実測が合わなければエラーを出す。</summary>
    public static class LotoSelfCheck
    {
        [MenuItem("Tools/NTsLoto/Self Check (Monte Carlo)")]
        public static void Run()
        {
            const int N = 200_000;
            var single = new int[LotoRules.SingleMax + 1];
            var six = new int[LotoRules.SixMax + 1];
            var full = new int[LotoRules.Streaks.Length];   // 上限まで連勝した回数
            var any = new int[LotoRules.Streaks.Length];    // 初回当選した回数
            for (int seed = 0; seed < N; seed++)
            {
                var r = LotoRules.Draw(seed);
                single[r.single]++;
                foreach (int n in r.six) six[n]++;
                for (int i = 0; i < r.streaks.Length; i++) { if (r.streaks[i].wins >= 1) any[i]++; if (r.streaks[i].wins == r.streaks[i].max) full[i]++; }
            }

            bool ok = true;
            void Check(string label, double got, double want, double tol)
            {
                bool pass = Math.Abs(got - want) <= tol; ok &= pass;
                Debug.Log($"{(pass ? "OK " : "NG ")} {label}: got {got:G5} want {want:G5}");
            }

            Check("P", LotoRules.P, 1.0 / 1024, 1e-12);
            for (int n = LotoRules.SingleMin; n <= LotoRules.SingleMax; n++) Check($"single {n}", (double)single[n] / N, 1.0 / 11, 0.005);
            double sixMin = 1, sixMax = 0;
            for (int n = LotoRules.SixMin; n <= LotoRules.SixMax; n++) { double f = (double)six[n] / N; sixMin = Math.Min(sixMin, f); sixMax = Math.Max(sixMax, f); }
            Check("six per-number min", sixMin, 6.0 / 88, 0.006);
            Check("six per-number max", sixMax, 6.0 / 88, 0.006);
            for (int i = 0; i < LotoRules.Streaks.Length; i++)
            {
                var s = LotoRules.Streaks[i];
                Check($"ball {s.ball} first win", (double)any[i] / N, s.p, 0.006);
                double pf = Math.Pow(s.p, s.max);
                Check($"ball {s.ball} full streak ({s.max})", (double)full[i] / N, pf, Math.Max(0.006, 4 * Math.Sqrt(pf * (1 - pf) / N)));
                if (s.ball == 33 || s.ball == 64 || s.ball == 81) Check($"ball {s.ball} p^max == P", pf, LotoRules.P, 1e-9);
            }
            if (ok) Debug.Log("[LotoSelfCheck] ALL OK"); else Debug.LogError("[LotoSelfCheck] FAILED — LotoRules を確認");
        }
    }
}
