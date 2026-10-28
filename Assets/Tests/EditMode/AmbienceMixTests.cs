using NUnit.Framework;
namespace TiffanyGame.Tests
{
    public sealed class AmbienceMixTests
    {
        private AmbienceMix mix;
        [SetUp] public void SetUp() => mix = new AmbienceMix();
        [TestCase(-1, 0.6f, 0.35f, 0.3f, 0f, 0f, AmbienceCall.Owl)] [TestCase(0, 0.6f, 0.35f, 0.3f, 0f, 0f, AmbienceCall.Owl)]
        [TestCase(1, 0.3f, 0.5f, 0.15f, 0f, 0f, AmbienceCall.Coyote)] [TestCase(2, 0.25f, 0.75f, 0f, 0f, 0f, AmbienceCall.Coyote)]
        [TestCase(3, 0.4f, 0.15f, 0.5f, 0f, 0f, AmbienceCall.BarnOwl)]
        [TestCase(4, 0f, 0.2f, 0f, 0f, 0f, AmbienceCall.Howl)]
        [TestCase(5, 0.2f, 0.45f, 0f, 0.6f, 0.6f, AmbienceCall.Owl)]
        [TestCase(6, 0.3f, 0.7f, 0f, 0f, 0f, AmbienceCall.Coyote)] [TestCase(7, 0.5f, 0.2f, 0.2f, 0f, 0f, AmbienceCall.Owl)]
        public void ProfilesHaveTheirOwnLayersAndCalls(int clearing, float crickets, float wind, float leaves, float frogs, float lake, AmbienceCall call)
        {
            mix.Tick(14f, clearing, float.PositiveInfinity, 0f);
            float[] expected = { crickets, wind, leaves, frogs, lake };
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], mix.Volume((AmbienceLayer)i), 0.0001f);
            Assert.AreEqual((true, call, 0, 0.5f), (mix.Call, mix.CallKind, mix.CallVariant, mix.CallVolume));
        }
        [TestCase(8f, 0f, 0f)] [TestCase(7f, 0f, 0f)] [TestCase(16.5f, 0f, 0.5f)]
        [TestCase(25f, 0f, 1f)] [TestCase(float.PositiveInfinity, 0f, 1f)]
        [TestCase(25f, 0.75f, 0.3f)] [TestCase(25f, 0.749f, 1f)] [TestCase(16.5f, 1f, 0.15f)]
        public void WitchAndPanicMultiplyEveryLayer(float distance, float panic, float factor)
        {
            mix.Tick(2f, 5, 25f, 0f);
            mix.Tick(2f, 5, distance, panic);
            float[] profile = { 0.2f, 0.45f, 0f, 0.6f, 0.6f };
            for (int i = 0; i < profile.Length; i++) Assert.AreEqual(profile[i] * factor, mix.Volume((AmbienceLayer)i), 0.0001f);
        }
        [TestCase(54f, 1f)] [TestCase(55f, 0.15f)] [TestCase(61.5f, 0.15f)]
        [TestCase(62f, 1f)] [TestCase(110f, 0.15f)] [TestCase(117f, 1f)]
        public void SilenceStartsAtFiftyFiveLastsSevenAndRepeatsOnTheClock(float time, float factor)
        {
            mix.Tick(2f, 0, 25f, 0f);
            mix.Tick(time - 2f, 0, 25f, 0f);
            Assert.AreEqual(0.35f * factor, mix.Volume(AmbienceLayer.Wind), 0.0001f);
        }
        [TestCase(2)] [TestCase(6)]
        public void CallsCycleIntervalsRotateKindsAndHowlsAndSkipSilence(int clearing)
        {
            float[] intervals = { 14f, 22f, 18f, 26f };
            int emitted = 0, howls = 0;
            for (int i = 0; i < 12; i++)
            {
                mix.Tick(intervals[i % 4] - 0.25f, clearing, 25f, 0f);
                Assert.IsFalse(mix.Call);
                mix.Tick(0.25f, clearing, 25f, 0f);
                Assert.AreEqual(i != 5, mix.Call);
                if (mix.Call)
                {
                    var kind = emitted++ % 2 == 0 ? AmbienceCall.Coyote : AmbienceCall.Howl;
                    Assert.AreEqual((kind, kind == AmbienceCall.Howl ? howls++ % 3 : 0, 0.5f), (mix.CallKind, mix.CallVariant, mix.CallVolume));
                }
            }
        }
        [TestCase(24.99f, 0f)] [TestCase(8f, 0f)] [TestCase(25f, 0.75f)]
        public void SuppressedCallsAreSkippedWithoutDelayingTheSchedule(float distance, float panic)
        {
            mix.Tick(14f, 0, distance, panic);
            Assert.AreEqual((false, 0f), (mix.Call, mix.CallVolume));
            mix.Tick(21f, 0, 25f, 0f);
            Assert.IsFalse(mix.Call);
            mix.Tick(1f, 0, 25f, 0f);
            Assert.IsTrue(mix.Call);
            mix.Tick(0f, 0, 25f, 0f);
            Assert.IsFalse(mix.Call);
        }
        [Test]
        public void ChangingClearingsFadesAtHalfVolumePerSecondAndZeroTimeFreezesIt()
        {
            mix.Tick(2f, 0, 25f, 0f);
            mix.Tick(0f, 3, 8f, 1f);
            Assert.AreEqual(0.35f, mix.Volume(AmbienceLayer.Wind), 0.0001f);
            mix.Tick(0.1f, 3, 25f, 0f);
            Assert.AreEqual(0.3f, mix.Volume(AmbienceLayer.Wind), 0.0001f);
            Assert.AreEqual(0.35f, mix.Volume(AmbienceLayer.Leaves), 0.0001f);
        }
    }
}
