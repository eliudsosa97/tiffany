using UnityEngine;
using UnityEngine.InputSystem;

namespace TiffanyGame
{
    public static class GameInput
    {
        public const float StickDeadzone = 0.2f;

        public static Vector2? MoveOverride;
        public static bool? SlowOverride;
        public static bool? RunOverride;
        public static Vector2? AimOverride;
        public static bool? InteractOverride;
        public static bool? LightToggleOverride;
        public static bool? FlashOverride;
        public static bool? KindleOverride;
        public static bool? PoltergeistOverride;
        public static bool? FadeOverride;

        static bool commandWasPressed;
        static FocusClickFilter focusClickFilter = new FocusClickFilter(true, float.NegativeInfinity, false);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            ClearOverrides();
            commandWasPressed = false;
#if UNITY_WEBGL && !UNITY_EDITOR
            const bool consumeFirstClick = true;
#else
            const bool consumeFirstClick = false;
#endif
            focusClickFilter = new FocusClickFilter(Application.isFocused, Time.realtimeSinceStartup, consumeFirstClick);
            InputSystem.onAfterUpdate -= UpdateKeyboard;
            InputSystem.onAfterUpdate += UpdateKeyboard;
            Application.focusChanged -= ResetKeyboardOnFocus;
            Application.focusChanged += ResetKeyboardOnFocus;
        }

        public static void ClearOverrides()
        {
            MoveOverride = null;
            SlowOverride = null;
            RunOverride = null;
            AimOverride = null;
            InteractOverride = null;
            LightToggleOverride = null;
            FlashOverride = null;
            KindleOverride = null;
            PoltergeistOverride = null;
            FadeOverride = null;
        }

        static bool CommandHeld(Keyboard keyboard) =>
            keyboard.leftCommandKey.isPressed || keyboard.rightCommandKey.isPressed;

        static void UpdateKeyboard()
        {
            var keyboard = Keyboard.current;
            bool pressed = keyboard != null && CommandHeld(keyboard);
            if (commandWasPressed && !pressed && keyboard != null) InputSystem.ResetDevice(keyboard);
            commandWasPressed = pressed;
        }

        static void ResetKeyboardOnFocus(bool focused)
        {
            focusClickFilter.SetFocus(focused, Time.realtimeSinceStartup);
            if (!focused) return;
            var keyboard = Keyboard.current;
            if (keyboard != null) InputSystem.ResetDevice(keyboard);
            commandWasPressed = false;
        }

        static bool KeyHeld(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && !CommandHeld(keyboard) && keyboard[key].isPressed;
        }

        static bool KeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && !CommandHeld(keyboard) && keyboard[key].wasPressedThisFrame;
        }

        public static Vector2 ShapeStick(Vector2 raw)
        {
            float magnitude = raw.magnitude;
            if (magnitude <= StickDeadzone) return Vector2.zero;
            return raw / magnitude * Mathf.Clamp01((magnitude - StickDeadzone) / (1f - StickDeadzone));
        }

        public static Vector2 CombineMove(Vector2 keys, Vector2 stick) =>
            Vector2.ClampMagnitude(stick.sqrMagnitude > keys.sqrMagnitude ? stick : keys, 1f);

        public static Vector2 Move
        {
            get
            {
                if (MoveOverride.HasValue) return MoveOverride.Value;
                var keys = Vector2.zero;
                var keyboard = Keyboard.current;
                if (keyboard != null && !CommandHeld(keyboard))
                {
                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keys.y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keys.y -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keys.x += 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keys.x -= 1f;
                }

                var gamepad = Gamepad.current;
                var stick = gamepad != null ? ShapeStick(gamepad.leftStick.ReadUnprocessedValue()) : Vector2.zero;
                return CombineMove(keys, stick);
            }
        }

        public static bool Slow => SlowOverride ??
            (KeyHeld(Key.LeftCtrl) || KeyHeld(Key.RightCtrl) || Gamepad.current?.leftTrigger.isPressed == true);

        public static bool Run => RunOverride ??
            (KeyHeld(Key.LeftShift) || KeyHeld(Key.RightShift) || Gamepad.current?.leftStickButton.isPressed == true);

        public static Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public static bool PointerMoved => !AimOverride.HasValue &&
            Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1f;

        public static Vector2 AimStick
        {
            get
            {
                if (AimOverride.HasValue) return AimOverride.Value;
                var gamepad = Gamepad.current;
                return gamepad != null ? ShapeStick(gamepad.rightStick.ReadUnprocessedValue()) : Vector2.zero;
            }
        }

        public static bool LightTogglePressed => LightToggleOverride ?? (KeyPressed(Key.F) ||
            Mouse.current?.rightButton.wasPressedThisFrame == true || Gamepad.current?.buttonNorth.wasPressedThisFrame == true);

        public static bool FlashPressed => FlashOverride ??
            (focusClickFilter.Accept(Mouse.current?.leftButton.wasPressedThisFrame == true, Time.realtimeSinceStartup) ||
            Gamepad.current?.rightTrigger.wasPressedThisFrame == true);

        public static bool KindlePressed => KindleOverride ??
            (KeyPressed(Key.Q) || Gamepad.current?.buttonWest.wasPressedThisFrame == true);

        public static bool PoltergeistPressed => PoltergeistOverride ??
            (KeyPressed(Key.E) || Gamepad.current?.buttonEast.wasPressedThisFrame == true);

        public static bool FadePressed => FadeOverride ??
            (KeyPressed(Key.R) || Gamepad.current?.rightShoulder.wasPressedThisFrame == true);

        public static bool InteractPressed => InteractOverride ??
            (KeyPressed(Key.Space) || Gamepad.current?.buttonSouth.wasPressedThisFrame == true);

        public static bool StartPressed
        {
            get
            {
                bool clicked = Mouse.current?.leftButton.wasPressedThisFrame == true;
                // El clic de inicio también cuenta como primer clic de foco en WebGL.
                if (clicked) focusClickFilter.Accept(true, Time.realtimeSinceStartup);
                return KeyPressed(Key.Space) || KeyPressed(Key.Enter) || clicked ||
                    Gamepad.current?.startButton.wasPressedThisFrame == true ||
                    Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
            }
        }

        public static bool PointerPressed => Mouse.current?.leftButton.wasPressedThisFrame == true;

        public static bool TitlePreviousPressed => KeyPressed(Key.W) || KeyPressed(Key.UpArrow)
            || Gamepad.current?.dpad.up.wasPressedThisFrame == true
            || Gamepad.current?.leftStick.up.wasPressedThisFrame == true;

        public static bool TitleNextPressed => KeyPressed(Key.S) || KeyPressed(Key.DownArrow)
            || Gamepad.current?.dpad.down.wasPressedThisFrame == true
            || Gamepad.current?.leftStick.down.wasPressedThisFrame == true;

        public static bool NotebookPressed => KeyPressed(Key.Tab) || Gamepad.current?.selectButton.wasPressedThisFrame == true;

        public static bool PausePressed => KeyPressed(Key.Escape) || KeyPressed(Key.P) ||
            Gamepad.current?.startButton.wasPressedThisFrame == true;

        public static bool NotebookNextPressed => KeyPressed(Key.D) || KeyPressed(Key.RightArrow) ||
            Gamepad.current?.dpad.right.wasPressedThisFrame == true;

        public static bool NotebookPreviousPressed => KeyPressed(Key.A) || KeyPressed(Key.LeftArrow) ||
            Gamepad.current?.dpad.left.wasPressedThisFrame == true;
    }
}
