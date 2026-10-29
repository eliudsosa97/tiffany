using System;
using UnityEngine;

namespace TiffanyGame
{
    internal sealed class WitchTactics
    {
        private readonly WitchLastSeen memory = new WitchLastSeen();
        private readonly WitchSearch search = new WitchSearch();
        private readonly WitchAmbush ambush = new WitchAmbush();
        private readonly WitchCandleWatch candle = new WitchCandleWatch();
        private readonly WitchBarkLure bark = new WitchBarkLure();
        private readonly System.Random random;
        private readonly Func<Vector3, Vector3> resolve;
        private bool perceived;
        private bool blocked;
        private bool onlyRain;

        public WitchIntent Intent { get; private set; }
        public bool Investigating => Intent == WitchIntent.Search || Intent == WitchIntent.LastSeen || memory.Pending;

        public WitchTactics(System.Random random, Func<Vector3, Vector3> resolve)
        {
            this.random = random;
            this.resolve = resolve;
        }

        public bool Observe(float dt, WitchProfile profile, in WitchPerception p, bool grounded,
            bool trackable, bool noiseHeard, bool playerNoise, bool seenHiding)
        {
            memory.Observe(in p, grounded, trackable, playerNoise);
            if (!profile.LastSeenEnabled) memory.Cancel();
            candle.Observe(dt, in p);
            if (profile.BarkLureEnabled && Intent != WitchIntent.BarkLure) bark.AdvanceClock(dt, profile, random);
            perceived = seenHiding || noiseHeard
                || (trackable && (p.SeesPlayer || p.SeesPlayerLight || (grounded && p.SmellsPlayer)));
            blocked = p.Raining || p.PlayerVanished || p.Flashed;
            onlyRain = p.Raining && !p.PlayerVanished && !p.Flashed;
            bool rainRetreat = onlyRain && Intent == WitchIntent.CandleWatch && candle.Retreating;
            bool safeInterrupt = p.PlayerInSafeZone && (Intent != WitchIntent.CandleWatch || !candle.Matches(in p));
            bool candleExit = Intent == WitchIntent.CandleWatch && !candle.Matches(in p);
            bool disabled = (Intent == WitchIntent.Search && !profile.SearchEnabled)
                || (Intent == WitchIntent.LastSeen && !profile.LastSeenEnabled)
                || (Intent == WitchIntent.Ambush && !profile.AmbushEnabled)
                || (Intent == WitchIntent.CandleWatch && !profile.CandleWatchEnabled)
                || (Intent == WitchIntent.BarkLure && !profile.BarkLureEnabled);
            if (!perceived && (!blocked || rainRetreat) && !safeInterrupt && !candleExit && !disabled) return false;
            bool interrupted = Intent != WitchIntent.None;
            Cancel();
            memory.Cancel();
            return interrupted;
        }

        public void Cancel()
        {
            if (Intent == WitchIntent.Ambush) ambush.Cancel();
            if (Intent == WitchIntent.BarkLure) bark.Cancel();
            Intent = WitchIntent.None;
        }

        public void Forget()
        {
            memory.Forget();
            Cancel();
        }

