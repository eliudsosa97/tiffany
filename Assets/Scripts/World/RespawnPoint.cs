using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class RespawnPoint
    {
        public const float RespawnEnergyFraction = 0.5f;

        public Vector3 Position { get; private set; }
        public bool HasFamilyCandle { get; private set; }
        public float FamilyCandleRadius { get; private set; }

        public RespawnPoint(Vector3 start)
        {
            Position = start;
        }

        public Vector3 ArrivalPosition(float sideOffset) => Position + Vector3.back * sideOffset;

        public bool IsAtArrival(Vector3 position, float sideOffset, float radius)
        {
            Vector3 offset = position - ArrivalPosition(sideOffset);
            offset.y = 0f;
            return offset.sqrMagnitude < radius * radius;
        }

        public void Touch(Candle candle, float radius = CandleSite.DefaultRadius)
        {
            if (candle != null && candle.Kind == CandleKind.Family)
            {
                Position = candle.Position;
                HasFamilyCandle = true;
                FamilyCandleRadius = radius;
            }
        }

        public Vector3 Respawn(SoulEnergy energy)
        {
            if (energy == null)
            {
                throw new ArgumentNullException(nameof(energy));
            }

            energy.ResetTo(SoulEnergy.Max * RespawnEnergyFraction);
            return Position;
        }
    }
}
