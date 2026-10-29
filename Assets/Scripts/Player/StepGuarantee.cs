namespace TiffanyGame
{
    public struct StepGuarantee
    {
        public const float DistanceMargin = 0.05f;
        public const float ContactSpacing = 0.25f;

        float distance;
        bool lastStepWasDistance;

        public static float StepLength(Gait gait) => gait switch
        {
            Gait.Slow => PlayerMotion.SlowSpeed * (40f / 60f) * 0.5f,
            Gait.Walk => PlayerMotion.WalkSpeed * (28f / 60f) * 0.5f,
            Gait.Run => PlayerMotion.RunSpeed * (24f / 60f) * 0.5f,
            _ => throw new System.ArgumentOutOfRangeException(nameof(gait))
        };

        public int Advance(float horizontalDistance, Gait gait, int contacts)
        {
            if (horizontalDistance > 0f) distance += horizontalDistance;
            float length = StepLength(gait);
            if (contacts > 0 && (!lastStepWasDistance || distance >= length * ContactSpacing))
            {
                distance = 0f;
                lastStepWasDistance = false;
                return contacts;
            }
            if (horizontalDistance <= 0f || distance < length + DistanceMargin) return 0;
            distance = 0f;
            lastStepWasDistance = true;
            return 1;
        }
    }
}
