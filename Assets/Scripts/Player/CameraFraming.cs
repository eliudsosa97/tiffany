using UnityEngine;

namespace TiffanyGame
{
    public static class CameraFraming
    {
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
