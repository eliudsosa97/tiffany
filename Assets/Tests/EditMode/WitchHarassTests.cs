using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchHarassTests
    {
        private WitchProfile profile;
        private WitchPerception p;
        private WitchBrain brain;

        [SetUp]
        public void SetUp()
        {
            profile = new WitchProfile();
            p = new WitchPerception { WitchPosition = Vector3.right * 12f };
            brain = new WitchBrain(profile, null, new FixedRandom(0.5));
            p.Raining = true;
            Tick(0f);
            Tick(profile.LandingDuration);
            p.Raining = false;
            p.SeesPlayer = true;
        }

        [TestCase(0d, 1.5f)]
        [TestCase(0.5d, 2f)]
        [TestCase(1d, 2.5f)]
        public void LooksAtPlayerForInjectedDurationBeforeMoving(double sample, float duration)
        {
            var harass = new WitchHarass(new FixedRandom(sample));
            Measure();
            Assert.IsTrue(harass.Tick(0f, profile, in p, out var looking));
            Assert.AreEqual(WitchIntent.Harass, looking.Intent);
            Assert.AreEqual(WitchState.Stalking, looking.State);
            Assert.AreEqual(p.PlayerPosition, looking.MoveTarget);
            Assert.AreEqual(0f, looking.Speed);
            harass.Tick(duration - 0.01f, profile, in p, out looking);
            Assert.AreEqual(0f, looking.Speed);
            harass.Tick(0.02f, profile, in p, out var circling);
            Assert.AreEqual(0.9f, circling.Speed);
        }

        [TestCase(0d, 90f, 10f)]
        [TestCase(0.5d, 135f, 12.5f)]
        [TestCase(1d, 180f, 15f)]
        public void ArcUsesInjectedAngleRadiusAndResolvedGround(double sample, float angle, float radius)
        {
            var harass = new WitchHarass(new FixedRandom(sample), point => new Vector3(point.x, 2f, point.z));
            Measure();
            harass.Tick(0f, profile, in p, out _);
            Assert.IsTrue(harass.Tick(profile.HarassMaxLookDuration, profile, in p, out var point));
            Vector3 initial = Vector3.right;
            float total = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = point.MoveTarget - p.PlayerPosition;
                direction.y = 0f;
                Assert.AreEqual(radius, direction.magnitude, 1e-4f);
                Assert.AreEqual(2f, point.MoveTarget.y, "Cada destino pasa por el resolvedor de suelo.");
                total += Vector3.Angle(initial, direction);
                initial = direction;
                p.WitchPosition = point.MoveTarget;
                if (!harass.Tick(0f, profile, in p, out point)) break;
            }
            Assert.AreEqual(angle, total, 1e-3f);
        }

        [Test]
        public void CloseInitialDetectionRetreatsToEightMetersBeforeLooking()
        {
            p.WitchPosition = Vector3.right * 5f;
            WitchDecision retreat = Tick(0f);
            Assert.AreEqual(WitchIntent.Harass, retreat.Intent);
            Assert.AreEqual(Vector3.right * 8f, retreat.MoveTarget);
            Assert.AreEqual(profile.StalkSpeed, retreat.Speed);
            p.WitchPosition = retreat.MoveTarget;
            Assert.AreEqual(0f, Tick(1f).Speed);
            Assert.AreEqual(0f, Tick(1.99f).Speed, "La mirada empieza al terminar la retirada.");
            Assert.Greater(Tick(0.02f).Speed, 0f);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void LightSendsHerToCoverOrTheNextArcPointThenHoldsForTwoSeconds(bool cover)
        {
            Tick(0f);
            WitchDecision circle = Tick(2f);
            p.WitchPosition = circle.MoveTarget;
            p.HasCover = cover;
            p.CoverPosition = p.WitchPosition + Vector3.back * 2f;
            p.PlayerLightTouchesWitch = true;
            WitchDecision hiding = Tick(0f);
            Assert.AreEqual(WitchIntent.Harass, hiding.Intent);
            Assert.AreEqual(profile.StalkSpeed, hiding.Speed);
            if (cover) Assert.AreEqual(p.CoverPosition, hiding.MoveTarget);
            else Assert.AreNotEqual(circle.MoveTarget, hiding.MoveTarget);
            p.WitchPosition = hiding.MoveTarget;
            Assert.AreEqual(0f, Tick(0f).Speed);
            p.SeesPlayer = p.PlayerLightTouchesWitch = false;
            Assert.AreEqual(0f, Tick(1.99f).Speed, "La cobertura conserva la pausa aunque se corte la vista.");
            Assert.AreEqual(profile.StalkSpeed, Tick(0.02f).Speed);
        }

        [Test]
        public void PerceivedLightAlsoTriggersCoverWithoutTheConeFlag()
        {
            Tick(0f);
            Tick(2f);
            p.HasCover = p.SeesPlayerLight = true;
            p.CoverPosition = p.WitchPosition + Vector3.back * 2f;
            Assert.AreEqual(p.CoverPosition, Tick(0f).MoveTarget);
        }

        [Test]
        public void CompletingTheArcAlwaysStartsAFullWarningBeforeSprint()
        {
            Tick(0f);
            WitchDecision decision = Tick(2f);
            for (int i = 0; i < 8 && decision.Intent == WitchIntent.Harass; i++)
            {
                p.WitchPosition = decision.MoveTarget;
                decision = Tick(0f);
            }
            Assert.AreEqual(WitchState.WindUp, decision.State);
            Assert.AreEqual(WitchState.WindUp, Tick(profile.WindUpDuration - 0.01f).State);
            Assert.AreEqual(WitchState.Sprinting, Tick(0.02f).State);
        }

        [TestCase(5.99f, WitchState.WindUp)]
        [TestCase(6f, WitchState.Stalking)]
        public void ApproachingInsideSixMetersInterruptsTheHarassment(float distance, WitchState expected)
        {
            Tick(0f);
            p.PlayerPosition = p.WitchPosition + Vector3.forward * distance;
            Assert.AreEqual(expected, Tick(0.1f).State);
        }

        [Test]
        public void TimeoutAtTwelveSecondsWarnsEvenIfRetreatOrArcIsBlocked()
        {
            p.WitchPosition = Vector3.right * 5f;
            Tick(0f);
            Assert.AreEqual(WitchIntent.Harass, Tick(11.99f).Intent);
            Assert.AreEqual(WitchState.WindUp, Tick(0.02f).State);
            Assert.AreEqual(WitchState.WindUp, Tick(0f).State, "Ni el límite ni la cercanía omiten el aviso.");
        }

        [TestCase(19.99f, WitchState.WindUp)]
        [TestCase(20f, WitchState.Stalking)]
        public void CompletedWarningSkipsHarassmentForTwentySeconds(float elapsed, WitchState expected)
        {
            Tick(0f);
            Tick(profile.HarassTimeLimit);
            Tick(profile.WindUpDuration);
            p.PlayerPosition = Vector3.left * 50f;
            Tick(profile.SprintDuration);
            Tick(profile.RecoverDuration);
            p.PlayerVanished = true;
            Tick(0f);
            p.Raining = true;
            Tick(elapsed - profile.SprintDuration - profile.RecoverDuration);
            p.PlayerVanished = false;
            p.PlayerPosition = p.WitchPosition + Vector3.right * 12f;
            Assert.AreEqual(expected, Tick(0f).State, "El enfriamiento se cuenta desde el aviso cumplido.");
        }

        [Test]
        public void InterruptedWarningDoesNotStartTheCooldown()
        {
            var harass = new WitchHarass(new FixedRandom(0.5));
            Measure();
            harass.Tick(0f, profile, in p, out _);
            Assert.IsFalse(harass.Tick(12f, profile, in p, out _));
            harass.Cancel();
            harass.AdvanceClock(30f);
            Assert.IsFalse(harass.Tick(0f, profile, in p, out _), "El aviso pendiente debe cumplirse aunque se haya interrumpido.");
            harass.CompleteWarning(profile);
            harass.AdvanceClock(20f);
            Assert.IsTrue(harass.Tick(0f, profile, in p, out _));
        }

        [Test]
        public void ReturnFlightAndWatchKeepTheirExistingDecisions()
        {
            brain = new WitchBrain(WitchProfiles.For(Chapter.Return), null);
            p.WitchPosition = Vector3.right * 8f;
            Assert.AreEqual(WitchState.WindUp, Tick(0f).State);
            brain = new WitchBrain(profile, null);
            Assert.AreNotEqual(WitchIntent.Harass, Tick(0f).Intent);
            brain.BeginWatch(p.WitchPosition, false);
            p.InWatchPassage = true;
            Assert.AreNotEqual(WitchIntent.Harass, Tick(0f).Intent);
        }

        [Test]
        public void ChaptersWithoutSprintStillHarassAndWarnBeforeWalking()
        {
            profile.CanSprint = false;
            Assert.AreEqual(WitchIntent.Harass, Tick(0f).Intent);
            Assert.AreEqual(WitchState.WindUp, Tick(12f).State);
            WitchDecision walking = Tick(profile.WindUpDuration);
            Assert.AreEqual(WitchState.Stalking, walking.State);
            Assert.AreEqual(profile.StalkSpeed, walking.Speed);
        }

        [Test]
        public void UnreachableArcWaitsForTheLimitInsteadOfSkippingStraightToTheAttack()
        {
            var harass = new WitchHarass(new FixedRandom(0.5), _ => Vector3.positiveInfinity);
            Measure();
            harass.Tick(0f, profile, in p, out _);
            Assert.IsTrue(harass.Tick(2.5f, profile, in p, out var waiting));
            Assert.AreEqual(0f, waiting.Speed);
            Assert.IsTrue(harass.Tick(9.49f, profile, in p, out _));
            Assert.IsFalse(harass.Tick(0.02f, profile, in p, out _));
        }

        [Test]
        public void RepeatedHarassTicksDoNotAllocate()
        {
            Tick(0f);
            Tick(2f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) Tick(0.001f);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "Mirar y rodear no asigna memoria por cuadro.");
        }

        private WitchDecision Tick(float dt)
        {
            Measure();
            return brain.Tick(dt, in p);
        }

        private void Measure() => p.DistanceToPlayer = WitchTargetsDistance(p.WitchPosition, p.PlayerPosition);

        private static float WitchTargetsDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }

        private sealed class FixedRandom : System.Random
        {
            private readonly double value;
            public FixedRandom(double value) => this.value = value;
            public override double NextDouble() => value;
        }
    }
}
