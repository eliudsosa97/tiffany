using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchBarkLure
    {
        public Vector3 Target { get; private set; }
        public bool Calling { get; private set; }
        private float delay = -1f;
        private float elapsed;

        public void AdvanceClock(float dt, WitchProfile profile, System.Random random)
        {
            if (delay < 0f) delay = Mathf.Lerp(profile.BarkLureMinDelay, profile.BarkLureMaxDelay, (float)random.NextDouble());
            delay = Mathf.Max(0f, delay - dt);
        }

        public bool TryBegin(in WitchPerception p, WitchProfile profile, System.Random random,
            Func<Vector3, Vector3> resolve)
        {
            if (delay != 0f) return false;
            delay = -1f;
            Vector3 forward = WitchTargets.Direction(p.PlayerPosition,
                p.HasNextClearing ? p.NextClearingPosition : p.PlayerPosition + p.PlayerForward);
            Vector3 side = new Vector3(-forward.z, 0f, forward.x) * (random.NextDouble() < 0.5 ? -1f : 1f);
            if (!WitchTargets.TryResolve(resolve, p.PlayerPosition + side * profile.BarkLureDistance, out var point)) return false;
            Target = point;
            elapsed = 0f;
            Calling = false;
            return true;
        }

        public bool Tick(float dt, Vector3 position, WitchProfile profile)
        {
            elapsed += dt;
            if (!Calling && (WitchTargets.Distance(position, Target) <= profile.ArriveDistance
                || elapsed >= profile.BarkLureTravelLimit))
            {
                Calling = true;
                elapsed = 0f;
            }
            return !Calling || elapsed < profile.BarkLureDuration;
        }

        public void Cancel()
        {
            delay = -1f;
            Calling = false;
        }
    }
}
