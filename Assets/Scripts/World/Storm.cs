using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-100)]
    public sealed class Storm : MonoBehaviour
    {
        public bool raining;
        public WitchController witch;
        public bool followChapter = true;
        public Light moon;
        public float moonFlashBoost = 2.5f;
        public Color flashAmbient = new Color(0.32f, 0.36f, 0.45f);
        public ParticleSystem rain;
        public AudioSource rainSource;
        public AudioSource thunderSource;
        public AudioClip[] thunderClips;
        StormCycle cycle;
        PlayerController[] players;
        WitchController[] witches;
        bool previousRaining;
        float baseMoonIntensity;
        Color baseAmbient;
        bool initialized;
        float appliedFlashLevel;

        public bool Flashing => cycle != null && cycle.Flashing;

        public void Strike()
        {
            if (followChapter && witch != null && !StormSchedule.IsRaining(witch.chapter)) return;
            (cycle ??= new StormCycle(System.Environment.TickCount)).Strike();
        }

        void ApplySchedule()
        {
            if (followChapter && witch != null) raining = StormSchedule.IsRaining(witch.chapter);
        }

        void Start()
        {
            cycle ??= new StormCycle(System.Environment.TickCount);
            players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            witches = FindObjectsByType<WitchController>(FindObjectsSortMode.None);
            ApplySchedule();
            previousRaining = !raining;
            if (moon != null) baseMoonIntensity = moon.intensity;
            baseAmbient = RenderSettings.ambientLight;
            initialized = true;
            if (rainSource != null) rainSource.spatialBlend = 0f;
            if (thunderSource != null) thunderSource.spatialBlend = 0f;
        }

        void OnDisable()
        {
            if (!initialized) return;
            if (moon != null) moon.intensity = baseMoonIntensity;
            RenderSettings.ambientLight = baseAmbient;
            appliedFlashLevel = 0f;
        }

        void Update()
        {
            ApplySchedule();
            cycle.Active = raining;
            cycle.Tick(Time.deltaTime);
            float flashLevel = cycle.FlashLevel;
            if (flashLevel != appliedFlashLevel)
            {
                if (moon != null) moon.intensity = baseMoonIntensity + flashLevel * moonFlashBoost;
                RenderSettings.ambientLight = Color.Lerp(baseAmbient, flashAmbient, flashLevel);
                appliedFlashLevel = flashLevel;
            }
            if (previousRaining != raining)
            {
                if (raining)
                {
                    if (rain != null) rain.Play();
                    if (rainSource != null) rainSource.Play();
                }
                else
                {
                    if (rain != null) rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (rainSource != null) rainSource.Stop();
                }
            }
            if (previousRaining && !raining && thunderSource != null) thunderSource.Stop();
            previousRaining = raining;
            foreach (var player in players)
            {
                if (player != null) player.raining = raining;
            }
            foreach (var target in witches)
            {
                if (target == null) continue;
                target.raining = raining;
                target.hearingMuted = cycle.ThunderSounding;
            }
            if (cycle.ThunderStartedThisTick && thunderSource != null && thunderClips != null && thunderClips.Length > 0)
            {
                var clip = thunderClips[Random.Range(0, thunderClips.Length)];
                if (clip != null)
                {
                    thunderSource.PlayOneShot(clip);
                    Debug.Log($"{GameInfo.LogPrefix} Trueno: {clip.name}");
                }
            }
        }
    }
}
