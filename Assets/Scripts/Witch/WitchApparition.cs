using UnityEngine;
namespace TiffanyGame
{
    [DefaultExecutionOrder(202)]
    public sealed class WitchApparition : MonoBehaviour
    {
        public Transform player;
        public WitchController witch;
        public PlayerLight playerLight;
        public Renderer[] renderers = System.Array.Empty<Renderer>();
        public AudioSource source;
        public AudioClip knock;
        public int clearing;
        private readonly ApparitionRules rules = new ApparitionRules();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock properties;
        private Color color;
        private float appliedAlpha = -1f;
        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (renderers.Length > 0 && renderers[0] != null && renderers[0].sharedMaterial != null)
                color = renderers[0].sharedMaterial.GetColor(BaseColor);
            ApplyAlpha();
        }
        private void Update()
        {
            if (Time.timeScale == 0f || player == null || rules.State == ApparitionState.Done) return;
            float distance = HorizontalDistance(transform.position, player.position);
            bool active = witch != null && witch.isActiveAndEnabled;
            float witchDistance = active ? HorizontalDistance(transform.position, witch.transform.position) : float.PositiveInfinity;
            bool chasing = active && TensionMusicRules.IsChasing(witch.Decision, witch.Profile.FollowSpeed);
            bool lit = playerLight != null && playerLight.State != null && playerLight.State.Visible && playerLight.spot != null
                && SoulLight.InCone(playerLight.transform.position, playerLight.transform.forward, transform.position,
                    playerLight.spot.range, playerLight.spot.spotAngle * 0.5f);
            ApparitionEvent result = rules.Tick(Time.deltaTime, distance, lit, chasing, witchDistance);
            ApplyAlpha();
            if (result == ApparitionEvent.Appeared)
                Debug.Log($"{GameInfo.LogPrefix} Aparición {clearing}: aparece a {distance:0.0} m");
            else if (result == ApparitionEvent.VanishedWithKnock || result == ApparitionEvent.VanishedQuietly)
            {
                if (result == ApparitionEvent.VanishedWithKnock && source != null && knock != null) source.PlayOneShot(knock);
                string reason = chasing ? "persecución" : witchDistance < ApparitionRules.WitchClearance ? "bruja"
                    : distance > ApparitionRules.ForgetDistance ? "lejos" : distance < ApparitionRules.VanishNear ? "cerca" : "luz";
                Debug.Log($"{GameInfo.LogPrefix} Aparición {clearing}: se desvanece ({reason})");
            }
        }
        private void ApplyAlpha()
        {
            if (appliedAlpha == rules.Alpha) return;
            color.a = appliedAlpha = rules.Alpha;
            properties.SetColor(BaseColor, color);
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                renderer.enabled = appliedAlpha > 0f;
                renderer.SetPropertyBlock(properties);
            }
        }
        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
