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
                Assert.That(lean.Advance(PlayerMotion.RunSpeed, angularSpeed, 1f / 60f), Is.InRange(-10f, 10f));
            Assert.AreEqual(expected, lean.Roll, 0.01f);
            for (int i = 0; i < 120; i++)
                Assert.That(lean.Advance(PlayerMotion.RunSpeed, -angularSpeed, 1f / 60f), Is.InRange(-10f, 10f));
        }

        [Test]
        public void LeanScalesWithSpeedAndAngularSpeedAndEasesIn()
        {
            var slowTurn = new PlayerLean();
            var fastTurn = new PlayerLean();
            var slowerRun = new PlayerLean();
            Assert.That(fastTurn.Advance(PlayerMotion.RunSpeed, 360f, 1f / 60f), Is.InRange(-1f, 0f));
            for (int i = 0; i < 120; i++)
            {
                slowTurn.Advance(PlayerMotion.RunSpeed, 180f, 1f / 60f);
                fastTurn.Advance(PlayerMotion.RunSpeed, 360f, 1f / 60f);
                slowerRun.Advance((PlayerMotion.WalkSpeed + PlayerMotion.RunSpeed) * 0.5f, 360f, 1f / 60f);
            }
            Assert.AreEqual(fastTurn.Roll * 0.5f, slowTurn.Roll, 0.01f);
            Assert.Less(Mathf.Abs(slowerRun.Roll), Mathf.Abs(fastTurn.Roll));
        }

        [TestCase(0f, 720f)]
        [TestCase(PlayerMotion.WalkSpeed, 720f)]
        [TestCase(PlayerMotion.RunSpeed, 0f)]
        public void StandingWalkingAndRunningStraightReturnUpright(float speed, float turn)
        {
            var lean = new PlayerLean();
            for (int i = 0; i < 60; i++) lean.Advance(PlayerMotion.RunSpeed, 360f, 1f / 60f);
            for (int i = 0; i < 60; i++) lean.Advance(speed, turn, 1f / 60f);
            Assert.AreEqual(0f, lean.Roll, 0.01f);
        }
    }
}
