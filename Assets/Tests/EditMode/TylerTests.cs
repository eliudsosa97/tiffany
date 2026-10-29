using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class TylerTests
    {
        private Tyler tyler;
        private TylerInputs inputs;

        [SetUp]
        public void SetUp()
        {
            tyler = new Tyler();
            inputs = TylerInputs.None;
        }

        [Test]
        public void StillnessGuidesUntilMovementRestartsTheDelay()
        {
            Assert.AreEqual(TylerReaction.None, tyler.Reaction);
            inputs.PlayerStill = true;
            Check(2.9f, TylerReaction.None);
            Check(0.2f, TylerReaction.Guide);
            Check(5f, TylerReaction.Guide);
            inputs.PlayerStill = false;
            Check(0f, TylerReaction.None);
            inputs.PlayerStill = true;
            Check(2.9f, TylerReaction.None);
            Check(0.2f, TylerReaction.Guide);
        }

        [TestCase(WitchForm.Grounded, 0f, TylerReaction.Growl)]
        [TestCase(WitchForm.Grounded, 19.9f, TylerReaction.Growl)]
        [TestCase(WitchForm.Grounded, 20f, TylerReaction.None)]
        [TestCase(WitchForm.Grounded, 20.1f, TylerReaction.None)]
        [TestCase(WitchForm.Flying, 0f, TylerReaction.Whine)]
        [TestCase(WitchForm.Flying, 29.9f, TylerReaction.Whine)]
        [TestCase(WitchForm.Flying, 30f, TylerReaction.None)]
        [TestCase(WitchForm.Flying, 30.1f, TylerReaction.None)]
        [TestCase(WitchForm.Flying, 10f, TylerReaction.Whine)]
        [TestCase(WitchForm.Grounded, -1f, TylerReaction.None)]
        [TestCase(WitchForm.Grounded, float.PositiveInfinity, TylerReaction.None)]
        [TestCase(WitchForm.Grounded, float.NegativeInfinity, TylerReaction.None)]
        [TestCase(WitchForm.Grounded, float.NaN, TylerReaction.None)]
        [TestCase(WitchForm.Flying, -1f, TylerReaction.None)]
        [TestCase(WitchForm.Flying, float.PositiveInfinity, TylerReaction.None)]
        [TestCase(WitchForm.Flying, float.NegativeInfinity, TylerReaction.None)]
        [TestCase(WitchForm.Flying, float.NaN, TylerReaction.None)]
        public void WitchReactionDependsOnFormAndValidDistance(WitchForm form, float distance, TylerReaction expected)
        {
            inputs.WitchForm = form;
            inputs.WitchDistance = distance;
            Check(0f, expected);
        }

        [TestCase(WitchForm.Grounded, TylerReaction.Growl)]
        [TestCase(WitchForm.Flying, TylerReaction.Whine)]
        public void WitchOverridesGuideWhileStillnessKeepsCounting(WitchForm form, TylerReaction expected)
        {
            inputs.PlayerStill = true;
            inputs.WitchForm = form;
            inputs.WitchDistance = 5f;
            Check(5f, expected);
            inputs.WitchDistance = float.PositiveInfinity;
            Check(0f, TylerReaction.Guide);
            inputs.WitchDistance = 5f;
            inputs.PlayerStill = false;
            Check(0f, expected);
            inputs.WitchDistance = float.PositiveInfinity;
            inputs.PlayerStill = true;
            Check(2.9f, TylerReaction.None);
        }

        [TestCase(WitchForm.Grounded)]
        [TestCase(WitchForm.Flying)]
        public void NoneDoesNotGrowlOrWhineAndGuidesAfterStillness(WitchForm form)
        {
            inputs = TylerInputs.None;
            Assert.AreEqual(float.PositiveInfinity, inputs.WitchDistance);
            inputs.WitchForm = form;
            Check(5f, TylerReaction.None);
            inputs.PlayerStill = true;
            Check(3.1f, TylerReaction.Guide);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(0f)]
        public void InvalidOrZeroTimePreservesStillnessAndStillAllowsMovementReset(float dt)
        {
            inputs.PlayerStill = true;
            Check(2.9f, TylerReaction.None);
            Check(dt, TylerReaction.None);
            Check(0.2f, TylerReaction.Guide);
            inputs.PlayerStill = false;
            Check(dt, TylerReaction.None);
            inputs.PlayerStill = true;
            Check(2.9f, TylerReaction.None);
        }

        [Test]
        public void MimicGrowlExpiresAndCannotRetriggerBeforeMinimumInterval()
        {
            inputs.WitchDistance = 25f;
            inputs.WitchForm = WitchForm.Grounded;
            Check(0f, TylerReaction.None);
            inputs.HeardMimic = true;
            Check(0f, TylerReaction.Growl);
            Check(Tyler.MimicDuration * 0.5f, TylerReaction.Growl);
            Check(Tyler.MimicDuration * 0.5f, TylerReaction.None);
            Check(Tyler.MimicInterval - Tyler.MimicDuration - 0.25f, TylerReaction.None);
            Check(0.25f, TylerReaction.Growl);
            inputs.HeardMimic = false;
            Check(Tyler.MimicDuration, TylerReaction.None);
        }

        [TestCase(30f)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NaN)]
        public void DistantOrMissingMimicDoesNotGrowl(float distance)
        {
            inputs.WitchDistance = distance;
            inputs.HeardMimic = true;
            Check(0f, TylerReaction.None);
        }

        [Test]
        public void ForestGrowlNeedsNoWitchAndCannotRetriggerBeforeMinimumInterval()
        {
            inputs.HeardForest = true;
            Check(0f, TylerReaction.Growl);
            Assert.IsTrue(tyler.GrowlingAtForest);
            Check(Tyler.MimicDuration * 0.5f, TylerReaction.Growl);
            Check(Tyler.MimicDuration * 0.5f, TylerReaction.None);
            Check(Tyler.MimicInterval - Tyler.MimicDuration - 0.25f, TylerReaction.None);
            Check(0.25f, TylerReaction.Growl);
            inputs.HeardForest = false;
            Check(Tyler.MimicDuration, TylerReaction.None);
        }

        [Test]
        public void ForestCannotRedirectMimicGrowlDuringCooldown()
        {
            inputs.WitchDistance = 25f;
            inputs.HeardMimic = true;
            Check(0f, TylerReaction.Growl);
            inputs.HeardMimic = false;
            inputs.HeardForest = true;
            Check(Tyler.MimicDuration * 0.5f, TylerReaction.Growl);
            Assert.IsFalse(tyler.GrowlingAtForest);
        }

        private void Check(float dt, TylerReaction expected)
        {
            Assert.AreEqual(expected, tyler.Tick(dt, in inputs));
            Assert.AreEqual(expected, tyler.Reaction);
        }
    }
}
