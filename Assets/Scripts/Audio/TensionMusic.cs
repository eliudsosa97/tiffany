using UnityEngine;
namespace TiffanyGame
{
    public sealed class TensionMusic : MonoBehaviour
    {
        public WitchController witch;
        public Transform player;
        public PlayerHiding hiding;
        public AudioSource drone, pulse;
        public float maxVolume = 0.55f;
        public float Level => rules.Level;
        private readonly TensionMusicRules rules = new TensionMusicRules();
        private bool paused;
        private int dominant;
        private void Start()
        {
            if (drone != null) drone.volume = 0f;
            if (pulse != null) pulse.volume = 0f;
        }
        private void Update()
        {
            if (witch == null || player == null || drone == null || pulse == null) return;
            if (Time.timeScale == 0f)
            {
                if (!paused) { drone.Pause(); pulse.Pause(); }
                paused = true;
                return;
            }
            if (paused) { drone.UnPause(); pulse.UnPause(); paused = false; }
            Vector3 offset = player.position - witch.transform.position;
            offset.y = 0f;
            rules.Tick(Time.deltaTime, witch.Decision, offset.magnitude, hiding != null && hiding.Hidden, witch.Profile.FollowSpeed);
            UpdateLoop(drone, rules.Drone * maxVolume);
            UpdateLoop(pulse, rules.Pulse * maxVolume);
            int next = rules.Pulse > 0f && rules.Pulse >= rules.Drone ? 2 : rules.Drone > 0f ? 1 : 0;
            if (next == dominant) return;
            dominant = next;
            Debug.Log(next == 2 ? GameInfo.LogPrefix + " Música: persecución"
                : next == 1 ? GameInfo.LogPrefix + " Música: acecho" : GameInfo.LogPrefix + " Música: silencio");
        }
        private static void UpdateLoop(AudioSource source, float volume)
        {
            float previous = source.volume;
            source.volume = volume;
            if (volume > 0f && previous <= 0f) source.Play();
            else if (volume <= 0f && previous > 0f) source.Stop();
        }
        private void OnDisable()
        {
            if (drone != null) { drone.Stop(); drone.volume = 0f; }
            if (pulse != null) { pulse.Stop(); pulse.volume = 0f; }
            paused = false;
        }
    }
}
