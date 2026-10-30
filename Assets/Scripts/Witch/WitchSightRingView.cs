using UnityEngine;

namespace TiffanyGame
{
    public sealed class WitchSightRingView : MonoBehaviour
    {
        public WitchController witch;
        public MeshRenderer ring;
        private WitchSightRing state;
        private MaterialPropertyBlock properties;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Thickness = Shader.PropertyToID("_Thickness");

        private void Awake()
        {
            state = new WitchSightRing(witch.senses, witch.playerLightOn);
            properties = new MaterialPropertyBlock();
            ring.enabled = false;
        }

        private void LateUpdate()
        {
            if (witch == null) { ring.enabled = false; return; }
            Vector3 offset = witch.player != null ? witch.player.position - witch.transform.position : Vector3.positiveInfinity;
            offset.y = 0f;
            state.Tick(Time.deltaTime, witch.isActiveAndEnabled && witch.player != null,
                witch.Decision.Form, witch.playerLightOn, offset.magnitude);
            if (state.Visible)
                transform.SetPositionAndRotation(witch.transform.position + Vector3.up * 0.08f, Quaternion.identity);
            transform.localScale = Vector3.one * state.Radius;
            properties.SetColor(BaseColor, new Color(0.5f, 0.08f, 0.06f, state.Alpha));
            properties.SetFloat(Thickness, 0.35f / Mathf.Max(state.Radius, 0.35f));
            ring.SetPropertyBlock(properties);
            ring.enabled = state.Alpha > 0f;
        }

        private void OnDisable() => ring.enabled = false;
    }
}
