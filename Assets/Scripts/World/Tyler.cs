namespace ElVelador
{
    public sealed class Tyler
    {
        public const float GrowlRadius = 20f;
        public const float WhineRadius = 30f;
        public const float GuideDelay = 3f;

        private float stillTime;

        public TylerReaction Reaction { get; private set; }

        public TylerReaction Tick(float dt, in TylerInputs inputs)
        {
            if (dt < 0f || float.IsNaN(dt))
            {
                dt = 0f;
            }

            stillTime = inputs.PlayerStill ? stillTime + dt : 0f;
            bool validWitch = inputs.WitchDistance >= 0f && !float.IsInfinity(inputs.WitchDistance);
            if (validWitch && inputs.WitchForm == WitchForm.Grounded && inputs.WitchDistance < GrowlRadius)
            {
                Reaction = TylerReaction.Growl;
            }
            else if (validWitch && inputs.WitchForm == WitchForm.Flying && inputs.WitchDistance < WhineRadius)
            {
                Reaction = TylerReaction.Whine;
            }
            else
            {
                Reaction = stillTime >= GuideDelay ? TylerReaction.Guide : TylerReaction.None;
            }

            return Reaction;
        }
    }
}
