using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchSenses
    {
        private readonly WitchSenseProfile profile;
        private StepNoise pendingNoise;
        private bool hasPendingNoise;

        public WitchSenses(WitchSenseProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public void Hear(in StepNoise noise)
        {
            pendingNoise = noise;
            hasPendingNoise = true;
        }

        public WitchPerception Sense(in WitchSenseInputs input)
        {
            float distance = HorizontalDistance(input.WitchPosition, input.PlayerPosition);
            bool flying = input.Form == WitchForm.Flying;
            bool grounded = input.Form == WitchForm.Grounded;
            bool heardNoise = hasPendingNoise && grounded
                && HorizontalDistance(input.WitchPosition, pendingNoise.Position) <= pendingNoise.Radius;
            hasPendingNoise = false;

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
                NoisePosition = heardNoise ? pendingNoise.Position : Vector3.zero
            };
        }

        private static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            from.y = to.y;
            return Vector3.Distance(from, to);
        }
    }
}
