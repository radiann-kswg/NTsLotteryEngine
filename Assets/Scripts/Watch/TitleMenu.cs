using UnityEngine;
using UnityEngine.SceneManagement;

namespace NTsLotteryEngine.Watch
{
    /// <summary>タイトル（docs/WATCH.md S1）: Watch / Ball View / Quit。↑↓（スティック）で選び A / Enter で決定。</summary>
    public class TitleMenu : MonoBehaviour
    {
        public const string Title = "Assets/Scenes/TitleScene.unity", Watch = "Assets/Scenes/WatchScene.unity", BallView = "Assets/Scenes/BallViewScene.unity";
        static readonly string[] Items = { "Watch", "Ball View", "Quit" };
        public Font hudFont;
        int cur;
        GUIStyle item, title;

        void Awake() => WatchBoot.Apply();

        void Update()
        {
            cur = (cur - WatchInput.Vertical() + Items.Length) % Items.Length;
            if (WatchInput.Accept)
            {
                if (cur == 0) SceneManager.LoadScene(Watch);
                else if (cur == 1) SceneManager.LoadScene(BallView);
                else Quit();
            }
            if (WatchInput.Back) Quit();
        }

        /// <summary>他シーンからの「戻る」。タイトルがビルドに無ければ終了。</summary>
        public static void Back()
        {
            if (SceneUtility.GetBuildIndexByScenePath(Title) >= 0) SceneManager.LoadScene(Title);
            else Quit();
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnGUI()
        {
            if (item == null)
            {
                var f = hudFont ? hudFont : GUI.skin.label.font;
                title = new GUIStyle(GUI.skin.label) { font = f, fontSize = Screen.height / 12, alignment = TextAnchor.MiddleCenter };
                item = new GUIStyle(GUI.skin.label) { font = f, fontSize = Screen.height / 20, alignment = TextAnchor.MiddleCenter };
            }
            float W = Screen.width, H = Screen.height;
            GUI.Label(new Rect(0, H * 0.18f, W, H * 0.16f), "NTs Lottery Balls", title);
            for (int i = 0; i < Items.Length; i++)
                GUI.Label(new Rect(0, H * (0.45f + 0.1f * i), W, H * 0.1f), (i == cur ? "> " : "") + Items[i] + (i == cur ? " <" : ""), item);
        }
    }
}
