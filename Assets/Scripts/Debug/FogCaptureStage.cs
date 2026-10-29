using UnityEngine;

namespace TiffanyGame
{
    public sealed class FogCaptureStage : MonoBehaviour
    {
        public GroundFog fog;
        public bool fogEnabled = true;

        bool wasActive;
        bool wasFoggy;
        bool applied;

        public bool Apply()
        {
            Clear();
            if (fog == null) return false;
            wasActive = fog.gameObject.activeSelf;
            wasFoggy = RenderSettings.fog;
            applied = true;
            fog.gameObject.SetActive(fogEnabled);
            RenderSettings.fog = fogEnabled;
            return true;
        }

        public void Clear()
        {
            if (!applied) return;
            applied = false;
            if (fog != null) fog.gameObject.SetActive(wasActive);
            RenderSettings.fog = wasFoggy;
        }

        void OnDisable() => Clear();
    }
}
