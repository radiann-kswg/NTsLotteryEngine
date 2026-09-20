using UnityEngine;
using UnityEngine.Rendering;

namespace NTsLotteryEngine.Watch
{
    /// <summary>観賞ビルド共通の起動設定（docs/WATCH.md M3）。Title / Watch / BallView の Awake から呼ぶ。LotoScene には効かせない。</summary>
    public static class WatchBoot
    {
        static bool applied;
        public static void Apply()
        {
            if (applied) return;
            applied = true;
            Application.targetFrameRate = 30;   // Pi 4B の上限。vSync 0 と組で物理の歩幅を安定させる
            QualitySettings.vSyncCount = 0;
            Cursor.visible = false;
            if (Application.isEditor) return;   // エディタは PC 設定のまま（LotoScene 作業を邪魔しない）
            var pi = Resources.Load<RenderPipelineAsset>("Pi_RPAsset");   // 影・HDR・MSAA・深度/不透明テクスチャなし（WatchSceneBuilder が作る）
            if (pi) QualitySettings.renderPipeline = pi;
        }
    }
}
