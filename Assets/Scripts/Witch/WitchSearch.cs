using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchSearch
    {
        private Vector3 center;
        private Vector3 target;
        private float elapsed;
        private float angle;
        private bool hasTarget;
        private bool started;

        public void Begin(Vector3 origin)
        {
            center = origin;
            elapsed = 0f;
            angle = 0f;
            hasTarget = started = false;
        }

        public bool Tick(float dt, Vector3 position, WitchProfile profile,
            Func<Vector3, Vector3> resolve, out Vector3 destination)
        {
            elapsed += dt;
            destination = position;
            if (elapsed >= profile.SearchDuration) return false;
            if (!started)
            {
                started = true;
                hasTarget = WitchTargets.TryResolve(resolve, center, out target);
            }
            else if (hasTarget) hasTarget = WitchTargets.TryResolve(resolve, target, out target);
            if (hasTarget && WitchTargets.Distance(position, target) > profile.ArriveDistance)
            {
                destination = target;
                return true;
            }

            float step = Mathf.Clamp(profile.SearchAngleStep, 1f, 180f);
            float growth = Mathf.Max(0.1f, profile.SearchRadiusGrowth);
            while (true)
            {
                float radius = profile.SearchStartRadius + growth * angle / 360f;
                if (radius > profile.SearchMaxRadius) return false;
                float radians = angle * Mathf.Deg2Rad;
                angle += step;
                Vector3 point = center + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;
                if (!WitchTargets.TryResolve(resolve, point, out target)
                    || WitchTargets.Distance(position, target) <= profile.ArriveDistance) continue;
                hasTarget = true;
                destination = target;
                return true;
            }
        }
    }
}
