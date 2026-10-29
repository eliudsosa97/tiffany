using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchSenses
    {
        private readonly WitchSenseProfile profile;
        private readonly StepNoise[] pendingNoises;
        private readonly Vector3[] hidePoints;
        private int pendingCount;

        public WitchSenses(WitchSenseProfile profile, IReadOnlyList<Vector3> hidePoints = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            pendingNoises = new StepNoise[8];
            this.hidePoints = new Vector3[hidePoints?.Count ?? 0];
            for (int i = 0; i < this.hidePoints.Length; i++) this.hidePoints[i] = hidePoints[i];
        }

        public void Hear(in StepNoise noise, Vector3 witchPosition)
        {
            if (pendingCount < pendingNoises.Length)
            {
                pendingNoises[pendingCount++] = noise;
                return;
            }

            int smallest = 0;
            float smallestMargin = HearingMargin(in pendingNoises[0], witchPosition);
            for (int i = 1; i < pendingCount; i++)
            {
                float margin = HearingMargin(in pendingNoises[i], witchPosition);
                if (margin < smallestMargin)
                {
                    smallest = i;
                    smallestMargin = margin;
                }
            }
            if (HearingMargin(in noise, witchPosition) > smallestMargin) pendingNoises[smallest] = noise;
        }

        public WitchPerception Sense(in WitchSenseInputs input)
        {
            float distance = HorizontalDistance(input.WitchPosition, input.PlayerPosition);
            bool flying = input.Form == WitchForm.Flying;
            bool grounded = input.Form == WitchForm.Grounded;
            bool heardNoise = false;
            Vector3 noisePosition = Vector3.zero;
            float bestMargin = float.NegativeInfinity;
            float bestDistance = float.PositiveInfinity;
            if (grounded)
            {
                for (int i = 0; i < pendingCount; i++)
                {
                    StepNoise noise = pendingNoises[i];
                    float noiseDistance = HorizontalDistance(input.WitchPosition, noise.Position);
                    if (noiseDistance > noise.Radius) continue;
                    float margin = noise.Radius - noiseDistance;
                    if (margin > bestMargin || (margin == bestMargin && noiseDistance < bestDistance))
                    {
                        heardNoise = true;
                        noisePosition = noise.Position;
                        bestMargin = margin;
                        bestDistance = noiseDistance;
                    }
                }
            }
            pendingCount = 0;
            Vector3 noiseHide = Vector3.zero;
            float hideDistance = float.PositiveInfinity;
            if (heardNoise)
            {
                for (int i = 0; i < hidePoints.Length; i++)
                {
                    float candidate = HorizontalDistance(noisePosition, hidePoints[i]);
                    if (candidate > profile.NoiseHideRadius || candidate >= hideDistance) continue;
                    hideDistance = candidate;
                    noiseHide = hidePoints[i];
                }
            }

            return new WitchPerception
            {
                WitchPosition = input.WitchPosition,
                PlayerPosition = input.PlayerPosition,
                PlayerLightPosition = input.PlayerLightPosition,
                PlayerInSafeZone = input.PlayerInSafeZone,
                PlayerVanished = input.PlayerVanished,
                PlayerHidden = input.PlayerHidden,
                Flashed = input.Flashed,
                Raining = input.Raining,
                HearingMuted = input.HearingMuted,
                DistanceToPlayer = distance,
                SeesPlayer = !input.PlayerVanished
                    && ((flying && input.FireLit && !input.PlayerUnderCover && distance <= profile.OverheadSightRadius)
                        || (grounded && input.LineOfSight
                            && distance <= (input.PlayerLightOn ? profile.LitSightDistance : profile.DarkSightDistance))),
                SeesPlayerLight = flying && input.PlayerLightOn && !input.PlayerUnderCover && !input.PlayerVanished
                    && HorizontalDistance(input.WitchPosition, input.PlayerLightPosition) <= profile.LightSightDistance,
                SmellsPlayer = grounded && !input.PlayerVanished && distance <= profile.SmellDistance,
                HeardNoise = heardNoise,
                NoisePosition = noisePosition,
                HasNoiseHide = hideDistance < float.PositiveInfinity,
                NoiseHidePosition = noiseHide
            };
        }

        private static float HearingMargin(in StepNoise noise, Vector3 witchPosition)
        {
            return noise.Radius - HorizontalDistance(witchPosition, noise.Position);
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
