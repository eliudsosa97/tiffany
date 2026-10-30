using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchHarass
    {
        private enum Phase { None, Retreat, Look, Circle, Cover, Hide }

        private readonly System.Random random;
        private readonly Func<Vector3, Vector3> resolve;
        private Phase phase;
        private float elapsed, phaseTime, lookDuration, cooldown;
        private float radius, arc, angle, turn;
        private Vector3 center, radial, destination, circleDestination;
        private bool hasDestination, lightSeen, awaitingWarning;

        public bool Active => phase != Phase.None;
        public bool AvoidingLight { get; private set; }

        public WitchHarass(System.Random random, Func<Vector3, Vector3> resolveGroundTarget = null)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            resolve = resolveGroundTarget;
        }

        public void AdvanceClock(float dt) => cooldown = Mathf.Max(0f, cooldown - dt);

        public void Cancel()
        {
            phase = Phase.None;
            hasDestination = false;
            AvoidingLight = false;
        }

        public void CompleteWarning(WitchProfile profile)
        {
            if (!awaitingWarning) return;
            awaitingWarning = false;
            cooldown = profile.HarassCooldown;
        }

        public bool Tick(float dt, WitchProfile profile, in WitchPerception p, out WitchDecision decision)
        {
            decision = default;
            if (!Active)
            {
                if (awaitingWarning || cooldown > 0f) return false;
                elapsed = phaseTime = angle = 0f;
                center = p.PlayerPosition;
                radial = WitchTargets.Direction(center, p.WitchPosition);
                lookDuration = Range(profile.HarassMinLookDuration, profile.HarassMaxLookDuration);
                radius = Range(profile.HarassMinRadius, profile.HarassMaxRadius);
                arc = Range(profile.HarassMinArc, profile.HarassMaxArc);
                turn = random.NextDouble() < 0.5 ? -1f : 1f;
                phase = p.DistanceToPlayer < profile.HarassCloseDistance ? Phase.Retreat : Phase.Look;
                lightSeen = hasDestination = false;
                dt = 0f;
            }
            elapsed += dt;
            if (elapsed >= profile.HarassTimeLimit
                || (phase != Phase.Retreat && p.DistanceToPlayer < profile.HarassCloseDistance)) return Finish();

            if (phase == Phase.Retreat)
            {
                if (p.DistanceToPlayer >= profile.HarassRetreatDistance)
                {
                    phase = Phase.Look;
                    phaseTime = 0f;
                    center = p.PlayerPosition;
                    radial = WitchTargets.Direction(center, p.WitchPosition);
                    dt = 0f;
                }
                else
                {
                    Vector3 retreat = p.PlayerPosition + WitchTargets.Direction(p.PlayerPosition, p.WitchPosition)
                        * (profile.HarassRetreatDistance + profile.ArriveDistance);
                    if (!WitchTargets.TryResolve(resolve, retreat, out var ground)) return Still(in p, out decision);
                    return Move(profile, ground, out decision);
                }
            }
            if (phase == Phase.Look)
            {
                phaseTime += dt;
                if (phaseTime < lookDuration) return Still(in p, out decision);
                phase = Phase.Circle;
                hasDestination = false;
            }
            bool lit = p.SeesPlayerLight || p.PlayerLightTouchesWitch;
            bool newLight = lit && !lightSeen;
            lightSeen = lit;
            if (phase == Phase.Hide)
            {
                phaseTime += dt;
                if (phaseTime < profile.HarassHideDuration) return Still(in p, out decision);
                phase = Phase.Circle;
                destination = circleDestination;
            }
            if (phase == Phase.Circle && newLight)
            {
                AvoidingLight = true;
                if (!hasDestination) NextPoint(profile, p.PlayerPosition);
                circleDestination = destination;
                if (p.HasCover && WitchTargets.TryResolve(resolve, p.CoverPosition, out var cover)
                    && WitchTargets.Distance(cover, p.PlayerPosition) >= profile.HarassCloseDistance)
                    destination = cover;
                else
                {
                    if (!NextPoint(profile, p.PlayerPosition) && !hasDestination) return Still(in p, out decision);
                    circleDestination = destination;
                }
                phase = Phase.Cover;
            }
            if (phase == Phase.Cover)
            {
                if (WitchTargets.Distance(p.WitchPosition, destination) <= profile.ArriveDistance)
                {
                    phase = Phase.Hide;
                    phaseTime = 0f;
                    return Still(in p, out decision);
                }
                return Move(profile, destination, out decision);
            }
            if (!hasDestination || WitchTargets.Distance(p.WitchPosition, destination) <= profile.ArriveDistance)
            {
                if (hasDestination && angle >= arc) return Finish();
                hasDestination = false;
                if (!NextPoint(profile, p.PlayerPosition)) return Still(in p, out decision);
            }
            return Move(profile, destination, out decision);
        }

        private bool NextPoint(WitchProfile profile, Vector3 playerPosition)
        {
            center = playerPosition;
            float candidate = angle;
            while (candidate < arc)
            {
                candidate = Mathf.Min(arc, candidate + Mathf.Max(1f, profile.HarassAngleStep));
                Vector3 point = center + Quaternion.AngleAxis(turn * candidate, Vector3.up) * radial * radius;
                if (!WitchTargets.TryResolve(resolve, point, out var ground)) continue;
                float distance = WitchTargets.Distance(ground, center);
                if (distance < profile.HarassMinRadius - 1e-4f || distance > profile.HarassMaxRadius + 1e-4f) continue;
                destination = ground;
                angle = candidate;
                hasDestination = true;
                return true;
            }
            return false;
        }

        private static bool Move(WitchProfile profile, Vector3 point, out WitchDecision decision)
        {
            decision = Decision(point, profile.StalkSpeed);
            return true;
        }

        private static bool Still(in WitchPerception p, out WitchDecision decision)
        {
            decision = Decision(p.PlayerPosition, 0f);
            return true;
        }

        private bool Finish()
        {
            Cancel();
            awaitingWarning = true;
            return false;
        }

        private static WitchDecision Decision(Vector3 target, float speed) => new WitchDecision
        {
            State = WitchState.Stalking,
            Form = WitchForm.Grounded,
            Intent = WitchIntent.Harass,
            MoveTarget = target,
            Speed = speed
        };

        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
