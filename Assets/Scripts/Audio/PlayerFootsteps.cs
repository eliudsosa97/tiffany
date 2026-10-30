using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFootsteps : MonoBehaviour
    {
        public AudioSource source;
        public AudioClip[] dirt, grass, slab, gravel, leaves, branches;
        public float slowVolume = 0.35f, walkVolume = 0.6f, runVolume = 1f;
        public int Steps { get; private set; }
        PlayerController player;
        AudioClip[][] clips;
        FootstepSounds sounds;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            clips = new[] { dirt, grass, slab, gravel, leaves, branches };
            var counts = new int[clips.Length];
            for (int i = 0; i < counts.Length; i++) counts[i] = clips[i]?.Length ?? 0;
            sounds = new FootstepSounds(counts, slowVolume, walkVolume, runVolume);
        }

        void OnEnable() => player.SoundStepped += Play;
        void OnDisable() => player.SoundStepped -= Play;

        void Play(GroundType ground, Gait gait)
        {
            if (source == null) return;
            var sound = sounds.Choose(ground, gait, Random.value);
            if (sound.Index < 0) return;
            var clip = clips[(int)sound.Ground][sound.Index];
            if (clip == null) return;
            source.pitch = sound.Pitch;
            source.PlayOneShot(clip, sound.Volume);
            Steps++;
        }
    }
}
