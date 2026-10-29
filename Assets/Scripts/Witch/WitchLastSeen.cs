using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchLastSeen
    {
        public bool HasLocation { get; private set; }
        public Vector3 Location { get; private set; }
        public bool Pending { get; private set; }
        public Vector3 Target { get; private set; }
        private Vector3 visualPosition;
        private bool wasVisible;
        private float elapsed;

        public void Observe(in WitchPerception p, bool grounded, bool trackable, bool playerNoise)
        {
            bool visible = trackable && (p.SeesPlayer || p.SeesPlayerLight);
            if (visible)
            {
                visualPosition = p.SeesPlayer ? p.PlayerPosition : p.PlayerLightPosition;
                Remember(visualPosition);
                Pending = false;
            }
            else if (wasVisible && !p.PlayerInSafeZone && !p.PlayerVanished) Pending = true;
            if (trackable && grounded && p.SmellsPlayer) Remember(p.PlayerPosition);
            else if (trackable && playerNoise && !visible) Remember(p.NoisePosition);
            wasVisible = visible;
            if (p.PlayerVanished) Forget();
            if (p.PlayerInSafeZone) Pending = false;
        }

        private void Remember(Vector3 position)
        {
            Location = position;
            HasLocation = true;
        }

        public bool Begin(Func<Vector3, Vector3> resolve)
        {
            Pending = false;
            elapsed = 0f;
            if (!WitchTargets.TryResolve(resolve, visualPosition, out var point)) return false;
            Target = point;
            return true;
        }

        public bool Tick(float dt, WitchProfile profile)
        {
            elapsed += dt;
            return elapsed < profile.LastSeenDuration;
        }

        public void Cancel() => Pending = false;

        public void Forget()
        {
            HasLocation = Pending = wasVisible = false;
        }
    }
}
