using System;

namespace ElVelador
{
    public sealed class Throwable
    {
        public const float BottleRadius = 10f;
        public const float CanRadius = 8f;
        public const float PotRadius = 12f;
        public const float BellRadius = 16f;
        public const float CandleRadius = 6f;

        public ThrowableKind Kind { get; }
        public bool Used { get; internal set; }

        public Throwable(ThrowableKind kind)
        {
            NoiseRadius(kind);
            Kind = kind;
        }

        public static float NoiseRadius(ThrowableKind kind)
        {
            return kind switch
            {
                ThrowableKind.Bottle => BottleRadius,
                ThrowableKind.Can => CanRadius,
                ThrowableKind.Pot => PotRadius,
                ThrowableKind.Bell => BellRadius,
                ThrowableKind.Candle => CandleRadius,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        }
    }
}
