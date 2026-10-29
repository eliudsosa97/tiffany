using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class OcclusionVolumeTests
    {
        static float Keep(Vector3 point) => OcclusionVolume.KeepFraction(point, Vector3.zero,
            Vector3.forward * 10f, 2f, 1f, 0.2f, 0.125f, 0.2f);

        [TestCase(0f, 0f, 5f, 0.125f)]
        [TestCase(1f, 0f, 5f, 0.125f)]
        [TestCase(0f, 1f, 5f, 0.125f)]
        [TestCase(1.5f, 0f, 5f, 0.5625f)]
        [TestCase(2f, 0f, 5f, 1f)]
        [TestCase(3f, 0f, 5f, 1f)]
        [TestCase(0f, 0f, -1f, 1f)]
        [TestCase(0f, 0f, 0.2f, 1f)]
        [TestCase(0f, 0f, 9.8f, 1f)]
        [TestCase(0f, 0f, 10f, 1f)]
        [TestCase(0f, 0f, 12f, 1f)]
        public void CylinderOnlyCutsBetweenCameraAndChest(float x, float y, float z, float expected)
        {
            Assert.AreEqual(expected, Keep(new Vector3(x, y, z)), 1e-5f);
        }

        [Test]
        public void SoftEdgeIsContinuousAndMonotonic()
        {
            float previous = Keep(new Vector3(1f, 0f, 5f));
            for (int i = 1; i <= 100; i++)
            {
                float current = Keep(new Vector3(1f + i * 0.01f, 0f, 5f));
                Assert.GreaterOrEqual(current, previous);
                Assert.Less(current - previous, 0.014f);
                previous = current;
            }
            Assert.AreEqual(1f, previous);
        }

        [Test]
        public void MovingAndRotatingTheVolumePreservesTheCut()
        {
            var rotation = Quaternion.Euler(42f, 63f, 11f);
            var camera = new Vector3(108f, 23f, 391f);
            var point = new Vector3(1.5f, 0f, 5f);
            float moved = OcclusionVolume.KeepFraction(camera + rotation * point, camera,
                camera + rotation * Vector3.forward * 10f, 2f, 1f, 0.2f, 0.125f, 0.2f);
            Assert.AreEqual(Keep(point), moved, 1e-4f);
        }

        [Test]
        public void DisabledOrDegenerateVolumeKeepsAllPixels()
        {
            Assert.AreEqual(1f, OcclusionVolume.KeepFraction(Vector3.forward, Vector3.zero,
                Vector3.zero, 2f, 1f, 0.2f, 0.125f, 0.2f));
            Assert.AreEqual(1f, OcclusionVolume.KeepFraction(Vector3.forward, Vector3.zero,
                Vector3.forward * 10f, 0f, 1f, 0.2f, 0.125f, 0.2f));
        }

        [TestCase(0f, 0)]
        [TestCase(0.125f, 2)]
        [TestCase(0.5625f, 9)]
        [TestCase(1f, 16)]
        public void BayerKeepsExpectedPixelsAndRepeatsEveryFour(float fraction, int expected)
        {
            int kept = 0;
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
            {
                bool visible = OcclusionVolume.KeepsPixel(fraction, x, y);
                if (visible) kept++;
                Assert.AreEqual(visible, OcclusionVolume.KeepsPixel(fraction, x + 4, y + 8));
            }
            Assert.AreEqual(expected, kept);
        }
    }
}
