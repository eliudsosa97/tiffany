using UnityEngine;
namespace TiffanyGame
{
    public sealed class ForestAmbience : MonoBehaviour
    {
        public Transform player;
        public Transform[] clearings = System.Array.Empty<Transform>();
        public float clearingRadius = 30f;
        public WitchController witch;
        public TensionMusic music;
        public PanicController panic;
        public AudioSource[] loops = System.Array.Empty<AudioSource>();
        public AudioClip[] loopClips = System.Array.Empty<AudioClip>();
        public AudioSource calls;
        public AudioClip owl, barnOwl, coyote;
        public AudioClip[] howls;
        private readonly AmbienceMix mix = new AmbienceMix();
        private void Start()
        {
            for (int i = 0; i < loops.Length; i++)
                if (loops[i] != null)
                    (loops[i].clip, loops[i].volume) = (i < loopClips.Length ? loopClips[i] : null, 0f);
        }
        private void Update()
        {
            if (Time.timeScale == 0f || player == null) return;
            int clearing = -1;
            float nearest = clearingRadius * clearingRadius;
            for (int i = 0; i < clearings.Length; i++)
            {
                if (clearings[i] == null) continue;
                float distance = HorizontalSquared(player.position - clearings[i].position);
                if (distance > nearest) continue;
                (nearest, clearing) = (distance, i);
            }
            mix.Tick(Time.deltaTime, clearing, witch != null ? Mathf.Sqrt(HorizontalSquared(player.position - witch.transform.position))
                : float.PositiveInfinity, panic != null ? panic.Model.Level : 0f);
            for (int i = 0; i < loops.Length && i < AmbienceMix.LayerCount; i++)
            {
                var source = loops[i];
                if (source == null) continue;
                float previous = source.volume, volume = mix.Volume((AmbienceLayer)i)
                    * (music != null ? TensionMusicRules.AmbienceDuck(music.Level) : 1f);
                source.volume = volume;
                if (volume > 0f && previous <= 0f) source.Play();
                else if (volume <= 0f && previous > 0f) source.Stop();
            }
            if (!mix.Call || calls == null) return;
            AudioClip clip = mix.CallKind switch
            {
                AmbienceCall.Owl => owl, AmbienceCall.BarnOwl => barnOwl, AmbienceCall.Coyote => coyote,
                _ => howls != null && mix.CallVariant < howls.Length ? howls[mix.CallVariant] : null
            };
            if (clip == null) return;
            calls.PlayOneShot(clip, mix.CallVolume);
            Debug.Log($"{GameInfo.LogPrefix} Ambiente: {mix.CallKind}");
        }
        private static float HorizontalSquared(Vector3 offset) => offset.x * offset.x + offset.z * offset.z;
        private void OnDisable()
        {
            foreach (var source in loops)
                if (source != null) { source.Stop(); source.volume = 0f; }
            if (calls != null) calls.Stop();
        }
    }
}
