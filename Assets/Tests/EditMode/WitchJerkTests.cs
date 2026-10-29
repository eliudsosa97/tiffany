using System.Collections.Generic;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class WitchJerkTests
    {
        private const float Dt = 1f / 60f;
        private static readonly WitchJerkPhase[] Cycle = { WitchJerkPhase.Lunge, WitchJerkPhase.Still, WitchJerkPhase.Tremble };

        private static WitchDecision Ground(WitchState state, float speed = 1.2f) =>
            new WitchDecision { State = state, Form = WitchForm.Grounded, Speed = speed };

        [TestCase(1)] [TestCase(7)] [TestCase(42)] [TestCase(2024)]
        public void StalkingCyclesWithDurationsInRangeAndKeepsMeanSpeed(int seed)
        {
            var jerk = new WitchJerk(seed);
            var phases = new List<WitchJerkPhase>();
            var durations = new List<float>();
            float time = 0f, distance = 0f;
            for (int i = 0; i < 3600; i++)
            {
                jerk.Tick(Dt, Ground(WitchState.Stalking));
                distance += jerk.SpeedFactor * Dt;
                time += Dt;
                if (jerk.Phase == WitchJerkPhase.Lunge)
                    Assert.AreEqual((true, WitchJerk.LungeAnimator, 0f), (jerk.SpeedFactor > 2f, jerk.AnimatorSpeed, jerk.Tremble));
                else Assert.AreEqual(0f, jerk.SpeedFactor);
                if (jerk.Phase == WitchJerkPhase.Tremble) Assert.That(jerk.Tremble, Is.InRange(0.01f, 1f));
                if (!jerk.PhaseStarted) continue;
                if (phases.Count > 0) durations.Add(time);
                phases.Add(jerk.Phase);
                time = 0f;
            }
            Assert.That(distance / 60f, Is.InRange(0.95f, 1.05f));
            for (int i = 0; i < phases.Count; i++) Assert.AreEqual(Cycle[i % 3], phases[i]);
            for (int i = 0; i < durations.Count; i++)
            {
                var (min, max) = phases[i] switch
                {
                    WitchJerkPhase.Lunge => (WitchJerk.LungeMin, WitchJerk.LungeMax),
                    WitchJerkPhase.Still => (WitchJerk.StillMin, WitchJerk.StillMax),
                    _ => (WitchJerk.TrembleMin, WitchJerk.TrembleMax)
                };
                Assert.That(durations[i], Is.InRange(min - Dt * 1.5f, max + Dt * 1.5f), $"{phases[i]} #{i}");
            }
        }

        [TestCase(WitchState.Flying)] [TestCase(WitchState.Landing)] [TestCase(WitchState.WindUp)]
        [TestCase(WitchState.Sprinting)] [TestCase(WitchState.Recovering)] [TestCase(WitchState.Stunned)]
        [TestCase(WitchState.Caught)] [TestCase(WitchState.TakingOff)] [TestCase(WitchState.Suspicious)]
        public void OtherStatesAreNeutralAndRestartTheCycle(WitchState state)
        {
            var jerk = new WitchJerk(3);
            for (int i = 0; i < 60; i++) jerk.Tick(Dt, Ground(WitchState.Stalking));
            Assert.AreNotEqual(WitchJerkPhase.Lunge, jerk.Phase);
            jerk.Tick(Dt, new WitchDecision { State = state, Form = WitchForm.Flying, Speed = 9f });
            Assert.AreEqual((WitchJerkPhase.Off, 1f, 1f, 0f), (jerk.Phase, jerk.SpeedFactor, jerk.AnimatorSpeed, jerk.Tremble));
            jerk.Tick(Dt, Ground(WitchState.Stalking));
            Assert.AreEqual((WitchJerkPhase.Lunge, true), (jerk.Phase, jerk.PhaseStarted));
        }

        [TestCase(WitchState.Suspicious, 0f)] [TestCase(WitchState.Stalking, 0f)] [TestCase(WitchState.Suspicious, 1.2f)]
        public void StandingStillTremblesNowAndThenWithoutMoving(WitchState state, float speed)
        {
            var jerk = new WitchJerk(5);
            for (int i = 0; i < 60; i++) jerk.Tick(Dt, Ground(WitchState.Stalking));
            int trembles = 0;
            for (int i = 0; i < 1200; i++)
            {
                jerk.Tick(Dt, Ground(state, speed));
                if (state == WitchState.Suspicious) Assert.AreEqual(1f, jerk.AnimatorSpeed);
                if (jerk.Phase == WitchJerkPhase.Tremble)
                {
                    Assert.AreEqual(0f, jerk.SpeedFactor);
                    Assert.That(jerk.Duration, Is.InRange(WitchJerk.IdleTrembleMin, WitchJerk.IdleTrembleMax));
                    if (jerk.PhaseStarted) trembles++;
                }
                else Assert.AreEqual((WitchJerkPhase.Off, 1f, 1f, 0f), (jerk.Phase, jerk.SpeedFactor, jerk.AnimatorSpeed, jerk.Tremble));
            }
            Assert.That(trembles, Is.InRange(3, 10));
            jerk.Tick(Dt, Ground(WitchState.Stalking));
            Assert.AreEqual(WitchJerkPhase.Lunge, jerk.Phase);
        }

        [Test]
        public void SuspicionSuppressesAnActiveTrembleAndKeepsAnimatorAtNormalSpeed()
        {
            var jerk = new WitchJerk(5);
            var stalking = Ground(WitchState.Stalking, 0f);
            jerk.Tick(Dt, stalking);
            jerk.Tick(jerk.Duration, stalking);
            jerk.Tick(jerk.Duration * 0.5f, stalking);
            Assert.AreEqual(WitchJerkPhase.Tremble, jerk.Phase);
            Assert.Greater(jerk.Tremble, 0f);

            var suspicious = Ground(WitchState.Suspicious, 0f);
            for (int i = 0; i < 180; i++)
            {
                jerk.Tick(Dt, suspicious);
                Assert.AreEqual(0f, jerk.Tremble);
                Assert.AreEqual(1f, jerk.AnimatorSpeed);
            }
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotAdvance(float dt)
        {
            var jerk = new WitchJerk(11);
            jerk.Tick(Dt, Ground(WitchState.Stalking));
            float duration = jerk.Duration;
            for (int i = 0; i < 100; i++) jerk.Tick(dt, Ground(WitchState.Stalking));
            jerk.Tick(duration - Dt * 2f, Ground(WitchState.Stalking));
            Assert.AreEqual((WitchJerkPhase.Lunge, duration), (jerk.Phase, jerk.Duration));
            jerk.Tick(Dt * 3f, Ground(WitchState.Stalking));
            Assert.AreEqual(WitchJerkPhase.Still, jerk.Phase);
        }
    }
}
