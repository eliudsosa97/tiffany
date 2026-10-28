using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class SoulLight
    {
        public const float FlashDuration = 0.2f;
        public const float LowEnergy = 20f;
        public const float MinSize = 0.5f;
        public const float MinFlicker = 0.6f;

        private readonly PowerSet powers;
        private readonly SoulEnergy energy;
        private bool hidden;

        public SoulLight(PowerSet powers, SoulEnergy energy)
        {
            this.powers = powers ?? throw new ArgumentNullException(nameof(powers));
            this.energy = energy ?? throw new ArgumentNullException(nameof(energy));
        }

        public bool IsOn { get; private set; }
        public bool IsFlashing => FlashRemaining > 0f;
        public float FlashRemaining { get; private set; }
        public bool Visible => IsOn || IsFlashing;

        public void SetHidden(bool hidden)
        {
            this.hidden = hidden;
            if (!hidden) return;
            IsOn = false;
            FlashRemaining = 0f;
        }

        public bool CanReadInput(float timeScale) => !hidden && timeScale > 0f;

        public bool Toggle()
        {
            IsOn = !hidden && !IsOn && PowerRules.CanUseLight(powers, energy);
            return IsOn;
        }

        public bool TryFlash(bool witchGrounded, bool witchInCone, bool lineOfSight, out bool stunsWitch)
        {
            stunsWitch = false;
            if (hidden || !PowerRules.TryFlash(powers, energy, witchGrounded, witchInCone, out stunsWitch)) return false;
            stunsWitch &= lineOfSight;
            FlashRemaining = FlashDuration;
            return true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt)) return;
            if (IsOn)
            {
                energy.Drain(dt);
                if (energy.IsEmpty) IsOn = false;
            }
            FlashRemaining = Mathf.Max(0f, FlashRemaining - dt);
        }

        public static float Size(float energy) => Mathf.Lerp(MinSize, 1f, energy / LowEnergy);

        public static float Flicker(float energy, float time)
        {
            if (energy >= LowEnergy) return 1f;
            float amplitude = (1f - MinFlicker) * (1f - Mathf.Clamp01(energy / LowEnergy));
            return 1f - amplitude * (1f - Mathf.Clamp01(Mathf.PerlinNoise(time * 12f, 0f)));
        }

        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 point, float range, float halfAngle)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude == 0f || range < 0f) return false;
            Vector3 offset = point - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude > range * range) return false;
            if (offset.sqrMagnitude == 0f) return true;
            float angle = Vector3.Angle(forward, offset);
            return angle <= halfAngle || Mathf.Approximately(angle, halfAngle);
        }
    }
}
