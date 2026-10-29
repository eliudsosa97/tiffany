using UnityEngine;

namespace TiffanyGame
{
    public static class CameraFraming
    {
        public static Vector2 DecoyOffset(float distance, float pitchDegrees, float verticalFovDegrees) =>
            new Vector2(2f, MaxVisibleHeight(distance, pitchDegrees, verticalFovDegrees, 0f, 2f) - 1.25f);

        public static float MaxVisibleHeight(float distance, float pitchDegrees, float verticalFovDegrees,
            float focusHeight, float offsetAlongView)
        {
            float pitch = pitchDegrees * Mathf.Deg2Rad;
            float topAngle = (pitchDegrees - verticalFovDegrees * 0.5f) * Mathf.Deg2Rad;
            return focusHeight + distance * Mathf.Sin(pitch)
                - (distance * Mathf.Cos(pitch) + offsetAlongView) * Mathf.Tan(topAngle);
        }
    }
}