        public bool TryDecide(float dt, WitchProfile profile, in WitchPerception p,
            WitchState state, bool idle, bool chasing, out WitchDecision decision)
        {
            decision = default;
            if (perceived || (state != WitchState.Flying && state != WitchState.Stalking)) return false;
            bool grounded = state == WitchState.Stalking;
            if (blocked)
            {
                if (!RetreatsInRain(in p, profile, grounded)) return false;
            }
            else if (p.PlayerInSafeZone)
            {
                if (!grounded || !profile.CandleWatchEnabled || !p.PlayerAtFamilyCandle) return false;
                if (Intent != WitchIntent.CandleWatch)
                {
                    if (!memory.HasLocation || !candle.TryBegin(in p, profile, resolve)) return false;
                    Cancel();
                    Intent = WitchIntent.CandleWatch;
                }
            }
            else if (Intent == WitchIntent.None)
            {
                if (profile.LastSeenEnabled && memory.Pending && (grounded || profile.CanLand))
                {
                    if (memory.Begin(resolve)) Intent = WitchIntent.LastSeen;
                    else
                    {
                        memory.Forget();
                        decision = Move(state, p.WitchPosition, 0f);
                        return true;
                    }
                }
                if (Intent == WitchIntent.None && grounded && profile.SearchEnabled && memory.HasLocation)
                    BeginSearch(memory.Location);
                if (Intent == WitchIntent.None && !grounded && idle && profile.CanLand && profile.AmbushEnabled
                    && ambush.TryBegin(dt, in p, profile, random, resolve)) Intent = WitchIntent.Ambush;
                if (Intent == WitchIntent.None && grounded && !chasing && profile.BarkLureEnabled
                    && bark.TryBegin(in p, profile, random, resolve)) Intent = WitchIntent.BarkLure;
            }

            switch (Intent)
            {
                case WitchIntent.LastSeen:
                    if (!memory.Tick(dt, profile)) return Finish(in p, profile, out decision);
                    if (!grounded) return ApproachFromAir(memory.Target, in p, profile, out decision);
                    if (WitchTargets.Distance(p.WitchPosition, memory.Target) > profile.ArriveDistance)
                    {
                        decision = Move(WitchState.Stalking, memory.Target, profile.FollowSpeed);
                        return true;
                    }
                    if (profile.SearchEnabled)
                    {
                        BeginSearch(memory.Target);
                        return Search(0f, in p, profile, out decision);
                    }
                    memory.Forget();
                    Cancel();
                    decision = Move(WitchState.Stalking, p.WitchPosition, 0f);
                    return true;
                case WitchIntent.Search:
                    return Search(dt, in p, profile, out decision);
                case WitchIntent.Ambush:
                    if (!grounded) return ApproachFromAir(ambush.Target, in p, profile, out decision);
                    if (!ambush.Wait(dt, profile)) return Finish(in p, profile, out decision);
                    decision = Move(WitchState.Stalking, p.WitchPosition, 0f);
                    return true;
                case WitchIntent.CandleWatch:
                    if (!candle.Tick(dt, in p, profile, resolve, out var circlePoint))
                        return Finish(in p, profile, out decision);
                    decision = Move(WitchState.Stalking, circlePoint, profile.StalkSpeed);
                    return true;
                case WitchIntent.BarkLure:
                    if (!bark.Tick(dt, p.WitchPosition, profile))
                    {
                        Cancel();
                        decision = Move(WitchState.Stalking, p.WitchPosition, 0f);
                        return true;
                    }
                    decision = Move(WitchState.Stalking, bark.Calling ? p.WitchPosition : bark.Target,
                        bark.Calling ? 0f : profile.StalkSpeed);
                    decision.VoiceLure = bark.Calling && profile.UsesVoiceLure;
                    return true;
                default:
                    return false;
            }
        }

        // Con lluvia no despega: al vencer el límite se aleja a pie para no dejarla atrapada en la veladora.
        private bool RetreatsInRain(in WitchPerception p, WitchProfile profile, bool grounded)
        {
            if (!onlyRain || !grounded || !profile.CandleWatchEnabled) return false;
            if (Intent == WitchIntent.CandleWatch) return true;
            if (!candle.Due(in p, profile)) return false;
            Intent = WitchIntent.CandleWatch;
            return true;
        }

        private void BeginSearch(Vector3 center)
        {
            search.Begin(center);
            memory.Cancel();
            Intent = WitchIntent.Search;
        }

        private bool Search(float dt, in WitchPerception p, WitchProfile profile, out WitchDecision decision)
        {
            if (!search.Tick(dt, p.WitchPosition, profile, resolve, out var point))
                return Finish(in p, profile, out decision);
            decision = Move(WitchState.Stalking, point, profile.StalkSpeed);
            return true;
        }

        private bool ApproachFromAir(Vector3 target, in WitchPerception p, WitchProfile profile, out WitchDecision decision)
        {
            bool arrived = WitchTargets.Distance(p.WitchPosition, target) <= profile.ArriveDistance;
            decision = Move(arrived ? WitchState.Landing : WitchState.Flying, target, arrived ? 0f : profile.FlySpeed);
            return true;
        }

        private bool Finish(in WitchPerception p, WitchProfile profile, out WitchDecision decision)
        {
            memory.Forget();
            bool onFoot = profile.GroundChase || p.Raining;
            if (onFoot) Cancel();
            decision = Move(onFoot ? WitchState.Stalking : WitchState.TakingOff, p.WitchPosition, 0f);
            return true;
        }

        private WitchDecision Move(WitchState state, Vector3 target, float speed)
        {
            bool flying = state == WitchState.Flying || state == WitchState.Landing || state == WitchState.TakingOff;
            return new WitchDecision
            {
                State = state,
                Intent = Intent,
                Form = flying ? WitchForm.Flying : WitchForm.Grounded,
                FireLit = state == WitchState.Flying || state == WitchState.TakingOff,
                MoveTarget = target,
                Speed = speed
            };
        }
    }
}
