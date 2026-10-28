using System;
using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class CrossingLayoutTests
    {
        [TestCase(3f, 4)]
        [TestCase(4f, 5)]
        [TestCase(5f, 6)]
        public void StonesCoverBothBanksWithWalkingDistances(float width, int count)
        {
            Vector3 start = new Vector3(8f, 0f, -2f);
            Vector3 end = start + Vector3.right * width;
            var stones = CrossingLayout.Create(start, end, count);

            Assert.AreEqual(count, stones.Length);
            Assert.IsTrue(CrossingLayout.IsWalkable(start, end, stones));
            for (int i = 0; i < count; i++)
                Assert.Less(Vector3.Distance(Vector3.Lerp(start, end, (i + 0.5f) / count), stones[i]), 0.0001f);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        public void RejectsAStoneThatBreaksTheWalkingDistance(int index)
        {
            Vector3 start = Vector3.back * 2f;
            Vector3 end = Vector3.forward * 2f;
            var stones = CrossingLayout.Create(start, end, 5);
            stones[index] += Vector3.right;

            Assert.IsFalse(CrossingLayout.IsWalkable(start, end, stones));
        }

        [Test]
        public void BankConnectionsArePartOfValidation()
        {
            var stones = CrossingLayout.Create(Vector3.zero, Vector3.forward * 4f, 5);

            Assert.IsFalse(CrossingLayout.IsWalkable(Vector3.back, Vector3.forward * 4f, stones));
            Assert.IsFalse(CrossingLayout.IsWalkable(Vector3.zero, Vector3.forward * 5f, stones));
        }

        [Test]
        public void TooFewStonesCannotCreateAnUnwalkableCrossing()
        {
            var error = Assert.Throws<ArgumentException>(() =>
                CrossingLayout.Create(Vector3.zero, Vector3.forward * 5f, 4));
            StringAssert.StartsWith(GameInfo.LogPrefix, error.Message);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsEmptyStoneCounts(int count)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CrossingLayout.Create(Vector3.zero, Vector3.forward, count));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidWalkingDistance(float spacing)
        {
            Assert.IsFalse(CrossingLayout.IsWalkable(Vector3.zero, Vector3.forward, new[] { Vector3.forward * 0.5f }, spacing));
        }

        [Test]
        public void RejectsMissingAndNonFinitePositions()
        {
            Assert.IsFalse(CrossingLayout.IsWalkable(Vector3.zero, Vector3.forward, null));
            Assert.IsFalse(CrossingLayout.IsWalkable(Vector3.zero, Vector3.forward, Array.Empty<Vector3>()));
            Assert.IsFalse(CrossingLayout.IsWalkable(Vector3.zero, Vector3.forward, new[] { new Vector3(float.NaN, 0f, 0f) }));
            Assert.Throws<ArgumentException>(() => CrossingLayout.Create(Vector3.zero, Vector3.positiveInfinity, 5));
        }
    }
}
