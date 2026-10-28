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
        private bool chasing;
        private bool wasHidden;
        private bool wasVanished;
        private bool sawPlayer;
        private bool sawHiding;
        private Vector3 target;
        private Vector3 verticalTarget;
        private Vector3 sprintEnd;

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
            sawHiding = p.PlayerHidden && (wasHidden ? sawHiding : sawPlayer);
            bool trackable = !p.PlayerVanished && !p.PlayerInSafeZone && (!p.PlayerHidden || sawHiding);
            bool noiseHeard = p.HeardNoise && !p.HearingMuted;
            bool noiseFromPlayer = noiseHeard && HorizontalDistance(p.NoisePosition, p.PlayerPosition) <= profile.PlayerNoiseMatch;
            bool detected = trackable && (p.SeesPlayer || p.SmellsPlayer || noiseFromPlayer || (p.PlayerHidden && sawHiding));
            bool lostEdge = (p.PlayerVanished && !wasVanished) || (p.PlayerHidden && !wasHidden && !sawHiding);
            wasHidden = p.PlayerHidden;
            wasVanished = p.PlayerVanished;
            sawPlayer = p.SeesPlayer;
            if (state == WitchState.Caught) return Decide(in p);

            dt = dt < 0f || float.IsNaN(dt) ? 0f : dt;
            stateTime += dt;
            if (state == WitchState.Stalking || state == WitchState.WindUp || state == WitchState.Sprinting
                || state == WitchState.Recovering || state == WitchState.Stunned)
            {
                if (lostEdge)
                {
                    hasTarget = false;
                    chasing = false;
                }
                if (noiseHeard || detected)
                {
                    target = detected ? p.PlayerPosition : p.NoisePosition;
                    hasTarget = true;
                    timeWithoutPerception = 0f;
                }
                else
                {
                    timeWithoutPerception += dt;
                }
                if (p.Flashed)
                {
                    Enter(WitchState.Stunned, p.WitchPosition);
                    return Decide(in p);
                }
            }
            Advance(in p, trackable, detected);
            return Decide(in p);
        }

        private void Advance(in WitchPerception p, bool trackable, bool detected)
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
                    if (detected && p.DistanceToPlayer < profile.CatchDistance)
                    {
                        Enter(WitchState.Caught, p.WitchPosition);
                        break;
                    }
                    if (detected && profile.CanSprint && p.DistanceToPlayer < profile.SprintTriggerDistance)
                    {
                        Enter(WitchState.WindUp, p.WitchPosition);
                        break;
                    }
                    if (!detected && hasTarget && HorizontalDistance(p.WitchPosition, target) <= profile.ArriveDistance)
                    {
                        hasTarget = false;
                        chasing = false;
                    }
                    if (timeWithoutPerception >= profile.LoseTrackTime && !p.Raining)
                    {
                        hasTarget = false;
                        Enter(WitchState.TakingOff, p.WitchPosition);
                    }
                    break;
                case WitchState.WindUp:
                    if (!trackable)
                    {
                        Enter(WitchState.Stalking, p.WitchPosition);
                    }
                    else if (stateTime >= profile.WindUpDuration)
                    {
                        Vector3 direction = p.PlayerPosition - p.WitchPosition;
                        direction.y = 0f;
                        if (direction.sqrMagnitude < 1e-6f)
                        {
                            direction = target - p.WitchPosition;
                            direction.y = 0f;
                        }
                        if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
                        sprintEnd = p.WitchPosition + direction.normalized * profile.SprintSpeed * profile.SprintDuration;
                        Enter(WitchState.Sprinting, p.WitchPosition);
                    }
                    break;
                case WitchState.Sprinting:
                    if (trackable && p.DistanceToPlayer < profile.CatchDistance)
                        Enter(WitchState.Caught, p.WitchPosition);
                    else if (stateTime >= profile.SprintDuration)
                        Enter(WitchState.Recovering, p.WitchPosition);
                    break;
                case WitchState.Recovering:
                    if (stateTime >= profile.RecoverDuration) Enter(WitchState.Stalking, p.WitchPosition);
                    break;
                case WitchState.Stunned:
                    if (stateTime >= profile.StunDuration) Enter(WitchState.Stalking, p.WitchPosition);
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
                Form = state == WitchState.Flying || state == WitchState.Landing || state == WitchState.TakingOff
                    ? WitchForm.Flying : WitchForm.Grounded,
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
                    decision.MoveTarget = hasTarget ? target : p.WitchPosition;
                    decision.Speed = hasTarget ? (chasing ? profile.FollowSpeed : profile.StalkSpeed) : 0f;
                    decision.VoiceLure = profile.UsesVoiceLure && !chasing;
                    break;
                case WitchState.WindUp:
                    decision.MoveTarget = target;
                    break;
                case WitchState.Sprinting:
                    decision.MoveTarget = sprintEnd;
                    decision.Speed = profile.SprintSpeed;
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
            if (next == WitchState.WindUp) chasing = true;
            if (next == WitchState.Stunned) chasing = false;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
