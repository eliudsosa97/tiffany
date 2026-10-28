using UnityEngine;

namespace TiffanyGame
{
    public readonly struct WitchPose
    {
        public bool FigureVisible { get; }
        public bool BallVisible { get; }
        public bool LightOn { get; }
        public bool Crouched { get; }
        public float FigureLean { get; }

        public WitchPose(bool figureVisible, bool ballVisible, bool lightOn, bool crouched, float figureLean)
        {
            FigureVisible = figureVisible;
            BallVisible = ballVisible;
            LightOn = lightOn;
            Crouched = crouched;
            FigureLean = figureLean;
        }
    }

    public static class WitchPoses
    {
        public static WitchPose For(WitchState state, bool fireLit)
        {
            switch (state)
            {
                case WitchState.Flying:
                    return new WitchPose(false, true, fireLit, false, 0f);
                case WitchState.Landing:
                    return new WitchPose(false, true, false, false, 0f);
                case WitchState.TakingOff:
                    return new WitchPose(false, true, true, false, 0f);
                case WitchState.WindUp:
                    return new WitchPose(true, false, false, true, 0f);
                case WitchState.Sprinting:
                    return new WitchPose(true, false, false, false, 30f);
                case WitchState.Recovering:
                    return new WitchPose(true, false, false, false, 15f);
                default:
                    return new WitchPose(true, false, false, false, 0f);
            }
        }

        public static float FireIntensity(float baseIntensity, float heightAboveGround, float fullHeight)
        {
            if (heightAboveGround <= 0f || fullHeight <= 0f) return 0f;
            float fraction = heightAboveGround / fullHeight;
            return baseIntensity * Mathf.Clamp01(fraction * fraction);
        }
    }
}
