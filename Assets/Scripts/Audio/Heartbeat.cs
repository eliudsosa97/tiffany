using UnityEngine;

namespace TiffanyGame
{
    public sealed class Heartbeat : MonoBehaviour
    {
        public AudioSource source;
        public AudioClip clip;
        public PanicController panic;
        public WitchController witch;
        public int Beats { get; private set; }
        readonly HeartbeatRate rate = new HeartbeatRate();
        int lastLoggedBpm;

        void Update()
        {
            if (Time.timeScale == 0f) return;
            float distance = float.PositiveInfinity;
            if (witch != null)
            {
                Vector3 offset = witch.transform.position - transform.position;
                offset.y = 0f;
                distance = offset.magnitude;
            }
            float intensity = HeartbeatRate.Intensity(panic != null ? panic.Model.Level : 0f, distance);
            float bpm = HeartbeatRate.BeatsPerMinute(intensity);
            if (HeartbeatRate.ShouldLog(bpm, lastLoggedBpm))
            {
                lastLoggedBpm = Mathf.RoundToInt(bpm);
                Debug.Log($"{GameInfo.LogPrefix} Latidos: {bpm:0} lpm");
            }
            if (!rate.Tick(Time.deltaTime, bpm) || source == null || clip == null) return;
            source.PlayOneShot(clip, HeartbeatRate.Volume(intensity));
            Beats++;
        }
    }
}
