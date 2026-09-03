using UnityEditor;

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
}
