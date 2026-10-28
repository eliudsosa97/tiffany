using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class PlayerAimTests
    {
        [TestCase(AimSource.Move)]
        [TestCase(AimSource.Stick)]
        [TestCase(AimSource.Pointer)]
        public void RightStickTakesPriorityOverPointer(AimSource current)
        {
            Assert.AreEqual(AimSource.Stick, PlayerAim.NextSource(current, Vector2.right, true, true));
        }

        [TestCase(AimSource.Move)]
        [TestCase(AimSource.Stick)]
        public void MovingPointerBecomesSource(AimSource current)
        {
            Assert.AreEqual(AimSource.Pointer, PlayerAim.NextSource(current, Vector2.zero, true, true));
        }

        [Test]
        public void PointerRemainsSourceWhenStill()
        {
            Assert.AreEqual(AimSource.Pointer, PlayerAim.NextSource(AimSource.Pointer, Vector2.zero, false, true));
        }

        [Test]
        public void ReleasingStickWithoutPointerMovementReturnsToMovement()
        {
            Assert.AreEqual(AimSource.Move, PlayerAim.NextSource(AimSource.Stick, Vector2.zero, false, true));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisallowedPointerReturnsToMovement(bool pointerMoved)
        {
            Assert.AreEqual(AimSource.Move, PlayerAim.NextSource(AimSource.Pointer, Vector2.zero, pointerMoved, false));
        }

        [TestCase(0f)]
        [TestCase(0.1f)]
        [TestCase(PlayerAim.PointerDeadzone)]
        public void PointerInsideOrAtDeadzoneHasNoDirection(float offset)
        {
            Assert.IsFalse(PlayerAim.TryDirection(AimSource.Pointer, Vector3.forward, Vector3.right,
                new Vector3(offset, 5f, 0f), out var direction));
            Assert.AreEqual(0f, direction.y);
        }

        [Test]
        public void PointerOutsideDeadzoneHasDirection()
        {
            Assert.IsTrue(PlayerAim.TryDirection(AimSource.Pointer, Vector3.forward, Vector3.right,
                new Vector3(0.2001f, 5f, 0f), out var direction));
            Assert.AreEqual(new Vector3(0.2001f, 0f, 0f), direction);
        }

        [Test]
        public void ZeroMovementHasNoDirection()
        {
            Assert.IsFalse(PlayerAim.TryDirection(AimSource.Move, Vector3.zero, Vector3.right,
                Vector3.forward, out var direction));
            Assert.AreEqual(Vector3.zero, direction);
        }

        [TestCase(AimSource.Move, 1f, 3f)]
        [TestCase(AimSource.Stick, 4f, 6f)]
        [TestCase(AimSource.Pointer, 7f, 9f)]
        public void SelectedDirectionDiscardsHeight(AimSource source, float x, float z)
        {
            Assert.IsTrue(PlayerAim.TryDirection(source, new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f),
                new Vector3(7f, 8f, 9f), out var direction));
            Assert.AreEqual(new Vector3(x, 0f, z), direction);
        }
    }
}
