using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class RespawnPoint
    {
        public const float RespawnEnergyFraction = 0.5f;

        public Vector3 Position { get; private set; }

        public RespawnPoint(Vector3 start)
        {
            Position = start;
        }

        public void Touch(Candle candle)
        {
            if (candle != null && candle.Kind == CandleKind.Family)
            {
                Position = candle.Position;
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
