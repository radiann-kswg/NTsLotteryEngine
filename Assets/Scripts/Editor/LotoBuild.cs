using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace NTsLotoEngine.EditorTools
{
    /// <summary>
    /// ビルド。Linux x64 は Raspberry Pi 4B（box64）向けに Mono バックエンド固定（NTsWallpaperEngine と同じ方針）。
    /// CLI: Unity.exe -batchmode -quit -projectPath . -executeMethod NTsLotoEngine.EditorTools.LotoBuild.BuildLinux64
    /// </summary>
    public static class LotoBuild
    {
        const string ScenePath = "Assets/Scenes/LotoScene.unity";

        [MenuItem("Tools/NTsLoto/Build Linux x64 (RPi)")]
        public static void BuildLinux64() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/NTsLotoEngine.x86_64");

        [MenuItem("Tools/NTsLoto/Build Windows x64")]
        public static void BuildWin64() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/NTsLotoEngine.exe");

        static void Build(BuildTarget target, string output)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = false;

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            });
            Debug.Log($"[LotoBuild] {target}: {report.summary.result} → {output} ({report.summary.totalSize / 1024 / 1024} MB)");
        }
    }
}
