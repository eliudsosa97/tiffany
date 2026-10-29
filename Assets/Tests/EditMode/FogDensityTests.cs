using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class FogDensityTests
    {
        [TestCase(false, false, 0.6f, 0.24f)]
        [TestCase(true, false, 1f, 0.4f)]
        [TestCase(false, true, 0.95f, 0.38f)]
        [TestCase(true, true, 1f, 0.4f)]
        public void BanksThickenAwayFromThePathAndThePathKeepsLessThanHalf(bool water, bool trees, float bank, float path)
        {
            Assert.LessOrEqual(path, bank * 0.5f);
            Assert.AreEqual(bank, FogDensity.Ambient(water, trees), 0.0001f);
            Assert.AreEqual(bank, FogDensity.Evaluate(Vector3.right * 10f,
                Vector3.back * 20f, Vector3.zero, Vector3.forward * 20f, water, trees), 0.0001f);
            Assert.AreEqual(path, FogDensity.Evaluate(Vector3.zero,
                Vector3.back * 20f, Vector3.zero, Vector3.forward * 20f, water, trees), 0.0001f);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void WaterAndTreesThickenFogWithoutClosingThePath(bool water, bool trees)
        {
            Vector3 start = Vector3.back * 20f;
            Vector3 end = Vector3.forward * 20f;
            Vector3 edge = Vector3.right * 10f;
            float baseline = FogDensity.Evaluate(edge, start, Vector3.zero, end, false, false);
            float dense = FogDensity.Evaluate(edge, start, Vector3.zero, end, water, trees);
            float path = FogDensity.Evaluate(Vector3.zero, start, Vector3.zero, end, water, trees);
            Assert.Greater(dense, baseline);
            Assert.Less(path, dense);
            Assert.That(dense, Is.InRange(0f, 1f));
            Assert.Greater(path, 0f);
        }

        [Test]
        public void BothArmsOfABendKeepTheirPathClear()
        {
            Vector3 start = Vector3.left * 20f;
            Vector3 end = Vector3.forward * 20f;
            float incoming = FogDensity.Evaluate(Vector3.left * 10f, start, Vector3.zero, end, true, true);
            float outgoing = FogDensity.Evaluate(Vector3.forward * 10f, start, Vector3.zero, end, true, true);
            float offPath = FogDensity.Evaluate(new Vector3(10f, 0f, -10f), start, Vector3.zero, end, true, true);
            Assert.AreEqual(incoming, outgoing);
            Assert.Less(incoming, offPath);
        }

        [Test]
        public void TranslatingRotatingAndChangingHeightDoesNotChangeDensity()
        {
            var start = new Vector3(-14f, 0f, 2f);
            var corner = new Vector3(3f, 0f, 8f);
            var end = new Vector3(15f, 0f, -6f);
            var point = new Vector3(2f, 1f, 3f);
            var offset = new Vector3(583f, 29f, -417f);
            var rotation = Quaternion.Euler(0f, 137f, 0f);
            float expected = FogDensity.Evaluate(point, start, corner, end, true, false);
            float actual = FogDensity.Evaluate(rotation * point + offset + Vector3.up * 17f,
                rotation * start + offset, rotation * corner + offset, rotation * end + offset, true, false);
            Assert.AreEqual(expected, actual, 0.0001f);
        }

        [Test]
        public void PathFalloffIsSmoothAndFiniteAtRepeatedEndpoints()
        {
            Vector3 start = new Vector3(8f, 0f, -4f);
            Vector3 end = start + Vector3.forward * 12f;
            float previous = 0f;
            for (int i = 0; i <= 24; i++)
            {
                float density = FogDensity.Evaluate(start + Vector3.right * (i * 0.25f), start, start, end, true, true);
                Assert.That(density, Is.InRange(previous, 1f));
                if (i > 0) Assert.Less(density - previous, 0.07f);
                previous = density;
            }
            Assert.AreEqual(0f, FogDensity.DistanceToSegment(start, start, start));
            Assert.AreEqual(FogDensity.BaseDensity * FogDensity.PathMultiplier,
                FogDensity.Evaluate(start, start, start, start, false, false));
            Assert.AreEqual(4f, FogDensity.DistanceToSegment(end + Vector3.forward * 4f, start, end), 0.0001f);
        }
    }
}
