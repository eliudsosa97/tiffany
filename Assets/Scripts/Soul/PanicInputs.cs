namespace ElVelador
{
    public struct PanicInputs
    {
        public bool InDarkness;
        public float WitchDistance;
        public bool HearingWhispers;
        public bool InSafeLight;
        public bool HiddenAndStill;
        public bool DogNear;

        public static PanicInputs None => new PanicInputs { WitchDistance = float.PositiveInfinity };
    }
}
