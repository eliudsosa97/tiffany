namespace TiffanyGame
{
    public sealed class TylerCatchUp
    {
        private float previousDistance;
        private float stalledSeconds;

        public void Reset()
        {
            previousDistance = 0f;
            stalledSeconds = 0f;
        }

        public bool Tick(float dt, float distance, bool active, bool returning)
        {
            if (!active || returning || distance <= 8f)
            {
                Reset();
                return false;
            }
            if (distance > 22f) return true;
            stalledSeconds = distance < previousDistance ? 0f : stalledSeconds + System.Math.Max(0f, dt);
            previousDistance = distance;
            return stalledSeconds >= 3f;
        }
    }
}
