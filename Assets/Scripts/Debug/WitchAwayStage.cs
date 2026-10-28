using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchAwayStage : MonoBehaviour
    {
        WitchController hiddenWitch;

        public void Apply()
        {
            Clear();
            var witch = FindAnyObjectByType<WitchController>();
            if (witch == null || !witch.gameObject.activeSelf) return;
            hiddenWitch = witch;
            hiddenWitch.gameObject.SetActive(false);
        }

        public void Clear()
        {
            if (hiddenWitch != null) hiddenWitch.gameObject.SetActive(true);
            hiddenWitch = null;
        }
    }
}
