using UnityEngine;

namespace TiffanyGame
{
    public sealed class EndingCaptureStage : MonoBehaviour
    {
        public EndingController ending;
        public bool credits;
        bool wasEnabled;
        bool witchActive;
        bool applied;

        public void Apply()
        {
            Clear();
            wasEnabled = ending.enabled;
            witchActive = ending.witch.gameObject.activeSelf;
            applied = true;
            ending.enabled = false;
            ending.witch.gameObject.SetActive(false);
            var preview = new EndingRules();
            preview.TryManifest(true, 0f, true);
            preview.Tick(EndingRules.ApproachSeconds
                + (credits ? EndingRules.FadeSeconds + EndingRules.TextSeconds : EndingRules.FadeSeconds * 0.5f));
            ending.gameInterface.ShowEnding(preview);
        }

        public void Clear()
        {
            if (!applied) return;
            ending.gameInterface.ShowEnding(ending.Rules);
            ending.witch.gameObject.SetActive(witchActive);
            ending.enabled = wasEnabled;
            applied = false;
        }
    }
}
