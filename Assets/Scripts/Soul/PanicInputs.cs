namespace ElVelador
{
    public struct PanicInputs
    {
        public bool InDarkness;
        public float VeladorDistance;
        public bool HearingWhispers;
        public bool InSafeLight;
        public bool HiddenAndStill;
        public bool DogNear;

        public static PanicInputs None => new PanicInputs { VeladorDistance = float.PositiveInfinity };
    }
}
