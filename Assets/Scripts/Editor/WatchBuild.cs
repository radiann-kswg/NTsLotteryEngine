using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using NTsLotteryEngine.Watch;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>
    /// 観賞ビルド（docs/WATCH.md）。Title / Watch / BallView の 3 シーン。Linux は Pi 4B（box64）向けに Mono・OpenGLCore 固定。
    /// ビルド前に創作DB の公開レコードだけを StreamingAssets/CreationsDB/ へ書き出す（git 管轄外。CreationsDb.ResolveRoot がそこを先に見る）。
    /// </summary>
    public static class WatchBuild
    {
        public const string Output = "Builds/Watch";

        [MenuItem("Tools/NTsLoto/Build Watch Linux x64 (RPi)")]
        public static void BuildLinux64() => Build(BuildTarget.StandaloneLinux64, $"{Output}/NTsLotoWatch.x86_64");

        [MenuItem("Tools/NTsLoto/Build Watch Windows x64")]
        public static void BuildWin64() => Build(BuildTarget.StandaloneWindows64, $"{Output}Win/NTsLotoWatch.exe");

        static void Build(BuildTarget target, string output)
        {
            ExportCreationsDb();
            WatchSceneBuilder.PiPipeline();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.OpenGLCore });   // V3D + Mesa で動く構成（Vulkan は X11 swapchain で落ちた）
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true; PlayerSettings.resizableWindow = false;

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { TitleMenu.Title, TitleMenu.Watch, TitleMenu.BallView },
                locationPathName = output, target = target, options = BuildOptions.None,
            });
            Debug.Log($"[WatchBuild] {target}: {report.summary.result} → {output} ({report.summary.totalSize / 1024 / 1024} MB)");
        }

        /// <summary>創作DB 3 冊から公開レコード（CreationsDb.ShownProgress）の必要な項目だけを StreamingAssets へ。未公開の名前はビルドに入れない。</summary>
        [MenuItem("Tools/NTsLoto/Export Creations DB (StreamingAssets)")]
        public static void ExportCreationsDb()
        {
            string src = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CreationsDb.SubmoduleRoot, "DataBases"));
            string dst = Path.Combine(Application.streamingAssetsPath, "CreationsDB", "DataBases");
            if (!Directory.Exists(src)) { Debug.LogWarning($"[WatchBuild] 創作DB が無い（scripts/setup-submodule）。名前無しでビルドする: {src}"); return; }
            Directory.CreateDirectory(dst);
            string[] keep = { "Num", "Num_Badge", "Name_JP", "Name_EN", "Progress", "ColorPalette" };
            int n = 0;
            foreach (var db in new[] { "Primary", "SemiPrimary", "SelfSecondary" })
            {
                string file = Path.Combine(src, $"db_{db}.json");
                if (!File.Exists(file)) continue;
                var outArr = new JArray();
                foreach (var t in JArray.Parse(File.ReadAllText(file)))
                {
                    if (t is not JObject o || !CreationsDb.ShownProgress.Contains((string)o["Progress"] ?? "")) continue;
                    var c = new JObject();
                    foreach (var k in keep) if (o[k] != null) c[k] = o[k];
                    outArr.Add(c); n++;
                }
                File.WriteAllText(Path.Combine(dst, $"db_{db}.json"), outArr.ToString(Newtonsoft.Json.Formatting.None));
            }
            AssetDatabase.Refresh();
            Debug.Log($"[WatchBuild] CreationsDB → StreamingAssets: {n} records");
        }
    }
}
