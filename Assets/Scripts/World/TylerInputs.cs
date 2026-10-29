namespace TiffanyGame
{
    public struct TylerInputs
    {
        public bool PlayerStill;
        public bool HeardMimic;
        public bool HeardForest;
        public WitchForm WitchForm;
        public bool WitchSuspicious;
        public float WitchDistance;

        public static TylerInputs None => new TylerInputs { WitchDistance = float.PositiveInfinity };
    }
}
