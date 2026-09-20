using UnityEngine;

namespace NTsLotteryEngine.Watch
{
    /// <summary>
    /// 観賞ビルドの入力（docs/WATCH.md M5）。旧 Input Manager のまま（Input System は入れない）。
    /// パッドは Xbox 配列（Windows XInput / Linux SDL とも A0 B1 X2 Y3 LB4 RB5 Back6 Start7）。RSC の割当に揃える。
    /// Pi では ucon-run-app の SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1 が前提（窓がフォーカスを持たない）。
    /// </summary>
    public static class WatchInput
    {
        static bool K(KeyCode a, KeyCode b) => Input.GetKeyDown(a) || Input.GetKeyDown(b);
        public static bool Prev => K(KeyCode.LeftArrow, KeyCode.JoystickButton4);
        public static bool Next => K(KeyCode.RightArrow, KeyCode.JoystickButton5);
        public static bool Machine => K(KeyCode.C, KeyCode.JoystickButton2);   // X
        public static bool Audio => K(KeyCode.M, KeyCode.JoystickButton3);     // Y
        public static bool Auto => K(KeyCode.Space, KeyCode.JoystickButton0);  // A
        public static bool Accept => K(KeyCode.Return, KeyCode.JoystickButton0);
        public static bool Help => K(KeyCode.H, KeyCode.JoystickButton6);      // Back / Select
        public static bool Back => K(KeyCode.Escape, KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton7);   // B / Start

        static float lastV;
        /// <summary>縦方向の 1 回押し（↑=+1 / ↓=−1 / 0）。スティックは倒した瞬間だけ。</summary>
        public static int Vertical()
        {
            float v = Input.GetAxisRaw("Vertical");
            int r = 0;
            if (v > 0.6f && lastV <= 0.6f) r = 1;
            else if (v < -0.6f && lastV >= -0.6f) r = -1;
            lastV = v;
            return r;
        }
    }
}
