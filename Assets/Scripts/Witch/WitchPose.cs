using UnityEngine;

namespace TiffanyGame
{
    public readonly struct WitchPose
    {
        public bool FigureVisible { get; }
        public bool BallVisible { get; }
        public bool LightOn { get; }

        public WitchPose(bool figureVisible, bool ballVisible, bool lightOn)
        {
            FigureVisible = figureVisible;
            BallVisible = ballVisible;
            LightOn = lightOn;
        }
    }

    public static class WitchPoses
    {
        public static WitchPose For(WitchState state, bool fireLit)
        {
            switch (state)
            {
                case WitchState.Flying:
                    return new WitchPose(false, true, fireLit);
                case WitchState.Landing:
                    return new WitchPose(false, true, false);
                case WitchState.TakingOff:
                    return new WitchPose(false, true, true);
                default:
                    return new WitchPose(true, false, false);
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
