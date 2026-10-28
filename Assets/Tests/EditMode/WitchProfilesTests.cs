using System;
using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class WitchProfilesTests
    {
        [TestCase(Chapter.Cemetery, false, false, false, false, 10f)]
        [TestCase(Chapter.WaterPlant, true, false, false, false, 0f)]
        [TestCase(Chapter.TrainTracks, true, false, true, false, 0f)]
        [TestCase(Chapter.Stable, true, true, true, false, 0f)]
        [TestCase(Chapter.WitchHut, true, true, true, false, 0f)]
        [TestCase(Chapter.Return, true, true, false, true, 0f)]
        public void ForSetsChapterFlagsAndPreservesNumericDefaults(
            Chapter chapter, bool canLand, bool canSprint, bool usesVoiceLure, bool groundChase, float flightHeightOffset)
        {
            WitchProfile profile = WitchProfiles.For(chapter);
            var defaults = new WitchProfile();

            Assert.AreEqual(canLand, profile.CanLand);
            Assert.AreEqual(canSprint, profile.CanSprint);
            Assert.AreEqual(usesVoiceLure, profile.UsesVoiceLure);
            Assert.AreEqual(groundChase, profile.GroundChase);
            Assert.AreEqual(flightHeightOffset, profile.FlightHeightOffset);
            Assert.AreEqual(defaults.FlySpeed, profile.FlySpeed);
            Assert.AreEqual(defaults.StalkSpeed, profile.StalkSpeed);
            Assert.AreEqual(defaults.FollowSpeed, profile.FollowSpeed);
            Assert.AreEqual(defaults.SprintSpeed, profile.SprintSpeed);
            Assert.AreEqual(defaults.SprintDuration, profile.SprintDuration);
            Assert.AreEqual(defaults.WindUpDuration, profile.WindUpDuration);
            Assert.AreEqual(defaults.RecoverDuration, profile.RecoverDuration);
            Assert.AreEqual(defaults.SprintTriggerDistance, profile.SprintTriggerDistance);
            Assert.AreEqual(defaults.CatchDistance, profile.CatchDistance);
            Assert.AreEqual(defaults.StunDuration, profile.StunDuration);
            Assert.AreEqual(defaults.LoseTrackTime, profile.LoseTrackTime);
            Assert.AreEqual(defaults.LandingDuration, profile.LandingDuration);
            Assert.AreEqual(defaults.TakeOffDuration, profile.TakeOffDuration);
            Assert.AreEqual(defaults.LandingDistance, profile.LandingDistance);
            Assert.AreEqual(defaults.ArriveDistance, profile.ArriveDistance);
            Assert.AreEqual(defaults.PlayerNoiseMatch, profile.PlayerNoiseMatch);
        }

        [Test]
        public void ForReturnsIndependentProfiles([Values] Chapter chapter)
        {
            WitchProfile first = WitchProfiles.For(chapter);
            WitchProfile second = WitchProfiles.For(chapter);
            float sprintSpeed = second.SprintSpeed;
            bool canLand = second.CanLand;

            Assert.AreNotSame(first, second);
            first.SprintSpeed += 1f;
            first.CanLand = !first.CanLand;
            Assert.AreEqual(sprintSpeed, second.SprintSpeed);
            Assert.AreEqual(canLand, second.CanLand);
        }

        [Test]
        public void ForRejectsUnknownChapter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WitchProfiles.For((Chapter)99));
        }
    }
}
