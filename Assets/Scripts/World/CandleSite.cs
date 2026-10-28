using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class CandleSite
    {
        public const float DefaultRadius = 3f;

        public Candle Candle { get; }
        public float Radius { get; }
        public bool PlayerInside { get; private set; }

        public CandleSite(Candle candle, float radius)
        {
            Candle = candle ?? throw new ArgumentNullException(nameof(candle));
            if (radius <= 0f || float.IsNaN(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
            Radius = radius;
        }

        public bool Contains(Vector3 point)
        {
            Vector3 offset = point - Candle.Position;
            offset.y = 0f;
            return Candle.IsSafeZone && offset.magnitude < Radius;
        }

        public void Tick(float dt, bool raining, Vector3 playerPosition, SoulEnergy energy, RespawnPoint respawn)
        {
            bool wasInside = PlayerInside;
            Candle.Tick(dt, raining);
            PlayerInside = Contains(playerPosition);
            if (!PlayerInside || Candle.Kind != CandleKind.Family) return;
            if (!wasInside) respawn?.Touch(Candle);
            energy?.Recharge(dt);
        }
    }
}
