using UnityEngine;

namespace TiffanyGame
{
    public sealed class PanicController : MonoBehaviour
    {
        public PlayerLight playerLight;
        public PlayerHiding hiding;
        public WitchController witch;
        public TylerController tyler;
        public AudioSource cueSource;
        public AudioClip[] fakeSteps;
        public AudioClip[] whispers;
        public AudioClip[] sobs;

        public PanicModel Model { get; } = new PanicModel();
        public float Level => Model.Level;
        public PanicStage Stage => Model.Stage;

        private readonly PanicEffects effects = new PanicEffects();
        private Vector3 previousPosition;
        private PanicStage previousStage;

        private void OnEnable() => previousPosition = transform.position;

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            bool witchActive = witch != null && witch.isActiveAndEnabled;
            bool lightOn = (playerLight != null && playerLight.State.IsOn)
                || (tyler != null && tyler.SharingLight);
            var inputs = PanicSenses.Build(lightOn,
                SafeZones.Contains(position),
                witchActive ? Vector3.Distance(position, witch.transform.position) : float.PositiveInfinity,
                witchActive && witch.Decision.VoiceLure, hiding != null && hiding.Hidden,
                TylerMotion.IsStill(previousPosition, position, dt),
                tyler != null && tyler.isActiveAndEnabled && tyler.NearPlayer);
            previousPosition = position;
            Model.Tick(dt, in inputs);
            PanicStage stage = Stage;
            if (stage != previousStage)
            {
                previousStage = stage;
                Debug.Log(stage switch
                {
                    PanicStage.Steps => GameInfo.LogPrefix + " Pánico: Steps",
                    PanicStage.FalseFires => GameInfo.LogPrefix + " Pánico: FalseFires",
                    PanicStage.Children => GameInfo.LogPrefix + " Pánico: Children",
                    _ => GameInfo.LogPrefix + " Pánico: Calm"
                });
            }
            PanicCues cues = effects.Tick(dt, stage, Time.time, hiding != null && hiding.Hidden);
            if (playerLight != null) playerLight.intensityScale = cues.LightScale;
            if (cues.FakeStep)
            {
                PlayCue(fakeSteps, Random.Range(-0.8f, 0.8f));
                Debug.Log(GameInfo.LogPrefix + " Paso falso");
            }
            if (cues.Whisper)
            {
                PlayCue(whispers, 0f);
                Debug.Log(GameInfo.LogPrefix + " Susurro");
            }
            if (cues.Sob)
            {
                NoiseBus.Publish(new StepNoise(position, PanicEffects.SobRadius));
                PlayCue(sobs, 0f);
                Debug.Log(GameInfo.LogPrefix + " Sollozo");
            }
        }

        private void PlayCue(AudioClip[] clips, float pan)
        {
            if (cueSource == null || clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            cueSource.panStereo = pan;
            cueSource.PlayOneShot(clip);
        }

        private void OnDisable()
        {
            if (playerLight != null) playerLight.intensityScale = 1f;
        }
    }
}
