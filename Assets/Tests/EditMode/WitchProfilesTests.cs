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
            Assert.AreEqual(0.9f, profile.StalkSpeed, "El acecho deliberado baja a 0.9 m/s sin cambiar seguimiento ni embestida.");
            Assert.AreEqual(2.5f, profile.FollowSpeed);
            Assert.AreEqual(9f, profile.SprintSpeed);
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

        [TestCase(Chapter.Cemetery, 45f, 60f)]
        [TestCase(Chapter.WaterPlant, 45f, 60f)]
        [TestCase(Chapter.TrainTracks, 45f, 60f)]
        [TestCase(Chapter.Stable, 25f, 35f)]
        [TestCase(Chapter.WitchHut, 25f, 35f)]
        [TestCase(Chapter.Return, 45f, 60f)]
        public void RoamDelayShortensOnlyInStableAndWitchHut(Chapter chapter, float minimum, float maximum)
        {
            WitchProfile profile = WitchProfiles.For(chapter);
            Assert.AreEqual(minimum, profile.RoamMinDelay, "En Establo y Jacal la ronda debe empezar antes de que termine un cruce de unos 35 s. Los demás capítulos conservan 45 a 60 s.");
            Assert.AreEqual(maximum, profile.RoamMaxDelay, "En Establo y Jacal la ronda debe empezar antes de que termine un cruce de unos 35 s. Los demás capítulos conservan 45 a 60 s.");
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

        [TestCase(Chapter.Cemetery, false, false, false, false, false)]
        [TestCase(Chapter.WaterPlant, true, false, false, false, true)]
        [TestCase(Chapter.TrainTracks, true, false, true, true, true)]
        [TestCase(Chapter.Stable, true, true, true, true, true)]
        [TestCase(Chapter.WitchHut, true, true, true, true, true)]
        [TestCase(Chapter.Return, true, false, false, false, true)]
        public void TacticsUnlockByClearing(Chapter chapter, bool search, bool ambush, bool candle, bool bark, bool lastSeen)
        {
            WitchProfile profile = WitchProfiles.For(chapter);
            Assert.AreEqual(search, profile.SearchEnabled, "La búsqueda se aprende después del cementerio.");
            Assert.AreEqual(lastSeen, profile.LastSeenEnabled, "La memoria visual se aprende después del cementerio.");
            Assert.AreEqual(ambush, profile.AmbushEnabled, "Adelantarse empieza en el establo y se desactiva en el regreso a pie.");
            Assert.AreEqual(candle, profile.CandleWatchEnabled, "La ronda se aprende en las vías y no detiene el regreso.");
            Assert.AreEqual(bark, profile.BarkLureEnabled, "El ladrido desde el bosque sustituye la voz continua desde las vías.");
        }

        [Test]
        public void DemoCopyDoesNotChangeTheChapterProfile()
        {
            var original = WitchProfiles.For(Chapter.Stable);
            var copy = original.Copy();
            copy.AmbushMinDelay = 1f;
            copy.SearchEnabled = false;
            Assert.AreEqual(40f, original.AmbushMinDelay);
            Assert.IsTrue(original.SearchEnabled);
        }
    }
}
