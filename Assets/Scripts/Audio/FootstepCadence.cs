namespace TiffanyGame
{
    public struct FootstepCadence
    {
        float distance;
        bool lastStepWasDistance;

        public static float StepLength(Gait gait) => gait switch
        {
            Gait.Slow => 0.55f,
            Gait.Walk => 0.75f,
            Gait.Run => 1f,
            _ => throw new System.ArgumentOutOfRangeException(nameof(gait))
        };

        public int Advance(float horizontalDistance, Gait gait, int contacts)
        {
            if (horizontalDistance > 0f) distance += horizontalDistance;
            float length = StepLength(gait);
            if (contacts > 0 && (!lastStepWasDistance || distance >= length * StepGuarantee.ContactSpacing))
            {
                distance = 0f;
                lastStepWasDistance = false;
                return contacts;
            }
            if (horizontalDistance <= 0f || distance < length) return 0;
            int count = (int)(distance / length);
            distance -= count * length;
            lastStepWasDistance = true;
            return count;
        }
    }
}
