using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElVelador
{
    public sealed class WitchBrain
    {
        private readonly WitchProfile profile;
        private readonly Vector3[] route;
        private WitchState state = WitchState.Flying;
        private int routeIndex;
        private float stateTime;
        private float timeWithoutPerception;
        private bool hasTarget;
        private Vector3 target;
        private Vector3 verticalTarget;

        public WitchBrain(WitchProfile profile, IReadOnlyList<Vector3> skyRoute)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            route = new Vector3[skyRoute?.Count ?? 0];
            for (int i = 0; i < route.Length; i++)
            {
                route[i] = skyRoute[i];
            }
        }

        public WitchDecision Tick(float dt, in WitchPerception p)
        {
            dt = dt < 0f || float.IsNaN(dt) ? 0f : dt;
            stateTime += dt;
            Advance(dt, in p);
            return Decide(in p);
        }

        private void Advance(float dt, in WitchPerception p)
        {
            switch (state)
            {
                case WitchState.Flying:
                    AdvanceFlying(in p);
                    break;
                case WitchState.Landing:
                    if (stateTime >= profile.LandingDuration)
                    {
                        timeWithoutPerception = 0f;
                        Enter(WitchState.Stalking, p.WitchPosition);
                    }
                    break;
                case WitchState.Stalking:
                    if (p.HeardNoise && !p.HearingMuted)
                    {
                        target = p.NoisePosition;
                        hasTarget = true;
                        timeWithoutPerception = 0f;
                    }
                    else
                    {
                        timeWithoutPerception += dt;
                    }
                    if (hasTarget && HorizontalDistance(p.WitchPosition, target) <= profile.ArriveDistance)
                    {
                        hasTarget = false;
                    }
                    if (timeWithoutPerception >= profile.LoseTrackTime && !p.Raining)
                    {
                        hasTarget = false;
                        Enter(WitchState.TakingOff, p.WitchPosition);
                    }
                    break;
                case WitchState.TakingOff:
                    if (p.Raining)
                    {
                        hasTarget = false;
                        Enter(WitchState.Landing, p.WitchPosition);
                    }
                    else if (stateTime >= profile.TakeOffDuration)
                    {
                        routeIndex = 0;
                        for (int i = 1; i < route.Length; i++)
                        {
                            if ((route[i] - p.WitchPosition).sqrMagnitude < (route[routeIndex] - p.WitchPosition).sqrMagnitude)
                            {
                                routeIndex = i;
                            }
                        }
                        hasTarget = false;
                        Enter(WitchState.Flying, p.WitchPosition);
                    }
                    break;
                default:
                    break;
            }
        }

        private void AdvanceFlying(in WitchPerception p)
        {
            if (p.Raining)
            {
                hasTarget = false;
                Enter(WitchState.Landing, p.WitchPosition);
                return;
            }
            if (p.SeesPlayerLight)
            {
                target = p.PlayerLightPosition;
                hasTarget = true;
            }
            if (hasTarget)
            {
                float distance = HorizontalDistance(p.WitchPosition, target);
                bool canLand = profile.CanLand && !p.PlayerInSafeZone;
                if (canLand && distance <= profile.LandingDistance)
                {
                    Enter(WitchState.Landing, p.WitchPosition);
                    return;
                }
                if (!canLand && !p.SeesPlayerLight && distance <= profile.ArriveDistance)
                {
                    hasTarget = false;
                }
            }
            if (!hasTarget && route.Length > 0 && Vector3.Distance(p.WitchPosition, route[routeIndex]) <= profile.ArriveDistance)
            {
                routeIndex = (routeIndex + 1) % route.Length;
            }
        }

        private WitchDecision Decide(in WitchPerception p)
        {
            var decision = new WitchDecision
            {
                State = state,
                Form = WitchForm.Flying,
                MoveTarget = p.WitchPosition,
                FireLit = state == WitchState.Flying || state == WitchState.TakingOff
            };
            switch (state)
            {
                case WitchState.Flying:
                    decision.MoveTarget = hasTarget ? new Vector3(target.x, p.WitchPosition.y, target.z)
                        : route.Length > 0 ? route[routeIndex] : p.WitchPosition;
                    decision.Speed = profile.FlySpeed;
                    break;
                case WitchState.Landing:
                case WitchState.TakingOff:
                    decision.MoveTarget = verticalTarget;
                    break;
                case WitchState.Stalking:
                    decision.Form = WitchForm.Grounded;
                    decision.MoveTarget = hasTarget ? target : p.WitchPosition;
                    decision.Speed = hasTarget ? profile.StalkSpeed : 0f;
                    decision.VoiceLure = profile.UsesVoiceLure;
                    break;
                default:
                    break;
            }
            return decision;
        }

        private void Enter(WitchState next, Vector3 position)
        {
            state = next;
            stateTime = 0f;
            verticalTarget = position;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
