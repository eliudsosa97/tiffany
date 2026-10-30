using UnityEngine;

namespace TiffanyGame
{
    public sealed class TylerCatchUp
    {
        private float previousDistance;
        private float stalledSeconds;
        private float retrySeconds;
        public bool Approaching { get; private set; }

        public void Reset()
        {
            previousDistance = 0f;
            stalledSeconds = 0f;
            retrySeconds = 0f;
            Approaching = false;
        }

        public void Placed() { Reset(); Approaching = true; }

        public static Vector3 Candidate(Vector3 player, Vector3 forward, Vector3 right, int index) =>
            player + (index < 3 ? -forward : index < 6 ? right : -right) * (10f + index % 3 * 2f);

        public static bool AcceptsPlacement(float distance, bool tylerVisible, bool pointVisible) =>
            !tylerVisible && !pointVisible && distance >= 10f && distance <= 14f;

        public bool Tick(float dt, float distance, bool active, bool returning)
        {
            if (!active || returning || distance <= TylerMotion.FollowDistance + 0.1f) Approaching = false;
            if (!active || returning || distance <= 8f)
            {
                previousDistance = stalledSeconds = retrySeconds = 0f;
                return false;
            }
            stalledSeconds = distance < previousDistance ? 0f : stalledSeconds + System.Math.Max(0f, dt);
            previousDistance = distance;
            retrySeconds = System.Math.Max(0f, retrySeconds - System.Math.Max(0f, dt));
            if (retrySeconds > 0f || (distance <= 30f && stalledSeconds < 5f)) return false;
            retrySeconds = 1f;
            return true;
        }
    }
}
