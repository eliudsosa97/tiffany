using UnityEngine;
namespace TiffanyGame
{
    public sealed class WitchPresence : MonoBehaviour
    {
        public WitchController witch;
        public Transform player;
        public Camera view;
        public AudioSource source;
        public float maxVolume = 0.8f;
        private readonly WitchPresenceRules rules = new WitchPresenceRules();
        private bool paused;
        private void Start()
        {
            if (source != null) source.volume = 0f;
        }
        private void Update()
        {
            if (witch == null || player == null || view == null || source == null) return;
            if (Time.timeScale == 0f)
            {
                if (!paused) source.Pause();
                paused = true;
                return;
            }
            if (paused) { source.UnPause(); paused = false; }
            Vector3 offset = player.position - witch.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            Vector3 point = view.WorldToViewportPoint(witch.transform.position + Vector3.up * 1.5f);
            bool onScreen = point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
            rules.Tick(Time.deltaTime, witch.isActiveAndEnabled ? witch.Decision.Form : WitchForm.Flying,
                witch.Decision.State, distance, onScreen);
            float previous = source.volume;
            source.volume = rules.Volume * maxVolume * 0.7f;
            if (source.volume > 0f && previous <= 0f)
            {
                source.Play();
                Debug.Log($"{GameInfo.LogPrefix} Bruja fuera de cuadro: murmullo a {distance:0} m");
            }
            else if (source.volume <= 0f && previous > 0f) source.Stop();
        }
        private void OnDisable()
        {
            if (source != null) { source.Stop(); source.volume = 0f; }
            paused = false;
        }
    }
}
