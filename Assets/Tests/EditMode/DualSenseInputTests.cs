using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TiffanyGame.Tests
{
    public class DualSenseInputTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            GameInput.ClearOverrides();
        }

        public override void TearDown()
        {
            GameInput.ClearOverrides();
            base.TearDown();
        }

        [TestCase("DualSenseGamepadHID")]
        [TestCase("WebGLGamepad")]
        public void ButtonsMatchTheGddThroughNativeAndBrowserLayouts(string layout)
        {
            var pad = (Gamepad)InputSystem.AddDevice(layout);
            var buttons = new ButtonControl[] { pad.leftTrigger, pad.leftStickButton, pad.buttonNorth,
                pad.rightTrigger, pad.buttonWest, pad.buttonEast, pad.rightShoulder, pad.buttonSouth,
                pad.selectButton, pad.startButton };
            for (int action = 0; action < buttons.Length; action++)
            {
                Press(buttons[action]);
                for (int other = 0; other < buttons.Length; other++)
                    Assert.AreEqual(action == other, ReadAction(other), layout + " " + buttons[action].name);
                InputSystem.Update();
                Assert.AreEqual(action < 2, ReadAction(action));
                Release(buttons[action]);
                Assert.IsFalse(ReadAction(action));
            }
        }

        [TestCase("DualSenseGamepadHID")]
        [TestCase("WebGLGamepad")]
        public void HalfLeftStickWalksAndRightStickKeepsIndependentAim(string layout)
        {
            var pad = (Gamepad)InputSystem.AddDevice(layout);
            Set(pad.leftStick, Vector2.up * 0.5f);
            Assert.Greater(GameInput.Move.y, 0f);
            Assert.AreEqual(PlayerMotion.WalkSpeed, PlayerMotion.TargetSpeed(GameInput.Move.magnitude, Gait.Walk), 0.02f);
            Set(pad.rightStick, Vector2.left * 0.6f);
            Assert.Less(GameInput.AimStick.x, -0.45f);
            Assert.Greater(GameInput.Move.y, 0.49f);
            Set(pad.leftStick, Vector2.up);
            Press(pad.leftStickButton);
            Assert.IsTrue(GameInput.Run);
            Assert.AreEqual(PlayerMotion.RunSpeed, PlayerMotion.TargetSpeed(GameInput.Move.magnitude, Gait.Run), 0.02f);
        }

        static bool ReadAction(int action) => action switch
        {
            0 => GameInput.Slow,
            1 => GameInput.Run,
            2 => GameInput.LightTogglePressed,
            3 => GameInput.FlashPressed,
            4 => GameInput.KindlePressed,
            5 => GameInput.PoltergeistPressed,
            6 => GameInput.FadePressed,
            7 => GameInput.InteractPressed,
            8 => GameInput.NotebookPressed,
            _ => GameInput.PausePressed
        };
    }
}
