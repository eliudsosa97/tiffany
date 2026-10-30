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
        private bool turnedOn;

        public void Apply(Transform player)
        {
            Clear();
            playerLight = player.GetComponent<PlayerLight>();
            soul = player.GetComponent<PlayerSoul>();
            if (playerLight == null || soul == null) return;
            soul.Powers.Unlock(Power.Light);
            soul.Energy.ResetTo(energy);
            turnedOn = !playerLight.State.IsOn;
            if (turnedOn) playerLight.Toggle();
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
            if (soul != null) soul.Energy.ResetTo(SoulEnergy.Max);
            if (turnedOn && playerLight != null && playerLight.State.IsOn) playerLight.Toggle();
            turnedOn = false;
            if (hiddenWitch != null)
            {
                if (playerLight.State.Visible) StartCoroutine(ShowWitchWhenDark(hiddenWitch));
                else ShowWitch(hiddenWitch);
            }
            hiddenWitch = null;
        }

        // Con la luz que vio en el último cuadro, la bruja la fija como objetivo y aterriza.
        private IEnumerator ShowWitchWhenDark(WitchController witch)
        {
            while (playerLight.State.Visible) yield return null;
            ShowWitch(witch);
        }

        private static void ShowWitch(WitchController witch)
        {
            witch.playerLightOn = false;
            witch.gameObject.SetActive(true);
        }

        private IEnumerator FlashBeforeCapture()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, GetComponent<CaptureStop>().wait - SoulLight.FlashDuration * 0.5f));
            if (soul.Energy.Current < SoulCosts.Flash) soul.Energy.ResetTo(SoulEnergy.Max);
            playerLight.TryBurst();
            flashRoutine = null;
        }
    }
}
