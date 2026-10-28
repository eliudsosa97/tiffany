namespace TiffanyGame
{
    public sealed class Tyler
    {
        public const float GrowlRadius = 20f;
        public const float WhineRadius = 30f;
        public const float GuideDelay = 3f;
        public const float MimicDuration = 1.5f;
        public const float MimicInterval = 3f;

        private float stillTime;
        private float mimicRemaining;
        private float mimicCooldown;

        public TylerReaction Reaction { get; private set; }

        public TylerReaction Tick(float dt, in TylerInputs inputs)
        {
            if (dt < 0f || float.IsNaN(dt))
            {
                dt = 0f;
            }

            stillTime = inputs.PlayerStill ? stillTime + dt : 0f;
            bool validWitch = inputs.WitchDistance >= 0f && !float.IsInfinity(inputs.WitchDistance);
            mimicRemaining = System.Math.Max(0f, mimicRemaining - dt);
            mimicCooldown = System.Math.Max(0f, mimicCooldown - dt);
            if (inputs.HeardMimic && validWitch && inputs.WitchDistance < WhineRadius && mimicCooldown <= 0f)
            {
                mimicRemaining = MimicDuration;
                mimicCooldown = MimicInterval;
            }
            if (mimicRemaining > 0f
                || (validWitch && inputs.WitchForm == WitchForm.Grounded && inputs.WitchDistance < GrowlRadius))
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
