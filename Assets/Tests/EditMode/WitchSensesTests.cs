using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class WitchSensesTests
    {
        private WitchSenses senses;
        private WitchSenseInputs input;

        [SetUp]
        public void SetUp()
        {
            senses = new WitchSenses(new WitchSenseProfile());
            input = new WitchSenseInputs
            {
                WitchPosition = new Vector3(0f, 20f, 0f),
                Form = WitchForm.Flying,
                FireLit = true,
                LineOfSight = true
            };
        }

        [Test]
        public void NullProfileIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new WitchSenses(null));
        }

        [TestCase(6f, true, false, false, true)]
        [TestCase(6.1f, true, false, false, false)]
        [TestCase(6f, false, false, false, false)]
        [TestCase(6f, true, true, false, false)]
        [TestCase(6f, true, false, true, false)]
        public void FlyingSightRequiresFireOpenSkyAndVisiblePlayer(float distance, bool fireLit,
            bool underCover, bool vanished, bool expected)
        {
            input.PlayerPosition = new Vector3(distance, 0f, 0f);
            input.FireLit = fireLit;
            input.PlayerUnderCover = underCover;
            input.PlayerVanished = vanished;
            input.LineOfSight = false;

            Assert.AreEqual(expected, senses.Sense(in input).SeesPlayer);
        }

        [TestCase(WitchForm.Flying, 45f, true, false, false, true)]
        [TestCase(WitchForm.Flying, 45.1f, true, false, false, false)]
        [TestCase(WitchForm.Flying, 45f, false, false, false, false)]
        [TestCase(WitchForm.Flying, 45f, true, true, false, false)]
        [TestCase(WitchForm.Flying, 45f, true, false, true, false)]
        [TestCase(WitchForm.Grounded, 0f, true, false, false, false)]
        public void LightSightUsesLightPositionAndRequiresFlying(WitchForm form, float distance,
            bool lightOn, bool underCover, bool vanished, bool expected)
        {
            input.Form = form;
            input.PlayerPosition = new Vector3(100f, 0f, 0f);
            input.PlayerLightPosition = new Vector3(0f, -10f, distance);
            input.PlayerLightOn = lightOn;
            input.PlayerUnderCover = underCover;
            input.PlayerVanished = vanished;
            input.FireLit = input.LineOfSight = false;

            WitchPerception p = senses.Sense(in input);
            Assert.AreEqual(expected, p.SeesPlayerLight);
            Assert.AreEqual(input.PlayerLightPosition, p.PlayerLightPosition);
        }

        [TestCase(3f, false, true, false, true)]
        [TestCase(3.1f, false, true, false, false)]
        [TestCase(3f, false, false, false, false)]
        [TestCase(15f, true, true, false, true)]
        [TestCase(15.1f, true, true, false, false)]
        [TestCase(15f, true, false, false, false)]
        [TestCase(3f, false, true, true, false)]
        [TestCase(15f, true, true, true, false)]
        public void GroundSightUsesLightRangeAndLineOfSight(float distance, bool lightOn,
            bool lineOfSight, bool vanished, bool expected)
        {
            input.Form = WitchForm.Grounded;
            input.PlayerPosition = new Vector3(0f, -10f, distance);
            input.PlayerLightOn = lightOn;
            input.LineOfSight = lineOfSight;
            input.PlayerVanished = vanished;
            input.PlayerUnderCover = true;
            input.FireLit = false;

            Assert.AreEqual(expected, senses.Sense(in input).SeesPlayer);
        }

        [TestCase(WitchForm.Flying)]
        [TestCase(WitchForm.Grounded)]
        public void HiddenPlayerRemainsVisibleForBrainTracking(WitchForm form)
        {
            input.Form = form;
            input.PlayerPosition = Vector3.right;
            input.PlayerLightPosition = Vector3.forward;
            input.PlayerLightOn = true;
            input.PlayerHidden = input.PlayerInSafeZone = input.Flashed = input.Raining = input.HearingMuted = true;

            WitchPerception p = senses.Sense(in input);
            Assert.IsTrue(p.SeesPlayer);
            Assert.AreEqual(form == WitchForm.Grounded, p.SmellsPlayer);
            Assert.AreEqual(form == WitchForm.Flying, p.SeesPlayerLight);
        }

        [TestCase(WitchForm.Grounded, 2.5f, false, true)]
        [TestCase(WitchForm.Grounded, 2.6f, false, false)]
        [TestCase(WitchForm.Flying, 0f, false, false)]
        [TestCase(WitchForm.Grounded, 0f, true, false)]
        public void SmellRequiresGroundedFormAndVisiblePlayer(WitchForm form, float distance,
            bool vanished, bool expected)
        {
            input.Form = form;
            input.PlayerPosition = new Vector3(distance, -10f, 0f);
            input.PlayerVanished = vanished;
            input.LineOfSight = false;
            input.PlayerUnderCover = true;

            Assert.AreEqual(expected, senses.Sense(in input).SmellsPlayer);
        }

        [TestCase(WitchForm.Grounded, 4f, true)]
        [TestCase(WitchForm.Grounded, 4.1f, false)]
        [TestCase(WitchForm.Flying, 4f, false)]
        public void NoiseUsesHorizontalRadiusAndIsConsumedEvenWhenUnheard(WitchForm form, float distance, bool expected)
        {
            input.Form = form;
            var noise = new StepNoise(new Vector3(0f, -30f, distance), 4f);
            senses.Hear(in noise);

            WitchPerception p = senses.Sense(in input);
            Assert.AreEqual(expected, p.HeardNoise);
            Assert.AreEqual(expected ? noise.Position : Vector3.zero, p.NoisePosition);

            input.Form = WitchForm.Grounded;
            input.WitchPosition = noise.Position;
            p = senses.Sense(in input);
            Assert.IsFalse(p.HeardNoise);
            Assert.AreEqual(Vector3.zero, p.NoisePosition);
        }

        [Test]
        public void NoPendingNoiseProducesNoSoundOrPosition()
        {
            input.Form = WitchForm.Grounded;

            WitchPerception p = senses.Sense(in input);
            Assert.IsFalse(p.HeardNoise);
            Assert.AreEqual(Vector3.zero, p.NoisePosition);
        }

        [TestCase(3f, 4f, true)]
        [TestCase(4.1f, 4f, false)]
        [TestCase(3f, 2f, false)]
        public void LatestNoiseReplacesPendingPositionAndRadius(float distance, float radius, bool expected)
        {
            input.Form = WitchForm.Grounded;
            var first = new StepNoise(Vector3.forward, 100f);
            var last = new StepNoise(new Vector3(distance, -10f, 0f), radius);
            senses.Hear(in first);
            senses.Hear(in last);

            WitchPerception p = senses.Sense(in input);
            Assert.AreEqual(expected, p.HeardNoise);
            Assert.AreEqual(expected ? last.Position : Vector3.zero, p.NoisePosition);
        }

        [Test]
        public void PendingNoiseLeavesPlayerAndWeatherFilteringToBrain()
        {
            input.Form = WitchForm.Grounded;
            input.PlayerHidden = input.PlayerVanished = input.PlayerInSafeZone = true;
            input.Flashed = input.Raining = input.HearingMuted = true;
            var noise = new StepNoise(input.PlayerPosition, 4f);
            senses.Hear(in noise);

            Assert.IsTrue(senses.Sense(in input).HeardNoise);
        }

        [TestCase(0f, 0f, 0f)]
        [TestCase(3f, 4f, 5f)]
        [TestCase(-3f, -4f, 5f)]
        public void PlayerDistanceIgnoresHeight(float x, float z, float expected)
        {
            input.WitchPosition = new Vector3(10f, 30f, -8f);
            input.PlayerPosition = new Vector3(10f + x, 0f, -8f + z);

            Assert.AreEqual(expected, senses.Sense(in input).DistanceToPlayer, 1e-4f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PositionsAndFlagsAreCopiedUnchanged(bool value)
        {
            input.PlayerPosition = new Vector3(1f, 2f, 3f);
            input.PlayerLightPosition = new Vector3(4f, 5f, 6f);
            input.PlayerInSafeZone = input.PlayerVanished = input.PlayerHidden = value;
            input.Flashed = input.Raining = input.HearingMuted = value;

            WitchPerception p = senses.Sense(in input);
            Assert.AreEqual(input.WitchPosition, p.WitchPosition);
            Assert.AreEqual(input.PlayerPosition, p.PlayerPosition);
            Assert.AreEqual(input.PlayerLightPosition, p.PlayerLightPosition);
            Assert.AreEqual(value, p.PlayerInSafeZone);
            Assert.AreEqual(value, p.PlayerVanished);
            Assert.AreEqual(value, p.PlayerHidden);
            Assert.AreEqual(value, p.Flashed);
            Assert.AreEqual(value, p.Raining);
            Assert.AreEqual(value, p.HearingMuted);
        }
    }
}
