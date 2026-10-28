using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchSenses
    {
        private readonly WitchSenseProfile profile;
        private readonly StepNoise[] pendingNoises;
        private int pendingCount;
        private Vector3 lastWitchPosition;
        private bool hasSensed;

        public WitchSenses(WitchSenseProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            pendingNoises = new StepNoise[8];
        }

        public void Hear(in StepNoise noise)
        {
            if (pendingCount < pendingNoises.Length)
            {
                pendingNoises[pendingCount++] = noise;
                return;
            }

            int smallest = 0;
            float smallestMargin = HearingMargin(in pendingNoises[0]);
            for (int i = 1; i < pendingCount; i++)
            {
                float margin = HearingMargin(in pendingNoises[i]);
                if (margin < smallestMargin)
                {
                    smallest = i;
                    smallestMargin = margin;
                }
            }
            if (HearingMargin(in noise) > smallestMargin) pendingNoises[smallest] = noise;
        }

        public WitchPerception Sense(in WitchSenseInputs input)
        {
            lastWitchPosition = input.WitchPosition;
            hasSensed = true;
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
                NoisePosition = noisePosition
            };
        }

        private float HearingMargin(in StepNoise noise)
        {
            return hasSensed ? noise.Radius - HorizontalDistance(lastWitchPosition, noise.Position) : noise.Radius;
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
