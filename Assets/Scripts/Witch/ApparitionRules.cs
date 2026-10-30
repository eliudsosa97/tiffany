using System;
namespace TiffanyGame
{
    public enum ApparitionState { Waiting, Shown, Vanishing, Done }
    public enum ApparitionEvent { None, Appeared, VanishedQuietly }
    public sealed class ApparitionRules
    {
        public const float ShowMin = 12f, ShowMax = 25f, WitchClearance = 30f;
        public const float VanishNear = 8f, ForgetDistance = 35f, FadeSeconds = 0.3f;
        public ApparitionState State { get; private set; }
        public float Alpha { get; private set; }
        private float elapsed;
        public ApparitionEvent Tick(float dt, float playerDistance, bool lit, bool chasing, float witchDistance)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return ApparitionEvent.None;
            if (State == ApparitionState.Vanishing)
            {
                elapsed = Math.Min(FadeSeconds, elapsed + dt);
                Alpha = 1f - elapsed / FadeSeconds;
                if (elapsed >= FadeSeconds) State = ApparitionState.Done;
            }
            else if (State == ApparitionState.Waiting)
            {
                if (playerDistance < ShowMin || playerDistance > ShowMax || chasing || witchDistance < WitchClearance)
                    return ApparitionEvent.None;
                State = ApparitionState.Shown;
                Alpha = 1f;
                return ApparitionEvent.Appeared;
            }
            else if (State == ApparitionState.Shown)
            {
                bool quiet = chasing || witchDistance < WitchClearance || playerDistance > ForgetDistance;
                if (!quiet && playerDistance >= VanishNear && !lit) return ApparitionEvent.None;
                State = ApparitionState.Vanishing;
                return ApparitionEvent.VanishedQuietly;
            }
            return ApparitionEvent.None;
        }
    }
}
