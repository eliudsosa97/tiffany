using UnityEngine;

namespace TiffanyGame
{
    public static class PlayerFacing
    {
        public static float SpeedFactor(Vector3 moveDirection, Vector3 facing, bool running, float backwardFactor)
        {
            moveDirection.y = 0f;
            facing.y = 0f;
            if (running || moveDirection.sqrMagnitude == 0f) return 1f;
            float dot = Vector3.Dot(moveDirection.normalized, facing.normalized);
            return Mathf.Lerp(backwardFactor, 1f, Mathf.Clamp01(dot));
        }

        public static bool FacesMovement(Gait inputGait, Vector3 moveDirection)
        {
            moveDirection.y = 0f;
            return inputGait == Gait.Run && moveDirection.sqrMagnitude > 0f;
        }

        public struct TurnState
        {
            bool wasRunning;
            bool returning;

            public float Speed(bool running, float remainingAngle, float turnSpeed, float returnTurnSpeed)
            {
                if (running) returning = false;
                else if (wasRunning) returning = true;
                if (remainingAngle <= 2f) returning = false;
                wasRunning = running;
                return returning ? returnTurnSpeed : turnSpeed;
            }
        }
    }
}
