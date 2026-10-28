using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class PowerCaptureStage : MonoBehaviour
    {
        private Coroutine moveRoutine;

        public void Apply(Transform player)
        {
            Clear();
            moveRoutine = StartCoroutine(MoveAfterLight(player));
        }

        public void Clear()
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        private IEnumerator MoveAfterLight(Transform player)
        {
            yield return null;
            yield return null;
            bool moved = player.GetComponent<Poltergeist>().TryMove();
            Debug.Log($"{GameInfo.LogPrefix} Captura de poder: mover objetos {(moved ? "movió" : "no movió")} un objeto");
            moveRoutine = null;
        }
    }
}
