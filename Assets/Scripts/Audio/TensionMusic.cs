using UnityEngine;
namespace TiffanyGame
{
    public sealed class TensionMusic : MonoBehaviour
    {
        public WitchController witch;
        public Transform player;
        public PlayerHiding hiding;
        public AudioSource drone, pulse;
        public float Level => rules.Level;
        public float Silence => rules.Silence;
        private readonly TensionMusicRules rules = new TensionMusicRules();
        private void Start() => StopLoops();
        private void Update()
        {
            StopLoops();
            if (witch == null || player == null || Time.timeScale == 0f) return;
            Vector3 offset = player.position - witch.transform.position;
            offset.y = 0f;
            rules.Tick(Time.deltaTime, witch.Decision, offset.magnitude, hiding != null && hiding.Hidden, witch.Profile.FollowSpeed);
        }
        private void StopLoops()
        {
            StopLoop(drone);
            StopLoop(pulse);
        }
        private static void StopLoop(AudioSource source)
        {
            if (source == null) return;
            source.volume = 0f;
            if (source.isPlaying) source.Stop();
        }
        private void OnDisable() => StopLoops();
    }
}
