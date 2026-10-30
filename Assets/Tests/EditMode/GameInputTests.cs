using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace TiffanyGame.Tests
{
    public class GameInputTests
    {
        const float Tolerance = 0.00001f;

        static void Change(ButtonControl control, float value)
        {
            using (StateEvent.From(control.device, out var eventPtr))
            {
                control.WriteValueIntoEvent(value, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
            InputSystem.Update();
        }

        [TestCase("left", true, false)]
        [TestCase("f", true, false)]
        [TestCase("right", false, false)]
        [TestCase("north", true, false)]
        [TestCase("r2", false, true)]
        public void LightUsesHeldMouseTriangleAndFWhileR2Runs(string input, bool held, bool run)
        {
            WithPointerInput(() =>
            {
                var pad = InputSystem.AddDevice<Gamepad>();
                try
                {
                    var keyboard = Keyboard.current;
                    var mouse = Mouse.current;
                    ButtonControl button = input == "f" ? keyboard.fKey : input == "north" ? pad.buttonNorth
                        : input == "r2" ? pad.rightTrigger : input == "left" ? mouse.leftButton : mouse.rightButton;
                    Change(button, 1f);
                    Assert.AreEqual(held, GameInput.LightHeld);
                    Assert.AreEqual(run, GameInput.Run);
                    InputSystem.Update();
                    Assert.AreEqual(held, GameInput.LightHeld);
                    Assert.AreEqual(run, GameInput.Run);
                    Change(button, 0f);
                    Assert.IsFalse(GameInput.LightHeld || GameInput.Run);
                }
                finally { InputSystem.RemoveDevice(pad); }
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConsumedOrFocusFilteredClickStaysInvalidUntilReleased(bool focusFiltered)
        {
            WithPointerInput(() =>
            {
                var filter = typeof(GameInput).GetField("focusClickFilter", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (focusFiltered) filter.SetValue(null, new FocusClickFilter(true, float.NegativeInfinity, true));
                var consumed = typeof(GameInput).GetField("pointerConsumedThroughFrame", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var charge = new FlashCharge();
                Change(Mouse.current.leftButton, 1f);
                if (!focusFiltered) GameInput.ConsumePointerPress();
                Assert.IsFalse(GameInput.LightHeld);
                consumed.SetValue(null, -1);
                InputSystem.Update();
                Assert.IsFalse(GameInput.LightHeld);
                Assert.AreEqual(FlashChargeResult.None, charge.Tick(4f, GameInput.LightHeld, true));
                Change(Mouse.current.leftButton, 0f);
                Assert.IsFalse(GameInput.LightHeld);
                Assert.AreEqual(FlashChargeResult.None, charge.Tick(0.1f, GameInput.LightHeld, true));
                Change(Mouse.current.leftButton, 1f);
                Assert.IsTrue(GameInput.LightHeld);
            });
        }

        static void WithPointerInput(System.Action test)
        {
            var devices = new DualSenseInputTests();
            devices.SetUp();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var filter = typeof(GameInput).GetField("focusClickFilter", flags);
            var consumed = typeof(GameInput).GetField("pointerConsumedThroughFrame", flags);
            var held = typeof(GameInput).GetField("pointerLightHeld", flags);
            var invalid = typeof(GameInput).GetField("pointerLightInvalid", flags);
            var previousFilter = filter.GetValue(null);
            var previousConsumed = consumed.GetValue(null);
            var previousHeld = held.GetValue(null);
            var previousInvalid = invalid.GetValue(null);
            try
            {
                filter.SetValue(null, new FocusClickFilter(true, float.NegativeInfinity, false));
                consumed.SetValue(null, -1);
                held.SetValue(null, false);
                invalid.SetValue(null, false);
                test();
            }
            finally
            {
                devices.TearDown();
                filter.SetValue(null, previousFilter);
                consumed.SetValue(null, previousConsumed);
                held.SetValue(null, previousHeld);
                invalid.SetValue(null, previousInvalid);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PowerOverridesControlPressesAndCanBeCleared(bool pressed)
        {
            GameInput.KindleOverride = pressed;
            GameInput.PoltergeistOverride = pressed;
            GameInput.FadeOverride = pressed;

            Assert.That(GameInput.KindlePressed, Is.EqualTo(pressed));
            Assert.That(GameInput.PoltergeistPressed, Is.EqualTo(pressed));
            Assert.That(GameInput.FadePressed, Is.EqualTo(pressed));

            GameInput.ClearOverrides();

            Assert.That(GameInput.KindleOverride, Is.Null);
            Assert.That(GameInput.PoltergeistOverride, Is.Null);
            Assert.That(GameInput.FadeOverride, Is.Null);
        }

        [TearDown]
        public void TearDown()
        {
            GameInput.ClearOverrides();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InteractOverrideControlsPressAndCanBeCleared(bool pressed)
        {
            GameInput.InteractOverride = pressed;
            Assert.That(GameInput.InteractPressed, Is.EqualTo(pressed));
            GameInput.ClearOverrides();
            Assert.That(GameInput.InteractOverride, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LightHeldOverrideControlsHoldAndCanBeCleared(bool held)
        {
            GameInput.LightHeldOverride = held;
            Assert.That(GameInput.LightHeld, Is.EqualTo(held));
            GameInput.ClearOverrides();
            Assert.That(GameInput.LightHeldOverride, Is.Null);
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
