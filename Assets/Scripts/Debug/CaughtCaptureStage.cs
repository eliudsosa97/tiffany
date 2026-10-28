using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class CaughtCaptureStage : MonoBehaviour
    {
        public CandleSpot spot;
        private PlayerRespawn respawn;

        public void Apply(Transform player)
        {
            var soul = player.GetComponent<PlayerSoul>();
            respawn = player.GetComponent<PlayerRespawn>();
            if (soul == null || respawn == null) return;
            if (spot != null) soul.Respawn.Touch(spot.Candle);
            if (respawn.witch != null) respawn.witch.enabled = true;
            respawn.Trigger();
        }

        public IEnumerator Clear()
        {
            while (respawn != null && respawn.Active) yield return null;
            respawn = null;
        }
    }
}
