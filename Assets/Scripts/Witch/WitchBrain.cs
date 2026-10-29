using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchBrain
    {
        private WitchProfile profile;
        private readonly Vector3[] route;
        private readonly System.Random random;
        private readonly Func<Vector3, Vector3> resolveGroundTarget;
        private readonly WitchTactics tactics;
        private readonly WitchCatchUp catchUp = new WitchCatchUp();
        private WitchDecision tacticDecision;
        private bool hasTacticDecision;
        private WitchState state = WitchState.Flying;
        private int routeIndex;
        private float stateTime;
        private float returnGraceRemaining;
        private float timeWithoutPerception;
        private bool hasTarget;
        private bool targetIsPlayer;
        private bool chasing;
        private bool warned;
        private bool wasHidden;
        private bool wasVanished;
        private bool sawPlayer;
        private bool sawHiding;
        private bool wasSeeingLight;
        private WitchState suspicionReturnState;
        private Vector3 suspicionOrigin;
        private bool hasNoiseTarget;
        private bool hasHideTarget;
        private bool inspectingNoise;
        private bool retreatingNoise;
        private float inspectionTime;
        private Vector3 hideOrigin;
        private Vector3 hideProgress;
        private float hideStallTime;
        private float timeFar;
        private float roamDelay;
        private bool roaming;
        private Vector3 roamTarget;
        private Vector3 target;
        private Vector3 verticalTarget;
        private bool landingOnTarget;
        private Vector3 sprintEnd;
        public WitchWatch Watch { get; private set; }

        public void BeginWatch(Vector3 post, bool requiresVanish)
        {
            Watch = new WitchWatch(post, requiresVanish);
            tactics.Forget();
            hasTarget = chasing = warned = targetIsPlayer = roaming = false;
            hasNoiseTarget = hasHideTarget = inspectingNoise = retreatingNoise = false;
            timeWithoutPerception = 0f;
            Enter(WitchState.Stalking, post);
        }

        public bool Chasing => chasing;

        public void StartGrace() => returnGraceRemaining = profile.GroundChase ? profile.ReturnGraceDuration : 0f;

        public WitchBrain(WitchProfile profile, IReadOnlyList<Vector3> skyRoute, System.Random random = null,
            Func<Vector3, Vector3> resolveGroundTarget = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.random = random ?? new System.Random();
            this.resolveGroundTarget = resolveGroundTarget;
            tactics = new WitchTactics(this.random, resolveGroundTarget);
            roamDelay = NextRoamDelay();
            if (profile.GroundChase) state = WitchState.Stalking;
            route = new Vector3[skyRoute?.Count ?? 0];
            for (int i = 0; i < route.Length; i++)
            {
                route[i] = skyRoute[i];
            }
        }

        public WitchDecision Tick(float dt, in WitchPerception p)
        {
            dt = dt < 0f || float.IsNaN(dt) || float.IsInfinity(dt) ? 0f : dt;
            if (returnGraceRemaining > 0f)
            {
                float graceTime = Mathf.Min(dt, returnGraceRemaining);
                returnGraceRemaining -= graceTime;
                dt -= graceTime;
                if (returnGraceRemaining > 0f)
                    return new WitchDecision
                    {
                        State = WitchState.Stalking,
                        Form = WitchForm.Grounded,
                        MoveTarget = p.WitchPosition
                    };
            }
            WitchDecision decision = TickState(dt, in p);
            decision.CatchUp = catchUp.Tick(dt, profile, in p, decision.State);
            return decision;
        }

        public void CatchUpPlaced(Vector3 playerPosition)
        {
            catchUp.Placed();
            tactics.Forget();
            target = playerPosition;
            targetIsPlayer = hasTarget = chasing = true;
            warned = hasNoiseTarget = hasHideTarget = inspectingNoise = retreatingNoise = false;
            timeWithoutPerception = 0f;
            Enter(WitchState.Stalking, playerPosition);
        }

        public void CatchUpBlocked() => catchUp.Retry(profile);

        private WitchDecision TickState(float dt, in WitchPerception p)
        {
            hasTacticDecision = false;
            sawHiding = p.PlayerHidden && (wasHidden ? sawHiding : sawPlayer);
            if (p.PlayerVanished && !wasVanished) sawHiding = false;
            bool trackable = !p.PlayerVanished && !p.PlayerInSafeZone && (!p.PlayerHidden || sawHiding);
            bool grounded = state == WitchState.Stalking || state == WitchState.WindUp || state == WitchState.Sprinting
                || state == WitchState.Recovering || state == WitchState.Stunned
                || (state == WitchState.Suspicious && suspicionReturnState == WitchState.Stalking);
            bool noiseHeard = grounded && p.HeardNoise && !p.HearingMuted;
            bool noiseFromPlayer = noiseHeard && HorizontalDistance(p.NoisePosition, p.PlayerPosition) <= profile.PlayerNoiseMatch;
            if (p.PlayerVanished && noiseFromPlayer) noiseHeard = false;
            if (Watch == null && tactics.Observe(dt, profile, in p, grounded, trackable, noiseHeard, noiseHeard && noiseFromPlayer,
                trackable && p.PlayerHidden && sawHiding))
                hasTarget = targetIsPlayer = chasing = warned = false;
            bool detected = trackable && (Watch != null
                ? p.InWatchPassage && (!Watch.PassageClear(p.WitchPosition) || Watch.RequiresVanish)
                : (profile.GroundChase && (catchUp.Hunting || !tactics.Investigating)) || p.SeesPlayer || p.SmellsPlayer
                    || (noiseHeard && noiseFromPlayer && (chasing
                        || HorizontalDistance(p.WitchPosition, p.NoisePosition) <= profile.ClearNoiseDistance))
                    || (p.PlayerHidden && sawHiding));
            if (Watch != null)
            {
                Watch.Tick(dt, p.WitchPosition);
                if (noiseHeard && !noiseFromPlayer) Watch.Hear(p.NoisePosition);
                if (!detected) noiseHeard = false;
            }
            bool lostEdge = (p.PlayerVanished && !wasVanished) || (p.PlayerHidden && !wasHidden && !sawHiding);
            bool seesLight = SeesLight(in p);
            bool newLight = seesLight && !wasSeeingLight;
            wasSeeingLight = seesLight;
            wasHidden = p.PlayerHidden;
            wasVanished = p.PlayerVanished;
            sawPlayer = p.SeesPlayer && !p.PlayerHidden && !p.PlayerVanished && !p.PlayerInSafeZone;
            if (state == WitchState.Caught) return Decide(in p);

            stateTime += dt;
            UpdateRoamingTimer(dt, in p);
            if (grounded)
            {
                if (lostEdge)
                {
                    if (chasing || targetIsPlayer) hasTarget = false;
                    chasing = warned = false;
                }
                if (p.PlayerInSafeZone) chasing = warned = false;
                if (p.Flashed)
                {
                    if (detected) TargetPlayer(in p);
                    Enter(WitchState.Stunned, p.WitchPosition);
                    return Decide(in p);
                }
            }
            if (state == WitchState.Suspicious)
            {
                AdvanceSuspicion(in p, trackable, detected, noiseHeard, noiseFromPlayer, newLight);
                return Decide(in p);
            }
            if (grounded)
            {
                if (state == WitchState.Stalking && !chasing && noiseHeard && !detected && !profile.GroundChase)
                {
                    TargetNoise(in p);
                    Suspect(WitchState.Stalking, p.NoisePosition, p.WitchPosition);
                    return Decide(in p);
                }
                if (detected)
                {
                    if (hasNoiseTarget) chasing = true;
                    TargetPlayer(in p);
                }
                else if (noiseHeard) TargetNoise(in p);
                else timeWithoutPerception += dt;
            }
            Advance(dt, in p, trackable, detected, newLight);
            return Decide(in p);
        }

        private void TargetPlayer(in WitchPerception p)
        {
            target = p.PlayerPosition;
            targetIsPlayer = hasTarget = true;
            hasNoiseTarget = hasHideTarget = inspectingNoise = retreatingNoise = false;
            timeWithoutPerception = 0f;
        }

        private void TargetNoise(in WitchPerception p)
        {
            target = p.HasNoiseHide ? p.NoiseHidePosition : p.NoisePosition;
            targetIsPlayer = false;
            hasTarget = hasNoiseTarget = true;
            hasHideTarget = p.HasNoiseHide;
            inspectingNoise = retreatingNoise = false;
            hideOrigin = hideProgress = p.WitchPosition;
            hideStallTime = 0f;
            timeWithoutPerception = 0f;
        }

        private void Suspect(WitchState returnState, Vector3 origin, Vector3 position)
        {
            suspicionReturnState = returnState;
            suspicionOrigin = origin;
            Enter(WitchState.Suspicious, position);
        }

        private void AdvanceSuspicion(in WitchPerception p, bool trackable, bool detected,
            bool noiseHeard, bool noiseFromPlayer, bool newLight)
        {
            bool listening = stateTime < profile.SuspicionDuration;
            if (suspicionReturnState == WitchState.Flying)
            {
                if (p.Raining && profile.CanLand)
                {
                    hasTarget = false;
                    Enter(WitchState.Landing, p.WitchPosition);
                }
                else if ((profile.CanLand && trackable && p.SeesPlayer) || ClearLight(in p) || (listening && newLight))
                {
                    target = SeesLight(in p) ? p.PlayerLightPosition : p.PlayerPosition;
                    targetIsPlayer = !SeesLight(in p);
                    hasTarget = true;
                    Enter(WitchState.Flying, p.WitchPosition);
                    AdvanceFlying(in p, false);
                }
                else if (!listening) Enter(WitchState.Flying, p.WitchPosition);
                return;
            }
            if (detected || (listening && noiseHeard))
            {
                bool confirmedPlayer = detected || (trackable && noiseFromPlayer);
                if (confirmedPlayer) TargetPlayer(in p);
                else TargetNoise(in p);
                chasing = confirmedPlayer;
                Enter(WitchState.Stalking, p.WitchPosition);
                Advance(0f, in p, trackable, confirmedPlayer, false);
            }
            else if (!listening)
            {
                timeWithoutPerception = 0f;
                Enter(WitchState.Stalking, p.WitchPosition);
            }
        }

        private void UpdateRoamingTimer(float dt, in WitchPerception p)
        {
            if (HorizontalDistance(p.WitchPosition, p.PlayerPosition) > profile.FarDistance) timeFar += dt;
            else if (timeFar > 0f)
            {
                timeFar = 0f;
                roamDelay = NextRoamDelay();
            }
        }

        private float NextRoamDelay() => Mathf.Lerp(profile.RoamMinDelay, profile.RoamMaxDelay, (float)random.NextDouble());

        private void RoamNear(in WitchPerception p)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Lerp(profile.RoamMinDistance, profile.RoamMaxDistance, (float)random.NextDouble());
            roamTarget = p.PlayerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            roamTarget.y = WitchMotion.CruiseHeight(route, p.PlayerPosition, p.WitchPosition.y)
                + (route.Length > 0 ? profile.FlightHeightOffset : 0f);
            roaming = true;
        }

        private static bool SeesLight(in WitchPerception p) => p.SeesPlayerLight && !p.PlayerHidden && !p.PlayerVanished;

        private bool ClearLight(in WitchPerception p) => SeesLight(in p)
            && HorizontalDistance(p.WitchPosition, p.PlayerLightPosition) <= profile.ClearLightDistance;

        public void SetProfile(WitchProfile next) =>
            profile = next ?? throw new ArgumentNullException(nameof(next));

        private void Advance(float dt, in WitchPerception p, bool trackable, bool detected, bool newLight)
        {
            if (TryTactic(dt, in p)) return;
            switch (state)
            {
                case WitchState.Flying:
                    AdvanceFlying(in p, newLight);
                    break;
                case WitchState.Landing:
                    if (landingOnTarget && SafeZones.Contains(verticalTarget))
                    {
                        if (!WitchTargets.TryResolve(resolveGroundTarget, verticalTarget, out var safeTarget)
                            || SafeZones.Contains(safeTarget))
                        {
                            hasTarget = false;
                            Enter(WitchState.TakingOff, p.WitchPosition);
                            break;
                        }
                        verticalTarget = safeTarget;
                        stateTime = 0f;
                    }
                    if (stateTime >= profile.LandingDuration)
                    {
                        timeWithoutPerception = 0f;
                        Enter(WitchState.Stalking, p.WitchPosition);
                        TryTactic(0f, in p);
                    }
                    break;
                case WitchState.Stalking:
                    if (hasTarget && !WitchTargets.TryResolve(resolveGroundTarget, target, out target)) hasTarget = false;
                    if (Watch == null && detected && ((chasing && warned) || !profile.CanSprint) && p.DistanceToPlayer < profile.CatchDistance)
                    {
                        Enter(WitchState.Caught, p.WitchPosition);
                        break;
                    }
                    if (detected && (profile.CanSprint || Watch != null) && p.DistanceToPlayer < profile.SprintTriggerDistance)
                    {
                        Enter(WitchState.WindUp, p.WitchPosition);
                        break;
                    }
                    if (hasTarget && hasNoiseTarget)
                    {
                        bool arrived = HorizontalDistance(p.WitchPosition, target) <= profile.ArriveDistance;
                        if (!retreatingNoise && !inspectingNoise && arrived)
                        {
                            inspectingNoise = true;
                            inspectionTime = 0f;
                        }
                        else if (inspectingNoise) inspectionTime += dt;
                        else if (HorizontalDistance(p.WitchPosition, hideProgress) >= profile.ArriveDistance)
                        {
                            hideProgress = p.WitchPosition;
                            hideStallTime = 0f;
                        }
                        else hideStallTime += dt;
                        bool finished = retreatingNoise ? arrived || hideStallTime >= profile.LoseTrackTime
                            : inspectingNoise ? inspectionTime >= profile.HideInspectionDuration : hideStallTime >= profile.LoseTrackTime;
                        if (!finished) break;
                        bool leaveOnFoot = !retreatingNoise && (p.Raining || profile.GroundChase);
                        hasTarget = hasNoiseTarget = hasHideTarget = inspectingNoise = retreatingNoise = false;
                        chasing = warned = false;
                        timeWithoutPerception = profile.LoseTrackTime;
                        if (leaveOnFoot)
                        {
                            Vector3 retreat = hideOrigin - target;
                            retreat.y = 0f;
                            if (retreat.sqrMagnitude < 1e-6f) retreat = Vector3.forward;
                            target = p.WitchPosition + retreat.normalized * profile.HideRetreatDistance;
                            hasTarget = hasNoiseTarget = retreatingNoise = WitchTargets.TryResolve(resolveGroundTarget, target, out target);
                            hideProgress = p.WitchPosition;
                            hideStallTime = 0f;
                        }
                        if (TryTactic(0f, in p)) break;
                    }
                    if (!detected && hasTarget && HorizontalDistance(p.WitchPosition, target) <= profile.ArriveDistance)
                    {
                        hasTarget = hasNoiseTarget = hasHideTarget = retreatingNoise = false;
                        chasing = warned = false;
                    }
                    if (Watch != null && !detected) hasTarget = chasing = false;
                    if (Watch == null && timeWithoutPerception >= profile.LoseTrackTime && !p.Raining && !profile.GroundChase)
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
                            if ((FlightPoint(i) - p.WitchPosition).sqrMagnitude < (FlightPoint(routeIndex) - p.WitchPosition).sqrMagnitude)
                            {
                                routeIndex = i;
                            }
                        }
                        hasTarget = false;
                        tactics.Cancel();
                        Enter(WitchState.Flying, p.WitchPosition);
                    }
                    break;
                default:
                    break;
            }
        }

        private void AdvanceFlying(in WitchPerception p, bool newLight)
        {
            if (p.Raining && profile.CanLand)
            {
                hasTarget = false;
                Enter(WitchState.Landing, p.WitchPosition);
                return;
            }
            bool seesPlayer = profile.CanLand && p.SeesPlayer
                && !p.PlayerVanished && !p.PlayerHidden && !p.PlayerInSafeZone;
            if (newLight && !ClearLight(in p) && !seesPlayer && !hasTarget)
            {
                Suspect(WitchState.Flying, p.PlayerLightPosition, p.WitchPosition);
                return;
            }
            bool seesTarget = (SeesLight(in p) && (ClearLight(in p) || hasTarget)) || seesPlayer;
            if (seesTarget)
            {
                target = SeesLight(in p) ? p.PlayerLightPosition : p.PlayerPosition;
                targetIsPlayer = !SeesLight(in p);
                hasTarget = true;
                roaming = false;
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
                if (!canLand && !seesTarget && distance <= profile.ArriveDistance)
                {
                    hasTarget = false;
                }
            }
            if (!hasTarget)
            {
                if (roaming && Vector3.Distance(p.WitchPosition, roamTarget) <= profile.ArriveDistance) roaming = false;
                else if (!roaming && timeFar >= roamDelay) RoamNear(in p);
            }
            if (!hasTarget && !roaming && route.Length > 0 && Vector3.Distance(p.WitchPosition, FlightPoint(routeIndex)) <= profile.ArriveDistance)
            {
                routeIndex = (routeIndex + 1) % route.Length;
            }
        }

        private WitchDecision Decide(in WitchPerception p)
        {
            if (hasTacticDecision) return tacticDecision;
            bool flyingSuspicion = state == WitchState.Suspicious && suspicionReturnState == WitchState.Flying;
            var decision = new WitchDecision
            {
                State = state,
                Intent = tactics.Intent,
                Form = state == WitchState.Flying || state == WitchState.Landing || state == WitchState.TakingOff || flyingSuspicion
                    ? WitchForm.Flying : WitchForm.Grounded,
                MoveTarget = p.WitchPosition,
                FireLit = state == WitchState.Flying || state == WitchState.TakingOff || flyingSuspicion,
                InspectingHide = state == WitchState.Stalking && hasHideTarget && inspectingNoise
            };
            switch (state)
            {
                case WitchState.Flying:
                    Vector3 patrolPoint = route.Length > 0 ? FlightPoint(routeIndex) : p.WitchPosition;
                    decision.MoveTarget = hasTarget ? new Vector3(target.x, patrolPoint.y, target.z)
                        : roaming ? roamTarget : patrolPoint;
                    decision.Speed = profile.FlySpeed;
                    break;
                case WitchState.Landing:
                case WitchState.TakingOff:
                    decision.MoveTarget = verticalTarget;
                    break;
                case WitchState.Stalking:
                    decision.MoveTarget = hasTarget ? target : p.WitchPosition;
                    decision.Speed = hasTarget && !inspectingNoise
                        ? (chasing || profile.GroundChase ? profile.FollowSpeed : profile.StalkSpeed) : 0f;
                    if (Watch != null)
                    {
                        decision.MoveTarget = hasTarget ? target : Watch.Target;
                        decision.Speed = HorizontalDistance(p.WitchPosition, decision.MoveTarget) > 0.15f ? profile.FollowSpeed : 0f;
                    }
                    decision.VoiceLure = profile.UsesVoiceLure && !profile.BarkLureEnabled && !chasing;
                    break;
                case WitchState.WindUp:
                    decision.MoveTarget = target;
                    break;
                case WitchState.Suspicious:
                    decision.MoveTarget = suspicionOrigin;
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
            landingOnTarget = false;
            if (next == WitchState.Stalking && hasTarget
                && !WitchTargets.TryResolve(resolveGroundTarget, target, out target)) hasTarget = false;
            if (next == WitchState.WindUp) chasing = warned = true;
            if (next == WitchState.Stunned || next == WitchState.TakingOff || next == WitchState.Landing) chasing = warned = false;
            if (next == WitchState.TakingOff || next == WitchState.Landing)
                roaming = hasNoiseTarget = hasHideTarget = inspectingNoise = retreatingNoise = false;
        }

        private bool TryTactic(float dt, in WitchPerception p)
        {
            if (catchUp.Hunting || Watch != null || (state != WitchState.Flying && state != WitchState.Stalking)) return false;
            if (state == WitchState.Stalking && hasNoiseTarget && !p.PlayerAtFamilyCandle) return false;
            if (!tactics.TryDecide(dt, profile, in p, state, !hasTarget, chasing, out tacticDecision)) return false;
            if ((tacticDecision.State == WitchState.Landing || (tacticDecision.State == WitchState.Stalking && tacticDecision.Speed > 0f))
                && !WitchTargets.TryResolve(resolveGroundTarget, tacticDecision.MoveTarget, out tacticDecision.MoveTarget))
            {
                tactics.Forget();
                hasTarget = false;
                return false;
            }
            if (tacticDecision.State != state) Enter(tacticDecision.State, p.WitchPosition);
            if (state == WitchState.Landing)
            {
                verticalTarget = tacticDecision.MoveTarget;
                landingOnTarget = true;
            }
            if (state == WitchState.Flying)
                tacticDecision.MoveTarget.y = WitchMotion.CruiseHeight(route, tacticDecision.MoveTarget, p.WitchPosition.y)
                    + (route.Length > 0 ? profile.FlightHeightOffset : 0f);
            hasTarget = targetIsPlayer = chasing = warned = false;
            hasNoiseTarget = hasHideTarget = inspectingNoise = retreatingNoise = false;
            timeWithoutPerception = 0f;
            hasTacticDecision = true;
            return true;
        }

        private Vector3 FlightPoint(int index) => route[index] + Vector3.up * profile.FlightHeightOffset;

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
