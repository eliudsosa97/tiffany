using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-100)]
    public sealed class Storm : MonoBehaviour
    {
        public bool raining;
        public Light lightning;
        public float lightningIntensity = 3f;
        public ParticleSystem rain;
        public AudioSource thunderSource;
        public AudioClip[] thunderClips;

        StormCycle cycle;
        PlayerController[] players;
        WitchController[] witches;
        bool previousRaining;

        public bool Flashing => cycle != null && cycle.Flashing;

        public void Strike() => (cycle ??= new StormCycle(System.Environment.TickCount)).Strike();

        void Start()
        {
            cycle ??= new StormCycle(System.Environment.TickCount);
            players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            witches = FindObjectsByType<WitchController>(FindObjectsSortMode.None);
            previousRaining = !raining;
            if (thunderSource != null) thunderSource.spatialBlend = 0f;
        }

        void Update()
        {
            cycle.Active = raining;
            cycle.Tick(Time.deltaTime);
            if (lightning != null)
            {
                lightning.intensity = cycle.FlashLevel * lightningIntensity;
                lightning.enabled = cycle.Flashing;
            }
            if (rain != null && previousRaining != raining)
            {
                if (raining) rain.Play();
                else rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (previousRaining && !raining && thunderSource != null) thunderSource.Stop();
            previousRaining = raining;
            foreach (var player in players)
            {
                if (player != null) player.raining = raining;
            }
            foreach (var witch in witches)
            {
                if (witch == null) continue;
                witch.raining = raining;
                witch.hearingMuted = cycle.ThunderSounding;
            }
            if (cycle.ThunderStartedThisTick && thunderSource != null && thunderClips != null && thunderClips.Length > 0)
            {
                var clip = thunderClips[Random.Range(0, thunderClips.Length)];
                if (clip != null) thunderSource.PlayOneShot(clip);
            }
        }
    }
}
