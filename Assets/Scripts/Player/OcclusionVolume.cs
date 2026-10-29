using UnityEngine;

namespace TiffanyGame
{
    public static class OcclusionVolume
    {
        static readonly int[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // Esta función y TiffanyOcclusion.hlsl comparten geometría y umbrales.
        public static float KeepFraction(Vector3 point, Vector3 camera, Vector3 focus, float radius,
            float softness, float focusMargin, float minimumKeep, float nearMargin)
        {
            var toFocus = focus - camera;
            float distance = toFocus.magnitude;
            if (radius <= 0f || distance <= 0.0001f) return 1f;
            var axis = toFocus / distance;
            var offset = point - camera;
            float along = Vector3.Dot(offset, axis);
            if (along <= nearMargin || along >= distance - focusMargin) return 1f;
            float radial = (offset - axis * along).magnitude;
            float edge = Mathf.Clamp01((radial - Mathf.Max(0f, radius - softness)) / Mathf.Max(0.0001f, softness));
            edge = edge * edge * (3f - 2f * edge);
            return Mathf.Lerp(minimumKeep, 1f, edge);
        }

        public static bool KeepsPixel(float keepFraction, int x, int y) =>
            keepFraction >= (Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
    }
}
