using NUnit.Framework;

namespace TiffanyGame.Tests
{
    public sealed class TylerLightShareTests
    {
        private TylerLightShare share;

        [SetUp]
        public void SetUp() => share = new TylerLightShare();

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void StartsOnlyWhenEmptyAndNear(bool empty, bool near, bool expected)
        {
            Assert.AreEqual(expected, share.Tick(1f, ShareMode.LightOnly, empty, near, false,
                empty ? 0f : 10f, out float recharge));
            Assert.AreEqual(0f, recharge);
        }

        [Test]
        public void LightOnlyStaysOnWithoutRechargingWhenEnergyRises()
        {
            Assert.IsTrue(share.Tick(1f, ShareMode.LightOnly, true, true, false, 0f, out _));
            Assert.IsTrue(share.Tick(10f, ShareMode.LightOnly, false, true, false, 100f, out float recharge));
            Assert.AreEqual(0f, recharge);
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void DistanceOrCandleStopsBothModes(bool near, bool candle)
        {
            foreach (var mode in new[] { ShareMode.LightOnly, ShareMode.SlowRecharge })
            {
                Assert.IsTrue(share.Tick(0f, mode, true, true, false, 0f, out _));
                Assert.IsFalse(share.Tick(1f, mode, true, near, candle, 0f, out float recharge));
                Assert.AreEqual(0f, recharge);
                Assert.IsFalse(share.Tick(1f, mode, false, true, false, 10f, out _));
            }
        }

        [TestCase(0.5f, 1f)]
        [TestCase(1f, 2f)]
        [TestCase(3f, 6f)]
        public void SlowRechargeAddsTwoPerSecondAndStaysOn(float dt, float expected)
        {
            Assert.IsTrue(share.Tick(dt, ShareMode.SlowRecharge, true, true, false, 0f, out float recharge));
            Assert.AreEqual(expected, recharge);
            Assert.IsTrue(share.Tick(dt, ShareMode.SlowRecharge, false, true, false, recharge, out recharge));
            Assert.AreEqual(expected, recharge);
        }

        [TestCase(24f, 1f)]
        [TestCase(25f, 0f)]
        [TestCase(100f, 0f)]
        public void SlowRechargeStopsAtCapWithoutOvershooting(float energy, float expected)
        {
            share.Tick(0f, ShareMode.SlowRecharge, true, true, false, 0f, out _);
            Assert.IsFalse(share.Tick(10f, ShareMode.SlowRecharge, false, true, false, energy, out float recharge));
            Assert.AreEqual(expected, recharge);
            Assert.IsFalse(share.Tick(1f, ShareMode.SlowRecharge, false, true, false, 20f, out recharge));
            Assert.AreEqual(0f, recharge);
            Assert.IsTrue(share.Tick(0f, ShareMode.SlowRecharge, true, true, false, 0f, out _));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTimeDoesNotRecharge(float dt)
        {
            Assert.IsTrue(share.Tick(dt, ShareMode.SlowRecharge, true, true, false, 0f, out float recharge));
            Assert.AreEqual(0f, recharge);
        }
    }
}
