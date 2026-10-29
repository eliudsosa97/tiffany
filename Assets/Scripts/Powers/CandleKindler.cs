using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class CandleKindler : MonoBehaviour
    {
        public CandleSpot[] candles = new CandleSpot[0];

        private PlayerSoul soul;
        private Vector3[] positions;
        private bool[] eligible;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            positions = new Vector3[candles.Length];
            eligible = new bool[candles.Length];
        }

        private void Update()
        {
            if (Time.timeScale != 0f && GameInput.KindlePressed) TryKindle();
        }

        public bool TryKindle()
        {
            for (int i = 0; i < candles.Length; i++)
            {
                CandleSpot spot = candles[i];
                eligible[i] = spot != null && spot.Candle.Kind == CandleKind.Kindled && !spot.Candle.IsLit;
                if (eligible[i]) positions[i] = spot.transform.position;
            }

            int target = KindleTargeting.Nearest(transform.position, positions, eligible, PowerRules.KindleRange);
            if (target < 0) return false;
            var chosen = candles[target];
            if (!PowerRules.TryLightCandle(soul.Powers, soul.Energy, chosen.Candle,
                chosen.storm != null && chosen.storm.raining)) return false;
            Debug.Log($"{GameInfo.LogPrefix} Tiffany enciende una veladora");
            return true;
        }
    }
}
