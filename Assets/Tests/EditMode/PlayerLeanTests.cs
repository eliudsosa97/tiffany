using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class PlayerLeanTests
    {
        [TestCase(-720f, 10f)]
        [TestCase(720f, -10f)]
        public void RunningLeansIntoTurnAndNeverExceedsTenDegrees(float angularSpeed, float expected)
        {
            var lean = new PlayerLean();
            for (int i = 0; i < 120; i++)
                Assert.That(lean.Advance(4f, angularSpeed, 1f / 60f), Is.InRange(-10f, 10f));
            Assert.AreEqual(expected, lean.Roll, 0.01f);
            for (int i = 0; i < 120; i++)
                Assert.That(lean.Advance(4f, -angularSpeed, 1f / 60f), Is.InRange(-10f, 10f));
        }

        [Test]
        public void LeanScalesWithSpeedAndAngularSpeedAndEasesIn()
        {
            var slowTurn = new PlayerLean();
            var fastTurn = new PlayerLean();
            var slowerRun = new PlayerLean();
            Assert.That(fastTurn.Advance(4f, 360f, 1f / 60f), Is.InRange(-1f, 0f));
            for (int i = 0; i < 120; i++)
            {
                slowTurn.Advance(4f, 180f, 1f / 60f);
                fastTurn.Advance(4f, 360f, 1f / 60f);
                slowerRun.Advance(3f, 360f, 1f / 60f);
            }
            Assert.AreEqual(fastTurn.Roll * 0.5f, slowTurn.Roll, 0.01f);
            Assert.Less(Mathf.Abs(slowerRun.Roll), Mathf.Abs(fastTurn.Roll));
        }

        [TestCase(0f, 720f)]
        [TestCase(2.2f, 720f)]
        [TestCase(4f, 0f)]
        public void StandingWalkingAndRunningStraightReturnUpright(float speed, float turn)
        {
            var lean = new PlayerLean();
            for (int i = 0; i < 60; i++) lean.Advance(4f, 360f, 1f / 60f);
            for (int i = 0; i < 60; i++) lean.Advance(speed, turn, 1f / 60f);
            Assert.AreEqual(0f, lean.Roll, 0.01f);
        }
    }
}
