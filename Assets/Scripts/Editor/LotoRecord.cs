using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>
    /// Tools > NTsLoto > Play + Record … Unity Recorder で Game View を MP4 に録りながら Play する（演出の観察・調整用）。
    /// 出力は Recordings/&lt;yyyyMMdd-HHmmss&gt;.mp4（git 管轄外）。Stop（または quitWhenDone）で自動的に閉じる。
    /// RSC の運用と同じで、User が手動で Recorder を回している最中は使わない（Time.captureFramerate を取り合う）。
    /// 静止画の切り出しはサンドボックス側の ffmpeg で行う（`ffmpeg -i x.mp4 -vf fps=1 f_%04d.png`）。
    /// </summary>
    [InitializeOnLoad]
    public static class LotoRecord
    {
        const string Key = "NTsLoto.RecordOnPlay";
        public static int Width = 960, Height = 540, Fps = 30;

        static RecorderController controller;

        static LotoRecord() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("Tools/NTsLoto/Play + Record")]
        public static void PlayAndRecord()
        {
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false);
                Start();
            }
            else if (s == PlayModeStateChange.ExitingPlayMode && controller != null && controller.IsRecording())
            {
                controller.StopRecording();
                controller = null;
            }
        }

        static void Start()
        {
            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRate = Fps;
            settings.CapFrameRate = true;   // 実時間に関係なく 1/Fps 刻みで進める（物理が安定・録画が落ちない）

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "LotoMovie";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings { Codec = CoreEncoderSettings.OutputCodec.MP4, EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Medium };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = Width, OutputHeight = Height };
            movie.OutputFile = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings")), DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            settings.AddRecorderSettings(movie);

            controller = new RecorderController(settings);
            controller.PrepareRecording();
            if (controller.StartRecording()) Debug.Log($"[LotoRecord] recording → {movie.OutputFile}.mp4 ({Width}x{Height}@{Fps})");
            else Debug.LogError("[LotoRecord] StartRecording failed");
        }
    }
}
