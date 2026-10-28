namespace TiffanyGame
{
    public struct TylerInputs
    {
        public bool PlayerStill;
        public WitchForm WitchForm;
        public float WitchDistance;

        public static TylerInputs None => new TylerInputs { WitchDistance = float.PositiveInfinity };
    }
}
