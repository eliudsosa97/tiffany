using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class GameInputTests
    {
        const float Tolerance = 0.00001f;

        [TearDown]
        public void TearDown()
        {
            GameInput.ClearOverrides();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FlashOverrideControlsFlashPressed(bool pressed)
        {
            GameInput.FlashOverride = pressed;

            Assert.That(GameInput.FlashPressed, Is.EqualTo(pressed));
        }

        [Test]
        public void ClearOverridesClearsFlashOverride()
        {
            GameInput.FlashOverride = true;

            GameInput.ClearOverrides();

            Assert.That(GameInput.FlashOverride, Is.Null);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.6f, -0.4f)]
        public void AimOverrideControlsStickSuppressesPointerAndCanBeCleared(float x, float y)
        {
            var aim = new Vector2(x, y);
            GameInput.AimOverride = aim;

            Assert.That(GameInput.AimStick, Is.EqualTo(aim));
            Assert.That(GameInput.PointerMoved, Is.False);

            GameInput.ClearOverrides();

            Assert.That(GameInput.AimOverride, Is.Null);
        }

        [TestCase(0f, 0f)]
        [TestCase(0.1f, 0.1f)]
        [TestCase(0.2f, 0f)]
        [TestCase(0f, -0.2f)]
        public void ShapeStickReturnsZeroInsideOrAtDeadzone(float x, float y)
        {
            Assert.That(GameInput.ShapeStick(new Vector2(x, y)), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ShapeStickJustOutsideDeadzoneReturnsSmallPositiveMagnitude()
        {
            float magnitude = GameInput.ShapeStick(new Vector2(0.2001f, 0f)).magnitude;

            Assert.That(magnitude, Is.GreaterThan(0f));
            Assert.That(magnitude, Is.EqualTo(0.000125f).Within(0.000001f));
        }

        [Test]
        public void ShapeStickUsesRadialDeadzone()
        {
            var raw = new Vector2(0.15f, 0.15f);

            Assert.That(GameInput.ShapeStick(raw).magnitude, Is.EqualTo(0.015165f).Within(Tolerance));
        }

        [Test]
        public void ShapeStickRescalesMagnitudeAboveDeadzone()
        {
            Assert.That(GameInput.ShapeStick(new Vector2(0.6f, 0f)).magnitude, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void ShapeStickAtFullDeflectionReturnsUnitMagnitude()
        {
            Assert.That(GameInput.ShapeStick(Vector2.right).magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void ShapeStickPreservesDirection()
        {
            var direction = GameInput.ShapeStick(new Vector2(0.3f, -0.4f)).normalized;

            Assert.That(direction.x, Is.EqualTo(0.6f).Within(Tolerance));
            Assert.That(direction.y, Is.EqualTo(-0.8f).Within(Tolerance));
        }

        [Test]
        public void ShapeStickClampsMagnitudeAboveOne()
        {
            var result = GameInput.ShapeStick(new Vector2(1f, 1f));

            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.x, Is.EqualTo(result.y).Within(Tolerance));
        }

        [Test]
        public void CombineMoveClampsKeyboardDiagonal()
        {
            var result = GameInput.CombineMove(new Vector2(1f, 1f), Vector2.zero);

            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.x, Is.EqualTo(1f / Mathf.Sqrt(2f)).Within(Tolerance));
            Assert.That(result.y, Is.EqualTo(1f / Mathf.Sqrt(2f)).Within(Tolerance));
        }

        [Test]
        public void CombineMoveChoosesStrongerStick()
        {
            var stick = new Vector2(-0.6f, 0.2f);

            Assert.That(GameInput.CombineMove(new Vector2(0.3f, 0f), stick), Is.EqualTo(stick));
        }

        [Test]
        public void CombineMoveChoosesStrongerKeyboard()
        {
            var keys = new Vector2(0f, 0.9f);

            Assert.That(GameInput.CombineMove(keys, new Vector2(-0.6f, 0.2f)), Is.EqualTo(keys));
        }

        [Test]
        public void CombineMoveClampsStrongerStick()
        {
            var result = GameInput.CombineMove(Vector2.right, new Vector2(0f, -2f));

            Assert.That(result, Is.EqualTo(Vector2.down));
        }
    }
}
