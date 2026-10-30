using UnityEngine;

namespace TiffanyGame
{
    public static class TiffanyAnimation
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string WalkBack = "WalkBack";
        public const string TurnLeft = "TurnLeft";
        public const string TurnRight = "TurnRight";
        public const string Hide = "Hide";
        public const string Caught = "Caught";
        public const string Vanish = "Vanish";
        public const string Empty = "Empty";
        public const string CarryLight = "CarryLight";
        public const string Flash = "Flash";
        public const string SpeedParameter = "Speed";
        public const string TurnRateParameter = "TurnRate";
        public const int BaseLayer = 0;
        public const int ArmsLayer = 1;
        public const float MoveSpeed = 0.1f;
        public const float TurnSpeed = 45f;

        public const float IdleBlend = 0.25f;
        public const float WalkBlend = 0.2f;
        public const float TurnBlend = 0.15f;
        public const float HideBlend = 0.3f;
        public const float CaughtBlend = 0.1f;
        public const float VanishBlend = 0.2f;
        public const float EmptyBlend = 0.2f;
        public const float CarryLightBlend = 0.2f;
        public const float FlashBlend = 0.1f;

        public static float TurnRate(float angularSpeed, float authoredSpeed) => Mathf.Abs(angularSpeed) / authoredSpeed;

        public struct ArmsState
        {
            public bool RestartFlash { get; private set; }
            float previousFlash;
            float remaining;

            public string Advance(bool suppressed, bool lightOn, float flashRemaining, float flashDuration, float dt)
            {
                RestartFlash = !suppressed && flashRemaining > previousFlash;
                previousFlash = flashRemaining;
                remaining = suppressed ? 0f : RestartFlash ? flashDuration : Mathf.Max(0f, remaining - dt);
                if (suppressed) return Empty;
                if (remaining > 0f) return Flash;
                return lightOn ? CarryLight : Empty;
            }
        }

        // angularSpeed en grados por segundo, positiva hacia la derecha como el yaw de Unity.
        public static string StateFor(bool caught, bool vanishing, bool hidden, float speed, float angularSpeed, bool backward)
        {
            if (caught) return Caught;
            if (vanishing) return Vanish;
            if (hidden) return Hide;
            if (speed > MoveSpeed) return backward ? WalkBack : Walk;
            if (angularSpeed > TurnSpeed) return TurnRight;
            if (angularSpeed < -TurnSpeed) return TurnLeft;
            return Idle;
        }

        public static float BlendFor(string state)
        {
            return state switch
            {
                Idle => IdleBlend,
                Walk or WalkBack => WalkBlend,
                TurnLeft or TurnRight => TurnBlend,
                Hide => HideBlend,
                Caught => CaughtBlend,
                Vanish => VanishBlend,
                Empty => EmptyBlend,
                CarryLight => CarryLightBlend,
                Flash => FlashBlend,
                _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }
}
