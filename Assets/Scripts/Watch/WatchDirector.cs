using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NTsLotteryEngine.Watch
{
    /// <summary>
    /// 観賞シーンの進行役（docs/WATCH.md）。動く球は常に 1 個。塔かガムボール機で転がし、追従カメラで見る。
    /// 落ちきったら同じ球を再投入、自動送りなら次のスキンへ。シーンは Tools > NTsLoto > Build Watch Scene が作る。
    /// </summary>
    public class WatchDirector : MonoBehaviour
    {
        public enum Machine { Tower, Coaster }

        [Header("References (built by WatchSceneBuilder)")]
        public BallSkinTable skins;
        public NumberBall ballPrefab;
        public KuruunTower tower;
        public WatchCoaster coaster;
        public Camera cam;
        public Font hudFont;

        [Header("Camera")]
        public float towerDist = 1.9f, towerUp = 0.55f, coasterDist = 1.05f, coasterUp = 0.30f, fov = 38f, camLerp = 6f;

        public static Machine Current { get => (Machine)PlayerPrefs.GetInt("Watch.Machine", 1); set => PlayerPrefs.SetInt("Watch.Machine", (int)value); }
        public static bool Auto { get => PlayerPrefs.GetInt("Watch.Auto", 1) != 0; set => PlayerPrefs.SetInt("Watch.Auto", value ? 1 : 0); }

        readonly List<BallSkin> rows = new List<BallSkin>();
        int idx;
        NumberBall ball; Rigidbody rb;
        Coroutine loop;
        bool help;
        Vector3 camDir = Vector3.back;
        GUIStyle label; int fontSize;
        // FPS ログ（M3）: 10 秒ごとに avg / min
        int frames; float span, maxDt;

        void Awake() => WatchBoot.Apply();

        void Start()
        {
            foreach (var s in skins.skins) if (s.texture) rows.Add(s);   // 貼り済みだけ
            if (rows.Count == 0) { Debug.LogError("[Watch] BallSkins.asset にテクスチャ付きの球が無い"); enabled = false; return; }
            coaster.Fill(rows);
            ball = Instantiate(ballPrefab); ball.name = "Ball";
            rb = BallUtil.Prepare(ball);
            ball.gameObject.AddComponent<WatchAudio>();
            if (!cam.GetComponent<AudioListener>()) cam.gameObject.AddComponent<AudioListener>();
            cam.fieldOfView = fov;
            Restart();
        }

        void Restart()
        {
            if (loop != null) StopCoroutine(loop);
            tower.gameObject.SetActive(Current == Machine.Tower);
            coaster.gameObject.SetActive(Current == Machine.Coaster);
            loop = StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            while (true)
            {
                var s = rows[idx];
                ball.number = s.number; skins.Apply(ball, s.slot);
                rb.isKinematic = false; rb.detectCollisions = true; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // 搬送中に切り替えられたときの後始末
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                if (Current == Machine.Tower) yield return tower.Run(ball, tower.levels.Length, null, null);   // cam=null: 追従はこちらでやる
                else yield return coaster.Run(ball);
                if (Auto) idx = (idx + 1) % rows.Count;
            }
        }

        void Update()
        {
            if (rows.Count == 0) return;
            if (WatchInput.Prev) { idx = (idx + rows.Count - 1) % rows.Count; Restart(); }
            if (WatchInput.Next) { idx = (idx + 1) % rows.Count; Restart(); }
            if (WatchInput.Machine) { Current = Current == Machine.Tower ? Machine.Coaster : Machine.Tower; Restart(); }
            if (WatchInput.Auto) Auto = !Auto;
            if (WatchInput.Audio) WatchAudio.Muted = !WatchAudio.Muted;
            if (WatchInput.Help) help = !help;
            if (WatchInput.Back) TitleMenu.Back();

            frames++; span += Time.unscaledDeltaTime; maxDt = Mathf.Max(maxDt, Time.unscaledDeltaTime);
            if (span >= 10f) { Debug.Log($"[Watch] fps avg {frames / span:F1} min {1f / maxDt:F1} ({Current}, ball {rows[idx].number})"); frames = 0; span = 0; maxDt = 0; }
        }

        void LateUpdate()
        {
            if (!ball) return;
            bool t = Current == Machine.Tower;
            if (!t && coaster.Dispensing)
            {
                var look = coaster.transform.TransformPoint(new Vector3(0, 1.48f, 0));
                var eye = coaster.transform.TransformPoint(new Vector3(2.9f, 2.6f, -4.8f));
                cam.transform.position = Vector3.Lerp(cam.transform.position, eye, 1f - Mathf.Exp(-camLerp * Time.deltaTime));
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), 1f - Mathf.Exp(-camLerp * Time.deltaTime));
                return;
            }
            var center = (t ? tower.transform : coaster.transform).position;
            var p = rb.position;
            var d = new Vector3(p.x - center.x, 0, p.z - center.z);
            if (d.sqrMagnitude > 1e-4f) camDir = d.normalized;
            var target = new Vector3(center.x, p.y, center.z) + camDir * (t ? towerDist : coasterDist) + Vector3.up * (t ? towerUp : coasterUp);
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-camLerp * Time.deltaTime));
            cam.transform.LookAt(p);
        }

        string Label(BallSkin s)
        {
            var name = skins.Character(s.slot, s.number)?.nameEN;   // 公開済みだけ。"93(Nintris)" / "Binor"（NTsSphereChaser と同じ表示）
            return "Ball " + (string.IsNullOrEmpty(name) ? s.number.ToString() : name);
        }

        void OnGUI()
        {
            if (rows.Count == 0) return;
            int fs = Mathf.Max(10, Screen.height / 36);
            if (label == null || fontSize != fs) { fontSize = fs; label = new GUIStyle(GUI.skin.label) { font = hudFont ? hudFont : GUI.skin.label.font, fontSize = fs, richText = true }; }
            float pad = Screen.height * 0.02f;
            string text = $"<b>{Label(rows[idx])}</b>  ({idx + 1}/{rows.Count})\n{(Current == Machine.Tower ? "Tower" : "Gumball")}   auto {(Auto ? "on" : "off")}   sfx {(WatchAudio.Muted ? "off" : "on")}";
            if (help) text += "\n\nLB/RB  < >   ball\nX  C   tower / gumball\nA  Space   auto advance\nY  M   sound on/off\nSelect  H   this help\nStart/B  Esc   title";
            var size = label.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(pad, pad, size.x + pad * 2, size.y + pad * 2), GUIContent.none);
            GUI.Label(new Rect(pad * 2, pad * 2, size.x, size.y), text, label);
        }
    }
}
