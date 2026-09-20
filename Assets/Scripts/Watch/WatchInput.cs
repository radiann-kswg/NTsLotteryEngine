using UnityEngine;

namespace NTsLotteryEngine.Watch
{
    /// <summary>
    /// 観賞ビルドの入力（docs/WATCH.md M5）。旧 Input Manager のまま（Input System は入れない）。
    /// パッドは Xbox 配列（Windows XInput / Linux SDL とも A0 B1 X2 Y3 LB4 RB5 Back6 Start7）。RSC の割当に揃える。
    /// 十字キーは Linux(SDL) ではハット＝7th/8th axis に来る。既定の Horizontal/Vertical はスティックしか見ないので、
    /// InputManager に足した WatchDPadX/Y も一緒に読む（2026-09-20 Pi 実機: 十字キーだけ無反応だった）。
    /// Pi では ucon-run-app の SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS=1 が前提（窓がフォーカスを持たない）。
    /// </summary>
    public static class WatchInput
    {
        static bool K(KeyCode a, KeyCode b) => Input.GetKeyDown(a) || Input.GetKeyDown(b);
        public static bool Prev => K(KeyCode.LeftArrow, KeyCode.JoystickButton4) || Horizontal() < 0;
        public static bool Next => K(KeyCode.RightArrow, KeyCode.JoystickButton5) || Horizontal() > 0;
        public static bool Machine => K(KeyCode.C, KeyCode.JoystickButton2);   // X
        public static bool Audio => K(KeyCode.M, KeyCode.JoystickButton3);     // Y
        public static bool Auto => K(KeyCode.Space, KeyCode.JoystickButton0);  // A
        public static bool Accept => K(KeyCode.Return, KeyCode.JoystickButton0);
        public static bool Help => K(KeyCode.H, KeyCode.JoystickButton6);      // Back / Select
        public static bool Back => K(KeyCode.Escape, KeyCode.JoystickButton1) || Input.GetKeyDown(KeyCode.JoystickButton7);   // B / Start

        static bool dpadOk = true;
        /// <summary>InputManager に無い軸を読むと毎フレーム例外を投げるので、1 度こけたら諦めてスティックだけで動かす。</summary>
        static float DPad(string axis)
        {
            if (!dpadOk) return 0f;
            try { return Input.GetAxisRaw(axis); }
            catch { dpadOk = false; Debug.LogWarning($"[Watch] 入力軸 {axis} が InputManager に無い。十字キーは効かない"); return 0f; }
        }

        static float Stronger(float a, float b) => Mathf.Abs(a) >= Mathf.Abs(b) ? a : b;

        /// <summary>倒した瞬間だけ 1 回。Prev/Next が同じフレームで 2 回呼ぶので、フレームごとに 1 度だけ判定する。</summary>
        static int Edge(float v, ref float last)
        {
            int r = 0;
            if (v > 0.6f && last <= 0.6f) r = 1;
            else if (v < -0.6f && last >= -0.6f) r = -1;
            last = v;
            return r;
        }

        static int vFrame = -1, vVal, hFrame = -1, hVal;
        static float lastV, lastH;

        /// <summary>縦の 1 回押し（↑=+1 / ↓=−1 / 0）。左スティックと十字キーのどちらでも。</summary>
        public static int Vertical()
        {
            if (vFrame != Time.frameCount)
            {
                vFrame = Time.frameCount;
                vVal = Edge(Stronger(Input.GetAxisRaw("Vertical"), DPad("WatchDPadY")), ref lastV);
            }
            return vVal;
        }

        /// <summary>横の 1 回押し（→=+1 / ←=−1 / 0）。球送り（LB/RB と同じ働き）に使う。</summary>
        static int Horizontal()
        {
            if (hFrame != Time.frameCount)
            {
                hFrame = Time.frameCount;
                hVal = Edge(Stronger(Input.GetAxisRaw("Horizontal"), DPad("WatchDPadX")), ref lastH);
            }
            return hVal;
        }
    }
}
