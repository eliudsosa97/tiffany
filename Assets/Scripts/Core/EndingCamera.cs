using UnityEngine;

namespace TiffanyGame
{
    public static class EndingCamera
    {
        public const float DefaultDistance = 7f;
        public const float DefaultPitch = 30f;

        public static void ClosePose(Vector3 tiffany, Vector3 family, float yaw, float distance, float pitch,
            out Vector3 position, out Quaternion rotation)
        {
            var focus = (tiffany + family) * 0.5f + Vector3.up * 0.7f;
            rotation = Quaternion.Euler(pitch, yaw, 0f);
            position = focus - rotation * Vector3.forward * distance;
        }

        public static void Blend(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot,
            float progress, out Vector3 pos, out Quaternion rot)
        {
            float t = Mathf.Clamp01(progress);
            t = t * t * (3f - 2f * t);
            pos = Vector3.Lerp(fromPos, toPos, t);
            rot = Quaternion.Slerp(fromRot, toRot, t);
        }
    }
}
