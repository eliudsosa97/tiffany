using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class PlaythroughDodge
    {
        public const float Range = 12f;
        public const float Duration = 1.2f;
        readonly Func<Vector3, Vector3, bool> clear;
        float remaining;
        bool dodging;

        public Vector3 Direction { get; private set; }
        public bool Started { get; private set; }
        public bool Repath { get; private set; }

        public PlaythroughDodge(Func<Vector3, Vector3, bool> clear) =>
            this.clear = clear ?? throw new ArgumentNullException(nameof(clear));

        public bool Tick(float dt, Vector3 girl, Vector3 girlForward, WitchState witchState, Vector3 witch, bool canStart)
        {
            Started = Repath = false;
            if (witchState == WitchState.Caught)
            {
                remaining = 0f;
                dodging = false;
                return false;
            }
            if (dodging)
            {
                remaining -= dt;
                if (remaining > 0f || witchState == WitchState.Sprinting) return true;
                remaining = 0f;
                dodging = false;
                Repath = true;
                return false;
            }
            if (!canStart || witchState != WitchState.Sprinting
                || PlaythroughPlan.HorizontalDistance(girl, witch) > Range) return false;
            Vector3 away = girl - witch;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : -Flat(girlForward);
            Vector3 side = new Vector3(-away.z, 0f, away.x);
            if (Vector3.Dot(side, girlForward) < 0f) side = -side;
            Direction = clear(girl, side) ? side : clear(girl, -side) ? -side : away;
            remaining = Duration;
            dodging = Started = true;
            return true;
        }

        static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 1e-6f ? vector.normalized : Vector3.forward;
        }
    }
}
