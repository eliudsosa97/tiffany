namespace TiffanyGame
{
    public readonly struct TylerPose
    {
        public bool Stopped { get; }
        public bool LookUp { get; }
        public bool Crouched { get; }
        public bool FaceWitch { get; }

        public TylerPose(bool stopped, bool lookUp, bool crouched, bool faceWitch)
        {
            Stopped = stopped;
            LookUp = lookUp;
            Crouched = crouched;
            FaceWitch = faceWitch;
        }
    }

    public static class TylerPoses
    {
        public static TylerPose For(TylerReaction reaction)
        {
            return reaction switch
            {
                TylerReaction.Whine => new TylerPose(true, true, false, true),
                TylerReaction.Growl => new TylerPose(true, false, true, true),
                _ => new TylerPose(false, false, false, false)
            };
        }
    }
}
