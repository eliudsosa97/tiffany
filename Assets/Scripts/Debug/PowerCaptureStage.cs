using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public enum PowerCaptureAction { MoveObject, Vanish }

    public sealed class PowerCaptureStage : MonoBehaviour
    {
        public PowerCaptureAction action = PowerCaptureAction.MoveObject;

        private Coroutine powerRoutine;
        private VanishPower vanish;

        public void Apply(Transform player)
        {
            Clear();
            if (action == PowerCaptureAction.Vanish) vanish = player.GetComponent<VanishPower>();
            powerRoutine = StartCoroutine(UseAfterLight(player));
        }

        public void Clear()
        {
            if (powerRoutine != null) StopCoroutine(powerRoutine);
            powerRoutine = null;
            if (vanish != null) vanish.Cancel();
            vanish = null;
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
            else
            {
                bool moved = player.GetComponent<Poltergeist>().TryMove();
                Debug.Log($"{GameInfo.LogPrefix} Captura de poder: mover objetos {(moved ? "movió" : "no movió")} un objeto");
            }
            powerRoutine = null;
        }
    }
}
