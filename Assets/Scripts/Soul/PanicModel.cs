using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class PanicModel
    {
        public const float StepsThreshold = 0.5f;
        public const float FalseFiresThreshold = 0.75f;
        public const float ChildrenThreshold = 0.9f;
        public const float DarknessRise = 0.012f;
        public const float WitchRiseMax = 0.06f;
        public const float WitchRadius = 20f;
        public const float WhispersRise = 0.03f;
        public const float DarknessMultiplier = 1.5f;
        public const float SafeLightCalm = 0.1f;
        public const float HiddenStillCalm = 0.04f;
        public const float DogCalm = 0.05f;

        public float Level { get; private set; }

        public PanicStage Stage =>
            Level >= ChildrenThreshold ? PanicStage.Children :
            Level >= FalseFiresThreshold ? PanicStage.FalseFires :
            Level >= StepsThreshold ? PanicStage.Steps :
            PanicStage.Calm;

        public void Tick(float dt, in PanicInputs inputs)
        {
            if (dt <= 0f)
            {
                return;
            }

            float rise = 0f;
            float distance = inputs.WitchDistance;
            if (distance >= 0f && distance < WitchRadius)
            {
                rise += WitchRiseMax * (1f - distance / WitchRadius);
            }

            if (inputs.HearingWhispers)
            {
                rise += WhispersRise;
            }

            if (inputs.InDarkness)
            {
                rise = rise * DarknessMultiplier + DarknessRise;
            }

            float calm = (inputs.InSafeLight ? SafeLightCalm : 0f)
                + (inputs.HiddenAndStill ? HiddenStillCalm : 0f)
                + (inputs.DogNear ? DogCalm : 0f);
            Level = Mathf.Clamp01(Level + (rise - calm) * dt);
        }

        public void Spike(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Level = Mathf.Clamp01(Level + amount);
        }

        public void ResetTo(float level)
        {
            Level = Mathf.Clamp01(level);
        }
    }
}
