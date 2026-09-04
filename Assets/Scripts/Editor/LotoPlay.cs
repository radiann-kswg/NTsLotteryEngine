using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NTsLotoEngine.EditorTools
{
    /// <summary>MCP からプレイモードを出し入れするためのメニュー（公式 MCP に Play が無いので）。</summary>
    public static class LotoPlay
    {
        [MenuItem("Tools/NTsLoto/Play")]
        public static void Play() => EditorApplication.isPlaying = true;

        [MenuItem("Tools/NTsLoto/Stop")]
        public static void Stop() => EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Play を N 回繰り返し、毎回の結果 JSON を退避する（実機の当落統計を貯める用。`LotoDirector.skipSieves` を立てれば塔だけ）。
    /// 終了検知は `LotoDirector.outPath`（既定 result.json）の更新。ドメインリロードを跨ぐので状態は EditorPrefs に置く。
    /// 使い方（RunCommand から）: `LotoPlayLoop.Start(20, "Output/verify")` / 止めるのは `LotoPlayLoop.Cancel()`。
    /// </summary>
    [InitializeOnLoad]
    public static class LotoPlayLoop
    {
        const string KeyLeft = "NTsLoto.PlayLoop.left", KeyDir = "NTsLoto.PlayLoop.dir";
        const string KeyStamp = "NTsLoto.PlayLoop.stamp", KeySrc = "NTsLoto.PlayLoop.src";

        static LotoPlayLoop() { EditorApplication.update += Update; }

        public static void Start(int runs, string dir = "Output/verify", string src = "result.json")
        {
            Directory.CreateDirectory(dir);
            EditorPrefs.SetInt(KeyLeft, runs); EditorPrefs.SetString(KeyDir, dir); EditorPrefs.SetString(KeySrc, src);
            Next();
        }

        [MenuItem("Tools/NTsLoto/Play Loop/Cancel")]
        public static void Cancel() { EditorPrefs.SetInt(KeyLeft, 0); Debug.Log("[PlayLoop] cancelled"); }

        static void Next()
        {
            int left = EditorPrefs.GetInt(KeyLeft, 0);
            if (left <= 0) { Debug.Log("[PlayLoop] done"); return; }
            var src = EditorPrefs.GetString(KeySrc, "result.json");
            EditorPrefs.SetString(KeyStamp, (File.Exists(src) ? File.GetLastWriteTimeUtc(src) : DateTime.MinValue).Ticks.ToString());
            Debug.Log($"[PlayLoop] run ({left} left)");
            EditorApplication.isPlaying = true;
        }

        static void Update()
        {
            if (EditorPrefs.GetInt(KeyLeft, 0) <= 0) return;
            var src = EditorPrefs.GetString(KeySrc, "result.json");
            if (EditorApplication.isPlaying)
            {
                // 結果 JSON が書かれたら 1 回ぶん終了
                if (!File.Exists(src)) return;
                if (File.GetLastWriteTimeUtc(src).Ticks <= long.Parse(EditorPrefs.GetString(KeyStamp, "0"))) return;
                EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // 遷移中
            if (File.Exists(src) && File.GetLastWriteTimeUtc(src).Ticks > long.Parse(EditorPrefs.GetString(KeyStamp, "0")))
            {
                var dir = EditorPrefs.GetString(KeyDir, "Output/verify");
                Directory.CreateDirectory(dir);
                File.Copy(src, Path.Combine(dir, $"run_{DateTime.Now:yyyyMMdd-HHmmss}.json"), true);
                EditorPrefs.SetInt(KeyLeft, EditorPrefs.GetInt(KeyLeft, 0) - 1);
                Next();
            }
        }
    }
}
