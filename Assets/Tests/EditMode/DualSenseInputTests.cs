using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace TiffanyGame.Tests
{
    public class DualSenseInputTests
    {
        Gamepad pad;
        Keyboard keyboard;
        Mouse mouse;
        InputDevice[] previousDevices;
        InputSettings previousSettings;
        HideFlags previousSettingsHideFlags;
        InputSettings testSettings;

        [SetUp]
        public void SetUp()
        {
            previousDevices = new InputDevice[] { Gamepad.current, Keyboard.current, Mouse.current };
            previousSettings = InputSystem.settings;
            previousSettingsHideFlags = previousSettings.hideFlags;
            testSettings = Object.Instantiate(previousSettings);
            testSettings.hideFlags = HideFlags.HideAndDontSave;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            // Los botones por cuadro necesitan actualizaciones de jugador también en EditMode.
            testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
            // Input System destruye los ajustes HideAndDontSave al sustituirlos.
            if (previousSettingsHideFlags == HideFlags.HideAndDontSave)
                previousSettings.hideFlags &= ~HideFlags.NotEditable;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            GameInput.ClearOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                GameInput.ClearOverrides();
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            }
            finally
            {
                if (testSettings != null)
                    testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", false);
                if (previousSettings != null)
                {
                    InputSystem.settings = previousSettings;
                    previousSettings.hideFlags = previousSettingsHideFlags;
                }
                if (testSettings != null && testSettings != InputSystem.settings)
                    Object.DestroyImmediate(testSettings);
                if (previousDevices != null)
                    foreach (var device in previousDevices)
                        if (device != null && device.added) device.MakeCurrent();
                pad = null;
                keyboard = null;
                mouse = null;
                previousDevices = null;
                previousSettings = null;
                testSettings = null;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TearDownRestoresTemporarySettingsAndOnlyRemovesOwnedDevices(bool createPad)
        {
            pad = (Gamepad)InputSystem.AddDevice("WebGLGamepad");
            int deviceCount = InputSystem.devices.Count;
            var nested = new DualSenseInputTests();
            try
            {
                nested.SetUp();
                if (createPad) nested.pad = (Gamepad)InputSystem.AddDevice("DualSenseGamepadHID");
                var createdKeyboard = nested.keyboard;
                var createdMouse = nested.mouse;
                var createdPad = nested.pad;
                nested.TearDown();
                nested.TearDown();
                Assert.IsTrue(testSettings != null);
                Assert.AreSame(testSettings, InputSystem.settings);
                Assert.AreEqual(HideFlags.HideAndDontSave, testSettings.hideFlags);
                Assert.AreEqual(deviceCount, InputSystem.devices.Count);
                Assert.AreSame(pad, Gamepad.current);
                Assert.AreSame(keyboard, Keyboard.current);
                Assert.AreSame(mouse, Mouse.current);
                Assert.IsFalse(createdKeyboard.added);
                Assert.IsFalse(createdMouse.added);
                if (createdPad != null) Assert.IsFalse(createdPad.added);
            }
            finally
            {
                nested.TearDown();
            }
        }

        [Test]
        public void TearDownWithoutSetUpLeavesInputSystemUntouched()
        {
            int deviceCount = InputSystem.devices.Count;
            new DualSenseInputTests().TearDown();
            Assert.AreSame(testSettings, InputSystem.settings);
            Assert.AreEqual(deviceCount, InputSystem.devices.Count);
            Assert.AreSame(keyboard, Keyboard.current);
            Assert.AreSame(mouse, Mouse.current);
        }

        [TestCase("DualSenseGamepadHID")]
        [TestCase("WebGLGamepad")]
        public void ButtonsUseTriangleForLightAndR2ForFlashThroughNativeAndBrowserLayouts(string layout)
        {
            pad = (Gamepad)InputSystem.AddDevice(layout);
            var buttons = new ButtonControl[] { pad.leftTrigger, pad.leftStickButton, pad.buttonNorth,
                pad.rightTrigger, pad.buttonWest, pad.buttonEast, pad.rightShoulder, pad.buttonSouth,
                pad.selectButton, pad.startButton };
            for (int action = 0; action < buttons.Length; action++)
            {
                ChangeControl(buttons[action], 1f);
                for (int other = 0; other < buttons.Length; other++)
                    Assert.AreEqual(action == other, ReadAction(other), layout + " " + buttons[action].name);
                NextFrame();
                Assert.AreEqual(action < 2, ReadAction(action));
                ChangeControl(buttons[action], 0f);
                Assert.IsFalse(ReadAction(action));
            }
        }

        [TestCase("DualSenseGamepadHID")]
        [TestCase("WebGLGamepad")]
        public void HalfLeftStickWalksAndRightStickKeepsIndependentAim(string layout)
        {
            pad = (Gamepad)InputSystem.AddDevice(layout);
            ChangeStick(pad.leftStick, Vector2.up * 0.5f);
            Assert.Greater(GameInput.Move.y, 0f);
            Assert.AreEqual(PlayerMotion.WalkSpeed, PlayerMotion.TargetSpeed(GameInput.Move.magnitude, Gait.Walk), 0.02f);
            ChangeStick(pad.rightStick, Vector2.left * 0.6f);
            Assert.Less(GameInput.AimStick.x, -0.45f);
            Assert.Greater(GameInput.Move.y, 0.49f);
            ChangeStick(pad.leftStick, Vector2.up);
            ChangeControl(pad.leftStickButton, 1f);
            Assert.IsTrue(GameInput.Run);
            Assert.AreEqual(PlayerMotion.RunSpeed, PlayerMotion.TargetSpeed(GameInput.Move.magnitude, Gait.Run), 0.02f);
        }

        [TestCase("DualSenseGamepadHID")]
        [TestCase("WebGLGamepad")]
        public void CenteredAndFullSticksKeepTheirDirectionAcrossLayouts(string layout)
        {
            pad = (Gamepad)InputSystem.AddDevice(layout);
            foreach (var direction in new[] { Vector2.zero, Vector2.left, Vector2.right, Vector2.up, Vector2.down })
            {
                ChangeStick(pad.leftStick, direction);
                ChangeStick(pad.rightStick, -direction);
                Assert.Less(Vector2.Distance(GameInput.Move, direction), 0.01f);
                Assert.Less(Vector2.Distance(GameInput.AimStick, -direction), 0.01f);
            }
        }

        void ChangeStick(StickControl stick, Vector2 value)
        {
            if (stick.x.stateBlock.format == InputStateBlock.FormatByte)
            {
                NextFrame();
                InputState.Change(stick.x, (byte)Mathf.RoundToInt((value.x + 1f) * 127.5f));
                InputState.Change(stick.y, (byte)Mathf.RoundToInt((1f - value.y) * 127.5f));
            }
            else
                ChangeControl(stick, value);
            Assert.Less(Vector2.Distance(stick.ReadUnprocessedValue(), value), 2f / byte.MaxValue);
        }

        void ChangeControl<TValue>(InputControl<TValue> control, TValue value) where TValue : struct
        {
            NextFrame();
            using (StateEvent.From(pad, out var state))
            {
                control.WriteValueIntoEvent(value, state);
                InputState.Change(pad, state);
            }
        }

        void NextFrame()
        {
            InputSystem.Update();
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            pad.MakeCurrent();
        }

        [TestCase("w", 0)]
        [TestCase("upArrow", 0)]
        [TestCase("s", 1)]
        [TestCase("downArrow", 1)]
        [TestCase("dpad/up", 0)]
        [TestCase("dpad/down", 1)]
        [TestCase("leftStick/up", 0)]
        [TestCase("leftStick/down", 1)]
        [TestCase("space", 2)]
        [TestCase("enter", 2)]
        [TestCase("buttonSouth", 2)]
        [TestCase("leftButton", 2)]
        [TestCase("escape", 3)]
        [TestCase("p", 3)]
        [TestCase("start", 3)]
        public void PauseUsesExistingNavigationSubmitAndCloseInputs(string input, int action)
        {
            pad = InputSystem.AddDevice<Gamepad>();
            NextFrame();
            if (input.StartsWith("leftStick")) ChangeStick(pad.leftStick, input.EndsWith("up") ? Vector2.up : Vector2.down);
            else GameInputTests.Press(keyboard.TryGetChildControl<ButtonControl>(input)
                ?? pad.TryGetChildControl<ButtonControl>(input) ?? mouse.GetChildControl<ButtonControl>(input));
            Assert.IsTrue(action == 0 ? GameInput.TitlePreviousPressed : action == 1 ? GameInput.TitleNextPressed
                : action == 2 ? GameInput.StartPressed : GameInput.PausePressed);
        }

        [TestCase(Key.LeftCommand, Key.F, true, false, false)]
        [TestCase(Key.RightCommand, Key.F, true, false, false)]
        [TestCase(Key.LeftAlt, Key.Enter, true, false, false)]
        [TestCase(Key.RightAlt, Key.Enter, true, false, false)]
        [TestCase(Key.None, Key.F, false, true, false)]
        [TestCase(Key.None, Key.Enter, false, false, true)]
        public void FullscreenShortcutsDoNotFlashOrSubmit(Key modifier, Key key, bool fullscreen, bool flash, bool submit)
        {
            pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.Update();
            if (modifier != Key.None) GameInputTests.Press(keyboard[modifier]);
            GameInputTests.Press(keyboard[key]);
            Assert.AreEqual((fullscreen, flash, submit), (GameInput.FullscreenTogglePressed, GameInput.FlashPressed, GameInput.StartPressed));
            InputSystem.Update();
            Assert.IsFalse(GameInput.FullscreenTogglePressed || GameInput.FlashPressed || GameInput.StartPressed);
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
