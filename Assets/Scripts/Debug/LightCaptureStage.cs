using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class LightCaptureStage : MonoBehaviour
    {
        public float energy = SoulEnergy.Max;
        public bool flash;
        public bool hideWitch = true;

        private PlayerSoul soul;
        private PlayerLight playerLight;
        private WitchController hiddenWitch;
        private Coroutine flashRoutine;

        public void Apply(Transform player)
        {
            Clear();
            playerLight = player.GetComponent<PlayerLight>();
            soul = player.GetComponent<PlayerSoul>();
            if (playerLight == null || soul == null) return;
            soul.Energy.ResetTo(energy);
            if (!playerLight.State.IsOn) playerLight.Toggle();
            if (hideWitch && playerLight.witch != null && playerLight.witch.gameObject.activeSelf)
            {
                hiddenWitch = playerLight.witch;
                hiddenWitch.gameObject.SetActive(false);
            }
            if (flash) flashRoutine = StartCoroutine(FlashBeforeCapture());
        }

        public void Clear()
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = null;
            if (hiddenWitch != null) hiddenWitch.gameObject.SetActive(true);
            hiddenWitch = null;
            if (soul != null) soul.Energy.ResetTo(SoulEnergy.Max);
        }

        private IEnumerator FlashBeforeCapture()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, GetComponent<CaptureStop>().wait - SoulLight.FlashDuration * 0.5f));
            if (soul.Energy.Current < SoulCosts.Flash) soul.Energy.ResetTo(SoulEnergy.Max);
            playerLight.TryFlash();
            flashRoutine = null;
        }
    }
}
