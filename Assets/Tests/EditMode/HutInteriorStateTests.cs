using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class HutInteriorStateTests
    {
        static readonly Bounds Interior = new Bounds(Vector3.up * 1.25f, new Vector3(7.6f, 3f, 7.6f));

        [TestCase(3.9f, 0f, 0f)]
        [TestCase(0f, 0f, 3.9f)]
        [TestCase(0f, -0.3f, 0f)]
        [TestCase(0f, 2.8f, 0f)]
        public void MarginDoesNotHideTheRoofBeforeEntry(float x, float y, float z)
        {
            var state = new HutInteriorState();
            Assert.IsFalse(state.Update(new Vector3(x, y, z), Interior, 0.25f));
            Assert.IsTrue(state.Update(Vector3.zero, Interior, 0.25f));
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        public void RoofStaysHiddenUntilPlayerLeavesTheMargin(float x, float y, float z)
        {
            var state = new HutInteriorState();
            var direction = new Vector3(x, y, z);
            var edge = Interior.center + Vector3.Scale(direction, Interior.extents);
            Assert.IsTrue(state.Update(Interior.center, Interior, 0.25f));
            Assert.IsTrue(state.Update(edge, Interior, 0.25f));
            Assert.IsTrue(state.Update(edge + direction * 0.2f, Interior, 0.25f));
            Assert.IsFalse(state.Update(edge + direction * 0.3f, Interior, 0.25f));
            Assert.IsFalse(state.Update(edge + direction * 0.2f, Interior, 0.25f));
            Assert.IsTrue(state.Update(Interior.center, Interior, 0.25f));
        }

        [Test]
        public void TeleportingOutsideAndResettingClearTheInteriorState()
        {
            var state = new HutInteriorState();
            Assert.IsTrue(state.Update(Vector3.zero, Interior, 0.25f));
            Assert.IsFalse(state.Update(Vector3.one * 100f, Interior, 0.25f));
            Assert.IsTrue(state.Update(Vector3.zero, Interior, 0.25f));
            state.Reset();
            Assert.IsFalse(state.IsInside);
            Assert.IsFalse(state.Update(Vector3.forward * 3.9f, Interior, 0.25f));
        }
    }
}
