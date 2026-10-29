using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchTacticsTests
    {
        private WitchProfile profile;
        private WitchBrain brain;
        private WitchPerception p;

        [SetUp]
        public void SetUp()
        {
            profile = new WitchProfile { SearchEnabled = true, CanSprint = false };
            p = new WitchPerception { PlayerPosition = Vector3.right * 15f, PlayerForward = Vector3.forward };
            brain = new WitchBrain(profile, null, new FixedRandom(0.5));
        }

        [Test]
        public void StableProfileResamplesThePendingTrainTracksRoamDelay()
        {
            profile = WitchProfiles.For(Chapter.TrainTracks);
            brain = new WitchBrain(profile, null, new FixedRandom(2d / 3d));
            p.PlayerPosition = Vector3.forward * 60f;
            Assert.AreEqual(p.WitchPosition, Step(5f).MoveTarget);

            profile = WitchProfiles.For(Chapter.Stable);
            brain.SetProfile(profile);
            Assert.AreEqual(p.WitchPosition, Step(26.5f).MoveTarget);
            WitchDecision roam = Step(0.25f);
            Assert.AreEqual(WitchState.Flying, roam.State);
            Assert.AreEqual(WitchIntent.None, roam.Intent);
            Assert.That(Distance(p.PlayerPosition, roam.MoveTarget), Is.InRange(profile.RoamMinDistance, profile.RoamMaxDistance));
        }

        [Test]
        public void SearchVisitsLastKnownPositionThenGrowsThreeMetersPerTurnAndExhaustsTheSpiral()
        {
            profile.SearchDuration = 1000f;
            Ground();
            p.SeesPlayer = true;
            Step(0f);
            Vector3 origin = p.PlayerPosition;
            p.SeesPlayer = false;
            p.PlayerPosition = Vector3.left * 80f;
            WitchDecision decision = Step(0f);
            Assert.AreEqual(WitchIntent.Search, decision.Intent);
            Assert.AreEqual(origin, decision.MoveTarget);
            p.WitchPosition = origin;
            for (int i = 0; i <= 24; i++)
            {
                decision = Step(0f);
                Assert.AreEqual(WitchIntent.Search, decision.Intent);
                Assert.AreEqual(WitchState.Stalking, decision.State);
                Assert.AreEqual(3f + i * 0.5f, Distance(origin, decision.MoveTarget), 1e-4f);
                Assert.AreEqual(profile.StalkSpeed, decision.Speed);
                p.WitchPosition = decision.MoveTarget;
            }
            Assert.AreEqual(WitchState.TakingOff, Step(0f).State);
            Assert.AreEqual(WitchState.Flying, Step(profile.TakeOffDuration).State);
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
        }

        [Test]
        public void SearchGivesUpAfterTwentySecondsEvenWithoutReachingItsFirstPoint()
        {
            BeginSearch();
            Assert.AreEqual(WitchState.Stalking, Step(19.99f).State);
            Assert.AreEqual(WitchState.TakingOff, Step(0.02f).State);
        }

        [TestCase("sight", WitchState.WindUp)]
        [TestCase("smell", WitchState.WindUp)]
        [TestCase("noise", WitchState.Suspicious)]
        [TestCase("flash", WitchState.Stunned)]
        [TestCase("rain", WitchState.Stalking)]
        [TestCase("safe", WitchState.Stalking)]
        [TestCase("vanished", WitchState.Stalking)]
        public void ExistingPerceptionsInterruptSearchBeforeAnyNewDecision(string signal, WitchState expected)
        {
            BeginSearch();
            profile.CanSprint = true;
            p.PlayerPosition = Vector3.right * 5f;
            p.SeesPlayer = signal == "sight";
            p.SmellsPlayer = signal == "smell";
            p.HeardNoise = signal == "noise";
            p.NoisePosition = Vector3.left * 20f;
            p.Flashed = signal == "flash";
            p.Raining = signal == "rain";
            p.PlayerInSafeZone = signal == "safe";
            p.PlayerVanished = signal == "vanished";
            WitchDecision decision = Step(0f);
            Assert.AreEqual(WitchIntent.None, decision.Intent);
            Assert.AreEqual(expected, decision.State);
            Assert.AreNotEqual(WitchState.Sprinting, decision.State, "Percibir durante la búsqueda no omite el aviso.");
        }

        [Test]
        public void PlayerNoiseIsRememberedAfterCheckingTheNearbyHideButDecoyDoesNotBecomePlayerMemory()
        {
            Ground();
            p.HeardNoise = p.HasNoiseHide = true;
            p.NoisePosition = p.PlayerPosition + Vector3.right * 0.5f;
            p.NoiseHidePosition = p.PlayerPosition + Vector3.forward * 3f;
            Vector3 remembered = p.NoisePosition;
            Assert.AreEqual(WitchState.Suspicious, Step(0f).State);
            p.HeardNoise = false;
            p.PlayerPosition = Vector3.left * 60f;
            Assert.AreEqual(p.NoiseHidePosition, Step(profile.SuspicionDuration).MoveTarget);
            p.WitchPosition = p.NoiseHidePosition;
            Assert.IsTrue(Step(0f).InspectingHide);
            WitchDecision search = Step(profile.HideInspectionDuration);
            Assert.AreEqual(WitchIntent.Search, search.Intent);
            Assert.AreEqual(remembered, search.MoveTarget);

            SetUp();
            Ground();
            p.HeardNoise = true;
            p.NoisePosition = Vector3.left * 15f;
            Step(0f);
            p.HeardNoise = false;
            Step(profile.SuspicionDuration);
            p.WitchPosition = p.NoisePosition;
            Step(0f);
            Assert.AreEqual(WitchState.TakingOff, Step(profile.HideInspectionDuration).State);
        }

        [Test]
        public void SmellRemembersPlayerPositionAndMutedOrVanishedNoiseDoesNot()
        {
            Ground();
            p.SmellsPlayer = true;
            Step(0f);
            Vector3 remembered = p.PlayerPosition;
            p.SmellsPlayer = false;
            p.PlayerPosition = Vector3.right * 80f;
            Assert.AreEqual(remembered, Step(0f).MoveTarget);
            foreach (bool vanished in new[] { false, true })
            {
                SetUp();
                Ground();
                p.HeardNoise = true;
                p.NoisePosition = p.PlayerPosition;
                p.PlayerVanished = vanished;
                p.HearingMuted = !vanished;
                Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
                p.HeardNoise = false;
                Assert.AreEqual(WitchState.TakingOff, Step(profile.LoseTrackTime).State);
            }
        }

        [Test]
        public void SearchSkipsUnresolvedPointsAndResolvesEveryVisitedDestination()
        {
            var search = new WitchSearch();
            search.Begin(Vector3.zero);
            int calls = 0;
            bool ResolveSearch(float dt, Vector3 position, out Vector3 target)
            {
                return search.Tick(dt, position, profile, point =>
                {
                    calls++;
                    return point.x >= 0f ? Vector3.positiveInfinity : point + Vector3.up * 2f;
                }, out target);
            }
            Assert.IsTrue(ResolveSearch(0f, Vector3.zero, out var destination));
            Assert.Greater(calls, 2);
            Assert.Less(destination.x, 0f);
            Assert.AreEqual(2f, destination.y);
            Assert.IsFalse(search.Tick(0f, destination, profile, _ => Vector3.positiveInfinity, out _));
        }

        [Test]
        public void SearchSkipsATargetThatBecomesUnresolvableBeforeArrival()
        {
            var search = new WitchSearch();
            Vector3 origin = Vector3.right * 15f;
            search.Begin(origin);
            Assert.IsTrue(search.Tick(0f, Vector3.zero, profile, point => point, out var first));
            Assert.AreEqual(origin, first);
            Assert.IsTrue(search.Tick(1f, Vector3.zero, profile,
                point => point == origin ? Vector3.positiveInfinity : point, out var next));
            Assert.AreNotEqual(first, next);
            Assert.AreEqual(3f, Distance(origin, next), 1e-4f);
        }

        [TestCase(false, 50f)]
        [TestCase(true, 25f)]
        [TestCase(true, 24.9f)]
        public void AmbushRequiresNextClearingAndPlayerStrictlyBeyondMinimumDistance(bool hasNext, float distance)
        {
            PrepareAmbush();
            p.HasNextClearing = hasNext;
            p.NextClearingPosition = p.PlayerPosition + Vector3.forward * distance;
            Assert.AreEqual(WitchIntent.None, Step(1f).Intent);
        }

        [TestCase(0.0, 40f)]
        [TestCase(0.5, 55f)]
        [TestCase(1.0, 70f)]
        public void AmbushDelayUsesInjectedRandom(double random, float delay)
        {
            profile = WitchProfiles.For(Chapter.Stable);
            profile.BarkLureEnabled = false;
            brain = new WitchBrain(profile, null, new FixedRandom(random));
            p.HasNextClearing = true;
            p.NextClearingPosition = p.PlayerPosition + Vector3.forward * 50f;
            Assert.AreEqual(WitchIntent.None, Step(delay - 0.1f).Intent);
            Assert.AreEqual(WitchIntent.Ambush, Step(0.11f).Intent);
        }

        [Test]
        public void AmbushLandsOnApproachEdgeWaitsStillDarkAndSilentThenTakesOff()
        {
            PrepareAmbush();
            WitchDecision flight = Step(1f);
            Vector3 edge = p.NextClearingPosition + (p.PlayerPosition - p.NextClearingPosition).normalized * profile.AmbushEdgeOffset;
            Assert.AreEqual(WitchIntent.Ambush, flight.Intent);
            Assert.AreEqual(0f, Distance(edge, flight.MoveTarget), 1e-4f);
            p.WitchPosition = flight.MoveTarget;
            WitchDecision landing = Step(0f);
            Assert.AreEqual(WitchState.Landing, landing.State);
            Assert.AreEqual(edge, landing.MoveTarget);
            Assert.IsFalse(landing.FireLit || landing.VoiceLure);
            p.WitchPosition = landing.MoveTarget;
            WitchDecision waiting = Step(profile.LandingDuration);
            Assert.AreEqual(WitchState.Stalking, waiting.State);
            Assert.AreEqual(WitchIntent.Ambush, waiting.Intent);
            Assert.AreEqual(0f, waiting.Speed);
            Assert.IsFalse(waiting.FireLit || waiting.VoiceLure);
            Assert.AreEqual(WitchState.Stalking, Step(44.99f).State);
            Assert.AreEqual(WitchState.TakingOff, Step(0.02f).State);
        }

        [Test]
        public void AmbushWarnsWhenPlayerArrivesAndRainCancelsAirborneApproach()
        {
            PrepareAmbush();
            p.WitchPosition = Step(1f).MoveTarget;
            Step(0f);
            p.WitchPosition.y = 0f;
            Step(profile.LandingDuration);
            p.PlayerPosition = p.WitchPosition + Vector3.right * 5f;
            p.SeesPlayer = true;
            WitchDecision warning = Step(0f);
            Assert.AreEqual(WitchIntent.None, warning.Intent);
            Assert.AreEqual(WitchState.WindUp, warning.State);
            Assert.AreEqual(WitchState.WindUp, Step(profile.WindUpDuration - 0.01f).State);
            Assert.AreEqual(WitchState.Sprinting, Step(0.02f).State);
            SetUp();
            PrepareAmbush();
            Step(1f);
            p.Raining = true;
            Assert.AreEqual(WitchState.Landing, Step(0f).State);
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
        }

        [Test]
        public void UnresolvedAmbushEdgeIsSkipped()
        {
            PrepareAmbush();
            brain = new WitchBrain(profile, null, resolveGroundTarget: _ => Vector3.positiveInfinity);
            Assert.AreEqual(WitchIntent.None, Step(1f).Intent);
        }

        [Test]
        public void AmbushUsesFlightRouteHeightNearestTheDestination()
        {
            PrepareAmbush();
            var route = new[] { Vector3.up * 14f, new Vector3(50f, 40f, 0f) };
            brain = new WitchBrain(profile, route);
            Assert.AreEqual(40f, Step(1f).MoveTarget.y, "El borde del siguiente claro usa la altura de vuelo de ese tramo del cerro.");
        }

        [TestCase(WitchIntent.Ambush)]
        [TestCase(WitchIntent.LastSeen)]
        public void CandleLitOnTheDestinationMidLandingMovesTheTouchdownOutsideTheSafeZone(WitchIntent intent)
        {
            WitchDecision landing = BeginTargetedLanding(intent, point => SafeZones.PushOut(point, 1f));
            Assert.AreEqual(intent, landing.Intent);
            Vector3 destination = landing.MoveTarget;
            Step(profile.LandingDuration * 0.5f);
            int zone = SafeZones.Add(destination, 4f);
            try
            {
                p.PlayerPosition = destination;
                p.PlayerInSafeZone = true;
                WitchDecision moved = Step(profile.LandingDuration * 0.5f);
                Assert.AreEqual(WitchState.Landing, moved.State, "El descenso no termina dentro de la veladora recién encendida.");
                Assert.IsFalse(SafeZones.Contains(moved.MoveTarget));
                Assert.AreEqual(WitchState.Landing, Step(profile.LandingDuration - 0.01f).State);
                Assert.AreEqual(moved.MoveTarget, Step(0f).MoveTarget);
                Assert.AreEqual(WitchState.Stalking, Step(0.02f).State);
            }
            finally { SafeZones.Remove(zone); }
        }

        [Test]
        public void CandleLitOnTheDestinationWithoutSafeGroundNearbyAbortsTheLanding()
        {
            WitchDecision landing = BeginTargetedLanding(WitchIntent.Ambush,
                point => SafeZones.Count > 0 ? Vector3.positiveInfinity : point);
            int zone = SafeZones.Add(landing.MoveTarget, 4f);
            try { Assert.AreEqual(WitchState.TakingOff, Step(0f).State); }
            finally { SafeZones.Remove(zone); }
        }

        [TestCase(3f, 6f)]
        [TestCase(8f, 11f)]
        public void CandleWatchCirclesOutsideTheActualSafeRadius(float safeRadius, float watchRadius)
        {
            BeginCandleWatch(safeRadius);
            for (int i = 0; i < 10; i++)
            {
                WitchDecision circle = Step(0f);
                Assert.AreEqual(WitchIntent.CandleWatch, circle.Intent);
                Assert.AreEqual(watchRadius, Distance(circle.MoveTarget, p.FamilyCandlePosition), 1e-4f);
                Assert.AreEqual(profile.StalkSpeed, circle.Speed);
                Assert.IsFalse(circle.FireLit || circle.VoiceLure);
                for (int j = 0; j <= 10; j++)
                    Assert.Greater(Distance(Vector3.Lerp(p.WitchPosition, circle.MoveTarget, j / 10f), p.FamilyCandlePosition), safeRadius);
                p.WitchPosition = circle.MoveTarget;
            }
        }

        [Test]
        public void CandleWatchLeavesAfterTwentySecondsAndRemembersCooldownAcrossOtherCandles()
        {
            profile.CandleWatchEnabled = true;
            var watch = new WitchCandleWatch();
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.FamilyCandleRadius = 3f;
            p.WitchPosition = Vector3.left * 12f;
            watch.Observe(0f, in p);
            Assert.IsTrue(watch.TryBegin(in p, profile, point => point));
            watch.Observe(19.99f, in p);
            Assert.IsTrue(watch.Tick(19.99f, in p, profile, point => point, out var circle));
            p.WitchPosition = circle;
            watch.Observe(0.02f, in p);
            Assert.IsTrue(watch.Tick(0.02f, in p, profile, point => point, out var retreat));
            Assert.Greater(Distance(retreat, p.FamilyCandlePosition), Distance(circle, p.FamilyCandlePosition));
            p.WitchPosition = retreat;
            Assert.IsFalse(watch.Tick(0f, in p, profile, point => point, out _));
            p.FamilyCandlePosition = Vector3.forward * 40f;
            watch.Observe(0f, in p);
            Assert.IsTrue(watch.TryBegin(in p, profile, point => point));
            p.FamilyCandlePosition = Vector3.zero;
            p.PlayerAtFamilyCandle = false;
            watch.Observe(59.99f, in p);
            p.PlayerAtFamilyCandle = true;
            watch.Observe(0f, in p);
            Assert.IsFalse(watch.TryBegin(in p, profile, point => point));
            p.PlayerAtFamilyCandle = false;
            watch.Observe(0.02f, in p);
            p.PlayerAtFamilyCandle = true;
            watch.Observe(0f, in p);
            Assert.IsTrue(watch.TryBegin(in p, profile, point => point));
        }

        [Test]
        public void CandleWatchRetreatEndsInTakeOffAndCannotTrapPlayerWithNewSearch()
        {
            BeginCandleWatch(3f);
            WitchDecision retreat = Step(profile.CandleWatchLimit);
            Assert.AreEqual(WitchIntent.CandleWatch, retreat.Intent);
            p.WitchPosition = retreat.MoveTarget;
            Assert.AreEqual(WitchState.TakingOff, Step(0f).State);
            Assert.AreEqual(WitchState.Flying, Step(profile.TakeOffDuration).State);
            Ground();
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
        }

        [Test]
        public void RainDuringCandleWatchStillRetreatsOnFootAtTheLimitAndRegistersTheCooldown()
        {
            BeginCandleWatch(3f);
            p.WitchPosition = Vector3.left * 6f;
            p.Raining = true;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent, "La lluvia interrumpe la ronda como cualquier percepción.");
            Assert.AreEqual(WitchIntent.None, Step(profile.CandleWatchLimit - 0.01f).Intent);
            WitchDecision retreat = Step(0.02f);
            Assert.AreEqual(WitchIntent.CandleWatch, retreat.Intent);
            Assert.AreEqual(WitchState.Stalking, retreat.State);
            Assert.AreEqual(profile.StalkSpeed, retreat.Speed);
            Assert.AreEqual(12f, Distance(retreat.MoveTarget, p.FamilyCandlePosition), 1e-4f);
            p.WitchPosition = retreat.MoveTarget;
            WitchDecision away = Step(0f);
            Assert.AreEqual(WitchState.Stalking, away.State, "Con lluvia no despega: se queda a pie lejos de la veladora.");
            Assert.AreEqual(WitchIntent.None, away.Intent);
            p.Raining = false;
            p.PlayerAtFamilyCandle = p.PlayerInSafeZone = false;
            p.PlayerPosition = Vector3.left * 20f;
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.PlayerPosition = Vector3.zero;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent, "La retirada con lluvia registra el enfriamiento de esa veladora.");

            SetUp();
            BeginCandleWatch(3f);
            p.WitchPosition = Vector3.left * 6f;
            WitchDecision leaving = Step(profile.CandleWatchLimit);
            p.Raining = true;
            WitchDecision kept = Step(0f);
            Assert.AreEqual(WitchIntent.CandleWatch, kept.Intent, "La lluvia no detiene una retirada ya empezada.");
            Assert.AreEqual(leaving.MoveTarget, kept.MoveTarget);
            Assert.AreEqual(profile.StalkSpeed, kept.Speed);
        }

        [Test]
        public void CandleWatchRequiresKnownPlayerAndTouchedFamilyCandleAndExitCanTriggerWarning()
        {
            profile.CandleWatchEnabled = true;
            Ground();
            p.PlayerInSafeZone = true;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = true;
            p.FamilyCandleRadius = 3f;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent, "Una veladora conocida no revela a una niña nunca localizada.");
            SetUp();
            BeginCandleWatch(3f);
            p.PlayerAtFamilyCandle = p.PlayerInSafeZone = false;
            p.PlayerPosition = p.WitchPosition + Vector3.right * 4f;
            p.SeesPlayer = true;
            profile.CanSprint = true;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
        }

        [Test]
        public void CandleWatchCanReplaceAnActiveSearchAndFlashInterruptsTheWatch()
        {
            BeginSearch();
            profile.CandleWatchEnabled = true;
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.FamilyCandlePosition = p.PlayerPosition;
            p.FamilyCandleRadius = 3f;
            Assert.AreEqual(WitchIntent.CandleWatch, Step(0f).Intent);
            p.Flashed = true;
            WitchDecision stunned = Step(0f);
            Assert.AreEqual(WitchIntent.None, stunned.Intent);
            Assert.AreEqual(WitchState.Stunned, stunned.State);
        }

        [Test]
        public void CandleLimitCountsResidenceBeforeTheWatchStarts()
        {
            var watch = new WitchCandleWatch();
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.FamilyCandleRadius = 3f;
            p.WitchPosition = Vector3.left * 6f;
            watch.Observe(12f, in p);
            Assert.IsTrue(watch.TryBegin(in p, profile, point => point));
            watch.Observe(8f, in p);
            Assert.IsTrue(watch.Tick(8f, in p, profile, point => point, out var retreat));
            Assert.AreEqual(12f, Distance(retreat, p.FamilyCandlePosition), 1e-4f);
        }

        [Test]
        public void CandleWatchSkipsUnresolvedCirclePointsAndRejectsAnEntirelyBlockedCircle()
        {
            var watch = new WitchCandleWatch();
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.FamilyCandleRadius = 3f;
            p.WitchPosition = Vector3.left * 12f;
            watch.Observe(0f, in p);
            int attempts = 0;
            Assert.IsTrue(watch.TryBegin(in p, profile, point =>
            {
                attempts++;
                return point.x <= 0f ? Vector3.positiveInfinity : point;
            }));
            Assert.Greater(attempts, 1);
            Assert.IsFalse(watch.TryBegin(in p, profile, _ => Vector3.positiveInfinity));
        }

        [TestCase(false, 0.0, -15f)]
        [TestCase(false, 1.0, 15f)]
        [TestCase(true, 0.0, -15f)]
        [TestCase(true, 1.0, 15f)]
        public void BarkLureChoosesLateralPointAtFifteenMetersAndOnlyCallsAfterArrival(bool hasNext, double random, float side)
        {
            PrepareBark(random);
            p.HasNextClearing = hasNext;
            p.NextClearingPosition = p.PlayerPosition + Vector3.right * 50f;
            p.PlayerForward = Vector3.right;
            WitchDecision walking = Step(0f);
            Assert.AreEqual(WitchIntent.BarkLure, walking.Intent);
            Assert.AreEqual(p.PlayerPosition + Vector3.forward * side, walking.MoveTarget);
            Assert.AreEqual(15f, Distance(walking.MoveTarget, p.PlayerPosition), 1e-4f);
            Assert.AreEqual(profile.StalkSpeed, walking.Speed);
            Assert.IsFalse(walking.VoiceLure);
            p.WitchPosition = walking.MoveTarget;
            WitchDecision calling = Step(0f);
            Assert.IsTrue(calling.VoiceLure);
            Assert.AreEqual(0f, calling.Speed);
            Assert.IsFalse(calling.FireLit);
            Assert.IsTrue(Step(9.99f).VoiceLure);
            Assert.IsFalse(Step(0.02f).VoiceLure);
        }

        [Test]
        public void BarkLureTravelHasTimeoutAndPerceptionSilencesItImmediately()
        {
            PrepareBark(0.5);
            p.PlayerForward = Vector3.right;
            Assert.IsFalse(Step(0f).VoiceLure);
            WitchDecision calling = Step(profile.BarkLureTravelLimit);
            Assert.IsTrue(calling.VoiceLure);
            Assert.AreEqual(0f, calling.Speed);
            p.HeardNoise = true;
            p.NoisePosition = Vector3.left * 20f;
            WitchDecision interrupted = Step(0f);
            Assert.AreEqual(WitchIntent.None, interrupted.Intent);
            Assert.AreEqual(WitchState.Suspicious, interrupted.State);
            Assert.IsFalse(interrupted.VoiceLure);
        }

        [TestCase(0.0, 25f)]
        [TestCase(1.0, 40f)]
        public void BarkCooldownIsRandomizedAndVoiceIsDisabledWhileWaiting(double random, float delay)
        {
            profile.BarkLureEnabled = true;
            profile.SearchEnabled = false;
            profile.LoseTrackTime = 100f;
            brain = new WitchBrain(profile, null, new FixedRandom(random));
            Ground();
            Assert.IsFalse(Step(delay - profile.LandingDuration - 0.1f).VoiceLure);
            Assert.AreEqual(WitchIntent.BarkLure, Step(0.11f).Intent);
        }

        [Test]
        public void BarkLureCannotInterruptAChaseAndLegacyProfilesStillUseIdleVoice()
        {
            PrepareBark(0.5);
            profile.CanSprint = true;
            p.PlayerPosition = Vector3.right * 5f;
            p.SeesPlayer = true;
            Assert.AreEqual(WitchState.WindUp, Step(0f).State);
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
            SetUp();
            Ground();
            Assert.IsTrue(Step(0f).VoiceLure);
        }

        [Test]
        public void BarkLureDoesNotStartInFlightAndSkipsAnUnresolvedForestPoint()
        {
            profile.BarkLureEnabled = true;
            profile.SearchEnabled = false;
            profile.BarkLureMinDelay = profile.BarkLureMaxDelay = 0f;
            brain = new WitchBrain(profile, null, resolveGroundTarget: _ => Vector3.positiveInfinity);
            Assert.AreEqual(WitchIntent.None, Step(60f).Intent);
            Ground();
            WitchDecision decision = Step(0f);
            Assert.AreEqual(WitchIntent.None, decision.Intent);
            Assert.IsFalse(decision.VoiceLure);
        }

        [Test]
        public void LightsOffInFlightReachesExactLastLightBeforeLandingAndSearching()
        {
            profile.LastSeenEnabled = true;
            p.WitchPosition = Vector3.up * 14f;
            p.SeesPlayerLight = true;
            p.PlayerLightPosition = Vector3.right * 20f;
            Assert.AreEqual(WitchState.Flying, Step(0f).State);
            p.SeesPlayerLight = false;
            p.PlayerLightPosition = p.PlayerPosition = Vector3.right * 80f;
            WitchDecision flight = Step(0f);
            Assert.AreEqual(WitchIntent.LastSeen, flight.Intent);
            Assert.AreEqual(new Vector3(20f, 14f, 0f), flight.MoveTarget);
            p.WitchPosition = new Vector3(10f, 14f, 0f);
            Assert.AreEqual(WitchState.Flying, Step(0f).State, "Apagar la luz exige llegar al punto, no aterrizar a LandingDistance.");
            p.WitchPosition = flight.MoveTarget;
            WitchDecision landing = Step(0f);
            Assert.AreEqual(WitchState.Landing, landing.State);
            Assert.AreEqual(Vector3.right * 20f, landing.MoveTarget);
            p.WitchPosition = landing.MoveTarget;
            WitchDecision search = Step(profile.LandingDuration);
            Assert.AreEqual(WitchIntent.Search, search.Intent);
            Assert.AreEqual(3f, Distance(search.MoveTarget, p.WitchPosition), 1e-4f);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LightsOffOnGroundUsesRealSensesAndFollowsLastSeenAtFollowSpeed(bool searchEnabled)
        {
            profile.LastSeenEnabled = true;
            profile.SearchEnabled = searchEnabled;
            Ground();
            var senses = new WitchSenses(new WitchSenseProfile());
            var input = new WitchSenseInputs
            {
                Form = WitchForm.Grounded, PlayerPosition = Vector3.right * 14f,
                PlayerLightOn = true, LineOfSight = true
            };
            brain.Tick(0f, senses.Sense(in input));
            input.PlayerLightOn = false;
            p = senses.Sense(in input);
            WitchDecision approach = Step(0f);
            Assert.AreEqual(WitchIntent.LastSeen, approach.Intent);
            Assert.AreEqual(input.PlayerPosition, approach.MoveTarget);
            Assert.AreEqual(profile.FollowSpeed, approach.Speed);
            p.PlayerPosition = Vector3.right * 80f;
            Assert.AreEqual(input.PlayerPosition, Step(0f).MoveTarget);
            p.WitchPosition = approach.MoveTarget;
            Assert.AreEqual(searchEnabled ? WitchIntent.Search : WitchIntent.None, Step(0f).Intent);
        }

        [Test]
        public void ReturnInvestigatesLostSightButNeverTakesOffAfterSearch()
        {
            profile = WitchProfiles.For(Chapter.Return);
            brain = new WitchBrain(profile, null);
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            Assert.AreEqual(WitchIntent.LastSeen, Step(0f).Intent);
            p.WitchPosition = p.PlayerPosition;
            p.PlayerPosition = Vector3.right * 80f;
            Assert.AreEqual(WitchIntent.Search, Step(0f).Intent);
            Assert.AreEqual(WitchState.Stalking, Step(profile.SearchDuration).State);
            Assert.AreEqual(profile.FollowSpeed, Step(0f).Speed);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReturnHuntsANoiseDirectlyWithoutSuspicion(bool inSafeZone)
        {
            profile = WitchProfiles.For(Chapter.Return);
            brain = new WitchBrain(profile, null);
            p.PlayerInSafeZone = inSafeZone;
            p.PlayerHidden = !inSafeZone;
            p.HeardNoise = true;
            p.NoisePosition = Vector3.left * 8f;
            for (int i = 0; i < 3; i++)
            {
                WitchDecision decision = Step(0.5f);
                Assert.AreEqual(WitchState.Stalking, decision.State, "En el regreso el ruido la pone a cazar, sin sospecha.");
                Assert.AreEqual(p.NoisePosition, decision.MoveTarget);
                Assert.AreEqual(profile.FollowSpeed, decision.Speed);
            }
        }

        [Test]
        public void FlashOrVanishingDoesNotScheduleLastSeenFromTheLossOfVisibility()
        {
            profile.LastSeenEnabled = true;
            Ground();
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            p.PlayerVanished = true;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
            p.PlayerVanished = false;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            p.Flashed = true;
            Assert.AreEqual(WitchState.Stunned, Step(0f).State);
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
        }

        [Test]
        public void SeenHidingEntryKeepsTrackingInsteadOfSearchingOrFollowingOldLight()
        {
            profile.LastSeenEnabled = profile.CanSprint = true;
            Ground();
            p.SeesPlayer = true;
            Step(0f);
            p.PlayerHidden = true;
            p.SeesPlayer = false;
            p.PlayerPosition = Vector3.right * 5f;
            WitchDecision warning = Step(0f);
            Assert.AreEqual(WitchState.WindUp, warning.State);
            Assert.AreEqual(WitchIntent.None, warning.Intent);
        }

        [Test]
        public void UnresolvedLastSeenDoesNotReuseTheOldFlightTarget()
        {
            profile.LastSeenEnabled = true;
            brain = new WitchBrain(profile, new[] { Vector3.up * 14f }, resolveGroundTarget: _ => Vector3.positiveInfinity);
            p.SeesPlayerLight = true;
            p.PlayerLightPosition = Vector3.right * 20f;
            Step(0f);
            p.SeesPlayerLight = false;
            Assert.AreEqual(WitchIntent.None, Step(0f).Intent);
            Assert.AreEqual(Vector3.up * 14f, Step(0f).MoveTarget);
        }

        [Test]
        public void SearchAndLastSeenTargetsStayOutsideSafeZonesAfterResolution()
        {
            int zone = SafeZones.Add(p.PlayerPosition, 5f);
            try
            {
                profile.LastSeenEnabled = true;
                brain = new WitchBrain(profile, null, resolveGroundTarget: point => SafeZones.PushOut(point, 1f));
                Ground();
                p.SeesPlayer = true;
                Step(0f);
                p.SeesPlayer = false;
                WitchDecision lastSeen = Step(0f);
                Assert.AreEqual(WitchIntent.LastSeen, lastSeen.Intent);
                Assert.IsFalse(SafeZones.Contains(lastSeen.MoveTarget));
                p.WitchPosition = lastSeen.MoveTarget;
                for (int i = 0; i < 10; i++)
                {
                    WitchDecision search = Step(0f);
                    Assert.AreEqual(WitchIntent.Search, search.Intent);
                    Assert.IsFalse(SafeZones.Contains(search.MoveTarget));
                    p.WitchPosition = search.MoveTarget;
                }
            }
            finally { SafeZones.Remove(zone); }
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidFrameTimeDoesNotExpireSearch(float dt)
        {
            BeginSearch();
            Assert.AreEqual(WitchIntent.Search, Step(dt).Intent);
            Assert.AreEqual(WitchState.Stalking, Step(19.99f).State);
            Assert.AreEqual(WitchState.TakingOff, Step(0.02f).State);
        }

        [Test]
        public void RepeatedSenseAndSearchTicksDoNotAllocate()
        {
            BeginSearch();
            var senses = new WitchSenses(new WitchSenseProfile());
            var input = new WitchSenseInputs { PlayerPosition = Vector3.right * 80f, Form = WitchForm.Grounded };
            brain.Tick(0f, senses.Sense(in input));
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) brain.Tick(0.01f, senses.Sense(in input));
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "Los datos de escena y la búsqueda no deben asignar memoria por cuadro.");
        }

        [Test]
        public void SensesCopyClearingFacingAndOnlyRecognizeTheTouchedCandleZone()
        {
            var senses = new WitchSenses(new WitchSenseProfile());
            var input = new WitchSenseInputs
            {
                HasNextClearing = true, NextClearingPosition = new Vector3(12f, 3f, 8f), PlayerForward = Vector3.left,
                HasFamilyCandle = true, FamilyCandlePosition = Vector3.right * 10f, FamilyCandleRadius = 4f,
                PlayerInSafeZone = true, PlayerPosition = Vector3.right * 11f
            };
            WitchPerception sensed = senses.Sense(in input);
            Assert.IsTrue(sensed.HasNextClearing && sensed.HasFamilyCandle && sensed.PlayerAtFamilyCandle);
            Assert.AreEqual(input.NextClearingPosition, sensed.NextClearingPosition);
            Assert.AreEqual(input.PlayerForward, sensed.PlayerForward);
            Assert.AreEqual(input.FamilyCandlePosition, sensed.FamilyCandlePosition);
            Assert.AreEqual(4f, sensed.FamilyCandleRadius);
            input.PlayerPosition = Vector3.zero;
            Assert.IsFalse(senses.Sense(in input).PlayerAtFamilyCandle);
            input.PlayerPosition = input.FamilyCandlePosition;
            input.HasFamilyCandle = false;
            Assert.IsFalse(senses.Sense(in input).PlayerAtFamilyCandle);
        }

        [Test]
        public void RespawnOnlyMarksFamilyTouchesAndKeepsTheirRealRadius()
        {
            var respawn = new RespawnPoint(Vector3.zero);
            Assert.IsFalse(respawn.HasFamilyCandle);
            respawn.Touch(new Candle(CandleKind.Kindled, Vector3.one, true), 7f);
            Assert.IsFalse(respawn.HasFamilyCandle);
            var candle = new Candle(CandleKind.Family, Vector3.right * 10f);
            var site = new CandleSite(candle, 5f);
            site.Tick(0f, false, candle.Position, null, respawn);
            Assert.IsTrue(respawn.HasFamilyCandle);
            Assert.AreEqual(candle.Position, respawn.Position);
            Assert.AreEqual(5f, respawn.FamilyCandleRadius);
        }

        [Test]
        public void CemeteryNeverStartsNewBehaviorsEvenWithAllContextAndElapsedDelays()
        {
            profile = WitchProfiles.For(Chapter.Cemetery);
            brain = new WitchBrain(profile, null);
            p.HasNextClearing = p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.NextClearingPosition = Vector3.right * 100f;
            p.SeesPlayer = p.Raining = p.HeardNoise = true;
            for (int i = 0; i < 5; i++)
            {
                WitchDecision decision = Step(80f);
                Assert.AreEqual(WitchIntent.None, decision.Intent);
                Assert.AreEqual(WitchState.Flying, decision.State);
            }
        }

        private void BeginSearch()
        {
            Ground();
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            Assert.AreEqual(WitchIntent.Search, Step(0f).Intent);
        }

        private void PrepareAmbush()
        {
            profile.SearchEnabled = false;
            profile.AmbushEnabled = profile.CanSprint = true;
            profile.AmbushMinDelay = profile.AmbushMaxDelay = 1f;
            p.HasNextClearing = true;
            p.PlayerPosition = Vector3.zero;
            p.NextClearingPosition = Vector3.right * 50f;
            p.WitchPosition = Vector3.up * 14f;
        }

        private WitchDecision BeginTargetedLanding(WitchIntent intent, System.Func<Vector3, Vector3> resolve)
        {
            if (intent == WitchIntent.Ambush) PrepareAmbush();
            else
            {
                profile.LastSeenEnabled = true;
                p.WitchPosition = Vector3.up * 14f;
            }
            brain = new WitchBrain(profile, null, new FixedRandom(0.5), resolve);
            if (intent == WitchIntent.LastSeen)
            {
                p.SeesPlayerLight = true;
                p.PlayerLightPosition = Vector3.right * 20f;
                Step(0f);
                p.SeesPlayerLight = false;
                p.PlayerLightPosition = p.PlayerPosition = Vector3.right * 80f;
            }
            p.WitchPosition = Step(intent == WitchIntent.Ambush ? 1f : 0f).MoveTarget;
            WitchDecision landing = Step(0f);
            Assert.AreEqual(WitchState.Landing, landing.State);
            return landing;
        }

        private void BeginCandleWatch(float safeRadius)
        {
            profile.CandleWatchEnabled = true;
            p.WitchPosition = Vector3.left * (safeRadius + 12f);
            p.PlayerPosition = Vector3.left * (safeRadius + 2f);
            Ground();
            p.SeesPlayer = true;
            Step(0f);
            p.SeesPlayer = false;
            p.HasFamilyCandle = p.PlayerAtFamilyCandle = p.PlayerInSafeZone = true;
            p.FamilyCandleRadius = safeRadius;
            p.FamilyCandlePosition = p.PlayerPosition = Vector3.zero;
            Assert.AreEqual(WitchIntent.CandleWatch, Step(0f).Intent);
        }

        private void PrepareBark(double random)
        {
            profile.SearchEnabled = false;
            profile.BarkLureEnabled = true;
            profile.BarkLureMinDelay = profile.BarkLureMaxDelay = 0f;
            brain = new WitchBrain(profile, null, new FixedRandom(random));
            Ground();
        }

        private void Ground()
        {
            p.Raining = true;
            Step(0f);
            Step(profile.LandingDuration);
            p.Raining = false;
        }

        private WitchDecision Step(float dt)
        {
            p.DistanceToPlayer = Distance(p.WitchPosition, p.PlayerPosition);
            return brain.Tick(dt, in p);
        }

        private static float Distance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        private sealed class FixedRandom : System.Random
        {
            private readonly double value;
            public FixedRandom(double value) => this.value = value;
            public override double NextDouble() => value;
        }
    }
}
