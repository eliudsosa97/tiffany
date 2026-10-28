using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class KindleTargetingTests
    {
        [Test]
        public void ChoosesTheNearestEligibleCandle()
        {
            Vector3 player = new Vector3(3f, 0f, 4f);
            var candles = new[] { player + Vector3.right * 1.5f, player + Vector3.forward * 0.5f, player + Vector3.left };

            Assert.AreEqual(1, KindleTargeting.Nearest(player, candles, new[] { true, true, true }, 2f));
        }

        [Test]
        public void IgnoresIneligibleCandles()
        {
            var candles = new[] { Vector3.right * 0.5f, Vector3.forward * 1.5f };

            Assert.AreEqual(1, KindleTargeting.Nearest(Vector3.zero, candles, new[] { false, true }, 2f));
            Assert.AreEqual(-1, KindleTargeting.Nearest(Vector3.zero, candles, new[] { false, false }, 2f));
        }

        [TestCase(2f, 0)]
        [TestCase(2.01f, -1)]
        public void IncludesOnlyCandlesWithinRange(float distance, int expected)
        {
            Assert.AreEqual(expected, KindleTargeting.Nearest(Vector3.zero,
                new[] { Vector3.forward * distance }, new[] { true }, 2f));
        }

        [Test]
        public void ReturnsMinusOneWithoutCandles()
        {
            Assert.AreEqual(-1, KindleTargeting.Nearest(Vector3.zero, new Vector3[0], new bool[0], 2f));
        }

        [Test]
        public void IgnoresHeight()
        {
            Vector3 player = new Vector3(3f, 100f, 4f);
            var candles = new[] { player + new Vector3(1f, -100f, 0f), player + Vector3.forward * 1.5f };

            Assert.AreEqual(0, KindleTargeting.Nearest(player, candles, new[] { true, true }, 2f));
        }

        [Test]
        public void EqualDistancesKeepTheFirstCandle()
        {
            Assert.AreEqual(0, KindleTargeting.Nearest(Vector3.zero,
                new[] { Vector3.left, Vector3.right }, new[] { true, true }, 2f));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void InvalidRangeCannotSelectACandle(float range)
        {
            Assert.AreEqual(-1, KindleTargeting.Nearest(Vector3.zero,
                new[] { Vector3.zero }, new[] { true }, range));
        }
    }
}
