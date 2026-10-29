using UnityEngine;

namespace TiffanyGame
{
    public static class FogDensity
    {
        public const float BaseDensity = 0.78f;
        public const float WaterBoost = 0.18f;
        public const float TreeBoost = 0.12f;
        public const float PathHalfWidth = 2.5f;
        public const float PathSoftness = 3f;
        public const float PathMultiplier = 0.85f;

        public static float Ambient(bool nearWater, bool nearTrees) =>
            Mathf.Clamp01(BaseDensity + (nearWater ? WaterBoost : 0f) + (nearTrees ? TreeBoost : 0f));

        public static float Evaluate(Vector3 point, Vector3 start, Vector3 corner, Vector3 end,
            bool nearWater, bool nearTrees)
        {
            float distance = Mathf.Min(DistanceToSegment(point, start, corner), DistanceToSegment(point, corner, end));
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PathHalfWidth, PathHalfWidth + PathSoftness, distance));
            return Ambient(nearWater, nearTrees) * Mathf.Lerp(PathMultiplier, 1f, edge);
        }

        public static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector2 offset = new Vector2(point.x - start.x, point.z - start.z);
            Vector2 segment = new Vector2(end.x - start.x, end.z - start.z);
            float along = segment.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(offset, segment) / segment.sqrMagnitude) : 0f;
            return (offset - segment * along).magnitude;
        }
    }
}
