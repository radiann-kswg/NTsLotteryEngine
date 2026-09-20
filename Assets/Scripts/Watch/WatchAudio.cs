using UnityEngine;

namespace NTsLotteryEngine.Watch
{
    /// <summary>
    /// 球 1 個の物理音（RSC の BallAudio / ParkAudio の移植。音源 Resources/SFX は RSC CC BY 4.0・LICENSE.md）。
    /// 衝突＝相対速度がしきい値を超えたときだけ「コトッ」、転がり＝同じ音をごく小さく距離ごとに刻む。ノブの既定値は RSC で User が耳で決めた値。
    /// </summary>
    public class WatchAudio : MonoBehaviour
    {
        public const float Master = 0.65f, LiftVolume = 0.10f;
        public const float RollVolume = 0.035f, RollFullSpeed = 2.0f, RollMinSpeed = 0.25f, RollTickDistance = 0.30f;
        public const float HitVolume = 0.35f, HitMinSpeed = 0.35f, HitFullSpeed = 4.0f;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("Watch.Mute", 0) != 0;
            set { PlayerPrefs.SetInt("Watch.Mute", value ? 1 : 0); AudioListener.volume = value ? 0f : Master; }
        }

        AudioSource src; AudioClip[] hits; Rigidbody rb;
        float travelled, lastTouch = -1f, lastHit = -1f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            hits = Resources.LoadAll<AudioClip>("SFX/Hits");
            src = Make3D(gameObject, null, false);
            AudioListener.volume = Muted ? 0f : Master;
        }

        public static AudioSource Make3D(GameObject go, AudioClip clip, bool loop)
        {
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.loop = loop; s.playOnAwake = false;
            s.spatialBlend = 1f; s.dopplerLevel = 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 1f; s.maxDistance = 40f;
            return s;
        }

        void Tock(float volume)
        {
            src.pitch = Random.Range(0.92f, 1.08f);
            src.PlayOneShot(hits[Random.Range(0, hits.Length)], volume);
        }

        void OnCollisionStay(Collision c) { lastTouch = Time.time; }

        void OnCollisionEnter(Collision c)
        {
            lastTouch = Time.time;
            if (hits.Length == 0 || Time.time - lastHit < 0.06f) return;
            float v = c.relativeVelocity.magnitude;
            if (v < HitMinSpeed) return;
            lastHit = Time.time; travelled = 0f;
            Tock(HitVolume * Mathf.InverseLerp(HitMinSpeed, HitFullSpeed, v));
        }

        void Update()
        {
            if (hits.Length == 0 || rb.isKinematic || Time.time - lastTouch > 0.1f) return;   // リフト搬送中と空中は無音
            float speed = rb.linearVelocity.magnitude;
            if (speed < RollMinSpeed) return;
            travelled += speed * Time.deltaTime;
            if (travelled < RollTickDistance) return;
            travelled = Random.Range(0f, RollTickDistance * 0.4f);
            Tock(RollVolume * Mathf.Clamp01(speed / RollFullSpeed));
        }
    }
}
