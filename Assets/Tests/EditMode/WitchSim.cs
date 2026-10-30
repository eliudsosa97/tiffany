using UnityEngine;

namespace TiffanyGame.Tests
{
    internal static class WitchSim
    {
        internal static WitchDecision BeginWarning(WitchBrain brain, WitchProfile profile, ref WitchPerception p)
        {
            WitchDecision decision = Step(brain, ref p, 0f);
            return decision.Intent == WitchIntent.Harass ? Step(brain, ref p, profile.HarassTimeLimit) : decision;
        }

        internal static WitchDecision Step(WitchBrain brain, ref WitchPerception p, float dt)
        {
            Measure(ref p);
            WitchDecision decision = brain.Tick(dt, in p);
            p.WitchPosition = Vector3.MoveTowards(p.WitchPosition, decision.MoveTarget, decision.Speed * (dt > 0f ? dt : 0f));
            Measure(ref p);
            return decision;
        }

        private static void Measure(ref WitchPerception p)
        {
            Vector3 offset = p.PlayerPosition - p.WitchPosition;
            offset.y = 0f;
            p.DistanceToPlayer = offset.magnitude;
        }
    }
}
