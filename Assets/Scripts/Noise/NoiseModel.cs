using System;

namespace TiffanyGame
{
    public static class NoiseModel
    {
        public const float DirtRadius = 2f;
        public const float GrassRadius = 4f;
        public const float SlabRadius = 4f;
        public const float GravelRadius = 8f;
        public const float LeavesRadius = 14f;
        public const float BranchesRadius = 20f;
        public const float RunMultiplier = 2f;
        public const float SlowMultiplier = 0.5f;
        public const float RainMultiplier = 0.5f;

        public static float StepRadius(GroundType ground, Gait gait, bool raining)
        {
            float radius = ground switch
            {
                GroundType.Dirt => DirtRadius,
                GroundType.Grass => GrassRadius,
                GroundType.Slab => SlabRadius,
                GroundType.Gravel => GravelRadius,
                GroundType.Leaves => LeavesRadius,
                GroundType.Branches => BranchesRadius,
                _ => throw new ArgumentOutOfRangeException(nameof(ground))
            };

            float gaitMultiplier = gait switch
            {
                Gait.Slow => SlowMultiplier,
                Gait.Walk => 1f,
                Gait.Run => RunMultiplier,
                _ => throw new ArgumentOutOfRangeException(nameof(gait))
            };

            return radius * gaitMultiplier * (raining ? RainMultiplier : 1f);
        }
    }
}
