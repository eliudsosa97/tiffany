using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchCandleWatch
    {
        private readonly Vector3[] cooldownCenters = new Vector3[32];
        private readonly float[] cooldowns = new float[32];
        private Vector3 residence;
        private float insideTime;
        private bool inside;
        private Vector3 center;
        private Vector3 target;
        private float radius;
        private float angle;
        private float retreatTime;
        private bool retreating;
        private int cooldownSlot;

        public void Observe(float dt, in WitchPerception p)
        {
            for (int i = 0; i < cooldowns.Length; i++) cooldowns[i] = Mathf.Max(0f, cooldowns[i] - dt);
            if (!p.PlayerAtFamilyCandle)
            {
                inside = false;
                insideTime = 0f;
                return;
            }
            if (!inside || residence != p.FamilyCandlePosition) insideTime = 0f;
            residence = p.FamilyCandlePosition;
            inside = true;
            insideTime += dt;
        }

        public bool Matches(in WitchPerception p) => p.PlayerAtFamilyCandle && center == p.FamilyCandlePosition;

        public bool TryBegin(in WitchPerception p, WitchProfile profile, Func<Vector3, Vector3> resolve)
        {
            if (!inside || insideTime >= profile.CandleWatchLimit) return false;
            int available = -1;
            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i] > 0f && cooldownCenters[i] == residence) return false;
                if (cooldowns[i] <= 0f) available = i;
            }
            if (available < 0) return false;
            center = residence;
            radius = Mathf.Max(profile.CandleWatchRadius, p.FamilyCandleRadius + profile.CandleWatchMargin);
            Vector3 direction = WitchTargets.Direction(center, p.WitchPosition);
            angle = Mathf.Atan2(direction.z, direction.x);
            if (!ResolveCirclePoint(resolve, p.FamilyCandleRadius, profile)) return false;
            cooldownSlot = available;
            retreating = false;
            retreatTime = 0f;
            return true;
        }

        public bool Tick(float dt, in WitchPerception p, WitchProfile profile,
            Func<Vector3, Vector3> resolve, out Vector3 destination)
        {
            destination = target;
            if (!retreating && insideTime >= profile.CandleWatchLimit)
            {
                cooldownCenters[cooldownSlot] = center;
                cooldowns[cooldownSlot] = profile.CandleWatchCooldown;
                retreating = true;
                Vector3 point = center + WitchTargets.Direction(center, p.WitchPosition)
                    * (radius + profile.CandleWatchRetreatDistance);
                if (!WitchTargets.TryResolve(resolve, point, out target)
                    || WitchTargets.Distance(center, target) <= p.FamilyCandleRadius) return false;
                destination = target;
                return true;
            }
            bool arrived = WitchTargets.Distance(p.WitchPosition, target) <= profile.ArriveDistance;
            if (retreating)
            {
                retreatTime += dt;
                return !arrived && retreatTime < profile.CandleWatchRetreatDuration;
            }
            if (arrived)
            {
                float safeStep = 2f * Mathf.Acos(Mathf.Clamp01((p.FamilyCandleRadius + profile.ArriveDistance) / radius));
                angle += Mathf.Min(Mathf.Clamp(profile.CandleWatchAngleStep, 1f, 90f) * Mathf.Deg2Rad, safeStep);
                if (!ResolveCirclePoint(resolve, p.FamilyCandleRadius, profile)) return false;
                destination = target;
            }
            return true;
        }

        private bool ResolveCirclePoint(Func<Vector3, Vector3> resolve, float safeRadius, WitchProfile profile)
        {
            float step = Mathf.Clamp(profile.CandleWatchAngleStep, 1f, 90f) * Mathf.Deg2Rad;
            int attempts = Mathf.CeilToInt(Mathf.PI * 2f / step);
            for (int i = 0; i < attempts; i++)
            {
                if (WitchTargets.TryResolve(resolve,
                    center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius, out target)
                    && WitchTargets.Distance(center, target) > safeRadius) return true;
                angle += step;
            }
            return false;
        }
    }
}
