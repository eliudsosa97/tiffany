using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchAmbush
    {
        public Vector3 Target { get; private set; }
        private float delay = -1f;
        private float waited;

        public bool TryBegin(float dt, in WitchPerception p, WitchProfile profile,
            System.Random random, Func<Vector3, Vector3> resolve)
        {
            if (delay < 0f) delay = Mathf.Lerp(profile.AmbushMinDelay, profile.AmbushMaxDelay, (float)random.NextDouble());
            delay -= dt;
            if (delay > 0f) return false;
            delay = -1f;
            if (!p.HasNextClearing || WitchTargets.Distance(p.PlayerPosition, p.NextClearingPosition)
                <= profile.AmbushMinPlayerDistance) return false;
            Vector3 edge = p.NextClearingPosition
                + WitchTargets.Direction(p.NextClearingPosition, p.PlayerPosition) * profile.AmbushEdgeOffset;
            if (!WitchTargets.TryResolve(resolve, edge, out var point)) return false;
            Target = point;
            waited = 0f;
            return true;
        }

        public bool Wait(float dt, WitchProfile profile)
        {
            waited += dt;
            return waited < profile.AmbushWait;
        }

        public void Cancel() => delay = -1f;
    }
}
