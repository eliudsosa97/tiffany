using UnityEngine;

namespace TiffanyGame
{
    public sealed class PanicCaptureStage : MonoBehaviour
    {
        public float level;

        private PanicController panic;
        private bool applied;
        private bool tylerWasActive;

        public void Apply(Transform player)
        {
            Clear();
            panic = player.GetComponent<PanicController>();
            if (panic == null) return;
            player.GetComponent<PlayerSoul>().Powers.Unlock(Power.Light);
            panic.Model.ResetTo(level);
            if (!panic.playerLight.State.IsOn) panic.playerLight.Toggle();
            if (panic.tyler != null)
            {
                tylerWasActive = panic.tyler.gameObject.activeSelf;
                panic.tyler.gameObject.SetActive(false);
            }
            applied = true;
        }

        public void Clear()
        {
            if (!applied || panic == null) return;
            panic.Model.ResetTo(0f);
            panic.playerLight.intensityScale = 1f;
            if (panic.tyler != null) panic.tyler.gameObject.SetActive(tylerWasActive);
            applied = false;
        }
    }
}
