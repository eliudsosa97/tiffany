using UnityEngine;

namespace TiffanyGame
{
    public sealed class PlayerSoul : MonoBehaviour
    {
        public float startEnergy = SoulEnergy.Max;
        public Power[] startingPowers = new Power[0];

        private SoulEnergy energy;
        private PowerSet powers;
        private RespawnPoint respawn;

        public SoulEnergy Energy => energy ?? (energy = new SoulEnergy(startEnergy));
        public RespawnPoint Respawn => respawn ?? (respawn = new RespawnPoint(transform.position));

        public PowerSet Powers
        {
            get
            {
                if (powers != null) return powers;
                powers = new PowerSet();
                for (int i = 0; i < startingPowers.Length; i++) powers.Unlock(startingPowers[i]);
                return powers;
            }
        }

        private void Awake()
        {
            if (respawn == null) respawn = new RespawnPoint(transform.position);
        }
    }
}
