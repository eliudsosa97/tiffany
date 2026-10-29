using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class StepGuaranteeTests
    {
        const float Dt = 1f / 60f;

        [Test]
        public void TenthSecondTapsEverySixTenthsReturnToIdleAndPublishStepsOverTenMeters()
        {
            int steps = Travel(10f, Vector2.up, true, out int idleEntries, out int contacts);
            Assert.Greater(idleEntries, 40);
            Assert.AreEqual(0, contacts);
            Assert.That(steps, Is.InRange(17, 25));
        }

        [Test]
        public void StickAtNineteenPercentStaysIdleAndPublishesStepsOverFiveMeters()
        {
            var input = StickResponse.Shape(Vector2.up * 0.19f);
            float speed = PlayerMotion.TargetSpeed(input.magnitude, Gait.Walk);
            Assert.That(speed, Is.InRange(0.07f, TiffanyAnimation.MoveSpeed));
            Assert.AreEqual(TiffanyAnimation.Idle, TiffanyAnimation.StateFor(false, false, false, speed, 0f, false));
            int steps = Travel(5f, input, false, out _, out int contacts);
            Assert.AreEqual(0, contacts);
            Assert.That(steps, Is.InRange(11, 12));
        }

        static int Travel(float meters, Vector2 input, bool tapping, out int idleEntries, out int contactCount)
        {
            var motion = new PlayerMotion.VelocityState();
            var contacts = new FootContacts();
            var guarantee = new StepGuarantee();
            float distance = 0f, phase = 0f;
            bool wasWalking = false;
            int steps = 0;
            idleEntries = contactCount = 0;
            for (int frame = 0; distance < meters && frame < 10000; frame++)
            {
                float target = !tapping || frame % 36 < 6 ? PlayerMotion.TargetSpeed(input.magnitude, Gait.Walk) : 0f;
                float speed = motion.Advance(Vector3.forward * target, Dt).magnitude;
                float moved = speed * Dt;
                var gait = PlayerMotion.NoiseGait(speed);
                bool walking = TiffanyAnimation.StateFor(false, false, false, speed, 0f, false) == TiffanyAnimation.Walk;
                int count = 0;
                if (walking)
                {
                    phase += moved / (2f * StepGuarantee.StepLength(gait));
                    count = contacts.Advance(1, phase, new Vector2(0f, 0.5f));
                }
                else
                {
                    if (wasWalking) idleEntries++;
                    contacts = default;
                    phase = 0f;
                }
                wasWalking = walking;
                distance += moved;
                contactCount += count;
                steps += guarantee.Advance(moved, gait, count);
            }
            Assert.GreaterOrEqual(distance, meters);
            return steps;
        }

        [TestCase(Gait.Slow)]
        [TestCase(Gait.Walk)]
        [TestCase(Gait.Run)]
        public void DistanceStepSuppressesCloseContactAndEveryPublishedStepResetsDistance(Gait gait)
        {
            var guarantee = new StepGuarantee();
            float length = StepGuarantee.StepLength(gait);
            float limit = length + StepGuarantee.DistanceMargin;
            Assert.AreEqual(0, guarantee.Advance(limit - 0.001f, gait, 0));
            Assert.AreEqual(1, guarantee.Advance(0.002f, gait, 0));
            Assert.AreEqual(0, guarantee.Advance(0f, gait, 1));
            Assert.AreEqual(0, guarantee.Advance(length * 0.25f - 0.001f, gait, 1));
            Assert.AreEqual(1, guarantee.Advance(0.002f, gait, 1));
            Assert.AreEqual(0, guarantee.Advance(limit - 0.001f, gait, 0));
            Assert.AreEqual(1, guarantee.Advance(0.002f, gait, 0));
        }

        [Test]
        public void ContactsWinOverDistanceAndTurningContactsStillPublishWithoutTravel()
        {
            var guarantee = new StepGuarantee();
            Assert.AreEqual(0, guarantee.Advance(0.2f, Gait.Walk, 0));
            Assert.AreEqual(1, guarantee.Advance(0f, Gait.Walk, 1));
            Assert.AreEqual(1, guarantee.Advance(StepGuarantee.StepLength(Gait.Walk) + StepGuarantee.DistanceMargin, Gait.Walk, 1));
            Assert.AreEqual(1, guarantee.Advance(0f, Gait.Slow, 1));
            Assert.AreEqual(0, guarantee.Advance(0f, Gait.Slow, 0));
        }

        [TestCase(Gait.Slow)]
        [TestCase(Gait.Walk)]
        [TestCase(Gait.Run)]
        public void StepLengthsMatchHalfTheAuthoredCycle(Gait gait)
        {
            var spec = ModelLibrary.LoadSpec("tiffany-provisional");
            var clip = System.Array.Find(spec.clips, entry => entry.name == gait.ToString());
            Assert.AreEqual(clip.speed * clip.frames / spec.fps * 0.5f, StepGuarantee.StepLength(gait), 1e-6f);
        }
    }
}
