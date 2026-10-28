using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public enum PowerCaptureAction { MoveObject, Vanish, Kindle }

    public sealed class PowerCaptureStage : MonoBehaviour
    {
        public PowerCaptureAction action = PowerCaptureAction.MoveObject;

        private Coroutine powerRoutine;
        private VanishPower vanish;
        private Candle[] candlesToExtinguish;
        private PowerSet powers;
        private Power granted;
        private bool alreadyUnlocked;

        public void Apply(Transform player)
        {
            Clear();
            powers = player.GetComponent<PlayerSoul>().Powers;
            granted = action == PowerCaptureAction.Vanish ? Power.Vanish : action == PowerCaptureAction.Kindle ? Power.Candles : Power.Poltergeist;
            alreadyUnlocked = powers.Has(granted);
            powers.Unlock(granted);
            if (action == PowerCaptureAction.Vanish) vanish = player.GetComponent<VanishPower>();
            powerRoutine = StartCoroutine(UseAfterLight(player));
        }

        public void Clear()
        {
            if (powerRoutine != null) StopCoroutine(powerRoutine);
            powerRoutine = null;
            if (vanish != null) vanish.Cancel();
            vanish = null;
            if (powers != null && !alreadyUnlocked) powers.Lock(granted);
            powers = null;
            if (candlesToExtinguish != null)
            {
                for (int i = 0; i < candlesToExtinguish.Length; i++) candlesToExtinguish[i]?.Extinguish();
                candlesToExtinguish = null;
            }
        }

        private IEnumerator UseAfterLight(Transform player)
        {
            yield return null;
            yield return null;
            if (action == PowerCaptureAction.Vanish)
            {
                bool started = vanish.TryStart();
                Debug.Log($"{GameInfo.LogPrefix} Captura de poder: desvanecerse {(started ? "activado" : "no activado")}");
            }
            else if (action == PowerCaptureAction.Kindle)
            {
                var kindler = player.GetComponent<CandleKindler>();
                candlesToExtinguish = new Candle[kindler.candles.Length];
                for (int i = 0; i < kindler.candles.Length; i++)
                {
                    CandleSpot spot = kindler.candles[i];
                    if (spot != null && !spot.Candle.IsLit) candlesToExtinguish[i] = spot.Candle;
                }
                bool kindled = kindler.TryKindle();
                Debug.Log($"{GameInfo.LogPrefix} Captura de poder: veladora {(kindled ? "encendida" : "no encendida")}");
            }
            else
            {
                bool moved = player.GetComponent<Poltergeist>().TryMove();
                Debug.Log($"{GameInfo.LogPrefix} Captura de poder: mover objetos {(moved ? "movió" : "no movió")} un objeto");
            }
            powerRoutine = null;
        }
    }
}
