using System;
using NUnit.Framework;
using UnityEngine;

namespace ElVelador.Tests
{
    public class NoiseModelTests
    {
        [TestCase(GroundType.Dirt, Gait.Slow, false, 1f)]
        [TestCase(GroundType.Dirt, Gait.Walk, false, 2f)]
        [TestCase(GroundType.Dirt, Gait.Run, false, 4f)]
        [TestCase(GroundType.Grass, Gait.Slow, false, 2f)]
        [TestCase(GroundType.Grass, Gait.Walk, false, 4f)]
        [TestCase(GroundType.Grass, Gait.Run, false, 8f)]
        [TestCase(GroundType.Slab, Gait.Slow, false, 2f)]
        [TestCase(GroundType.Slab, Gait.Walk, false, 4f)]
        [TestCase(GroundType.Slab, Gait.Run, false, 8f)]
        [TestCase(GroundType.Gravel, Gait.Slow, false, 4f)]
        [TestCase(GroundType.Gravel, Gait.Walk, false, 8f)]
        [TestCase(GroundType.Gravel, Gait.Run, false, 16f)]
        [TestCase(GroundType.Leaves, Gait.Slow, false, 7f)]
        [TestCase(GroundType.Leaves, Gait.Walk, false, 14f)]
        [TestCase(GroundType.Leaves, Gait.Run, false, 28f)]
        [TestCase(GroundType.Branches, Gait.Slow, false, 10f)]
        [TestCase(GroundType.Branches, Gait.Walk, false, 20f)]
        [TestCase(GroundType.Branches, Gait.Run, false, 40f)]
        [TestCase(GroundType.Dirt, Gait.Walk, true, 1f)]
        [TestCase(GroundType.Grass, Gait.Walk, true, 2f)]
        [TestCase(GroundType.Slab, Gait.Walk, true, 2f)]
        [TestCase(GroundType.Gravel, Gait.Walk, true, 4f)]
        [TestCase(GroundType.Leaves, Gait.Walk, true, 7f)]
        [TestCase(GroundType.Branches, Gait.Walk, true, 10f)]
        [TestCase(GroundType.Gravel, Gait.Run, true, 8f)]
        public void StepRadiusMatchesDesign(GroundType ground, Gait gait, bool raining, float expected)
        {
            Assert.AreEqual(expected, NoiseModel.StepRadius(ground, gait, raining), 1e-4f);
        }

        [Test]
        public void PublishDeliversPositionAndRadius()
        {
            var noise = new StepNoise(new Vector3(1f, 2f, 3f), 8f);
            StepNoise received = default;
            int calls = 0;
            Action<StepNoise> handler = value =>
            {
                received = value;
                calls++;
            };

            NoiseBus.Published += handler;
            try
            {
                NoiseBus.Publish(in noise);

                Assert.AreEqual(1, calls);
                Assert.AreEqual(1f, received.Position.x, 1e-4f);
                Assert.AreEqual(2f, received.Position.y, 1e-4f);
                Assert.AreEqual(3f, received.Position.z, 1e-4f);
                Assert.AreEqual(8f, received.Radius, 1e-4f);
            }
            finally
            {
                NoiseBus.Published -= handler;
            }
        }
    }
}
