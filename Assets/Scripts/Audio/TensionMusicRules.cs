using UnityEngine;
namespace TiffanyGame
{
    public sealed class TensionMusicRules
    {
        public const float FadeIn = 1.5f, FadeOut = 2f, DuckAmount = 0.45f;
        public const float SilenceFade = 0.3f;
        public const float NearDistance = 15f, FarDistance = 40f, DistantLevel = 0.4f, SpeedTolerance = 0.01f;
        public float DroneTarget { get; private set; }
        public float PulseTarget { get; private set; }
        public float Drone { get; private set; }
        public float Pulse { get; private set; }
        public float Silence { get; private set; }
        public float Level => Mathf.Max(Drone, Pulse);
        public static float AmbienceDuck(float level) => 1f - DuckAmount * level;
        public static bool IsChasing(in WitchDecision decision, float followSpeed) => decision.Form == WitchForm.Grounded
            && (decision.State == WitchState.WindUp || decision.State == WitchState.Sprinting || decision.State == WitchState.Recovering
                || (decision.State == WitchState.Stalking && decision.Speed >= followSpeed - SpeedTolerance));
        public void Tick(float dt, in WitchDecision decision, float distanceToPlayer, bool playerHidden, float followSpeed)
        {
            WitchState state = decision.State;
            bool silencing = decision.Form == WitchForm.Grounded && state == WitchState.WindUp;
            bool audible = decision.Form == WitchForm.Grounded && !playerHidden
                && (state == WitchState.Stalking || state == WitchState.Sprinting
                    || state == WitchState.Recovering || state == WitchState.Stunned);
            float distanceFactor = Mathf.Lerp(1f, DistantLevel,
                Mathf.Clamp01((distanceToPlayer - NearDistance) / (FarDistance - NearDistance)));
            DroneTarget = audible ? distanceFactor : 0f;
            PulseTarget = audible && IsChasing(decision, followSpeed) ? distanceFactor : 0f;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float fadeOut = silencing ? SilenceFade : FadeOut;
            Drone = Mathf.MoveTowards(Drone, DroneTarget, dt / (DroneTarget > Drone ? FadeIn : fadeOut));
            Pulse = Mathf.MoveTowards(Pulse, PulseTarget, dt / (PulseTarget > Pulse ? FadeIn : fadeOut));
            Silence = Mathf.MoveTowards(Silence, silencing ? 1f : 0f, dt / (silencing ? SilenceFade : FadeIn));
        }
    }
}
