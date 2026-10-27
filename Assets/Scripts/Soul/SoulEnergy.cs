using System;
using UnityEngine;

namespace ElVelador
{
    public sealed class SoulEnergy
    {
        public const float Max = 100f;

        public float Current { get; private set; }
        public bool IsEmpty => Current <= 0f;

        public SoulEnergy() : this(Max)
        {
        }

        public SoulEnergy(float start)
        {
            ResetTo(start);
        }

        public bool TrySpend(float amount)
        {
            if (amount < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount > Current)
            {
                return false;
            }

            Current -= amount;
            return true;
        }

        public void Drain(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - SoulCosts.LightPerSecond * dt);
        }

        public void Recharge(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            Current = Mathf.Min(Max, Current + SoulCosts.RechargePerSecond * dt);
        }

        public void ResetTo(float value)
        {
            Current = Mathf.Clamp(value, 0f, Max);
        }
    }
}
