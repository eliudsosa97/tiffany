using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class TylerCaptureStage : MonoBehaviour
    {
        public TylerReaction reaction;
        public Vector3 tylerOffset = new Vector3(1.8f, 0f, 1.5f);
        public Vector3 witchOffset = new Vector3(0f, 0f, 10f);
        public bool lightTyler = true;

        private TylerController tyler;
        private Light fillLight;
        private bool applied;
        private bool tylerWasEnabled;

        public void Apply(Transform player)
        {
            Clear();
            if (tyler == null) tyler = FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            if (tyler == null) return;
            if (!tyler.gameObject.activeSelf) tyler.Appear(player.position);
            tylerWasEnabled = tyler.enabled;
            tyler.enabled = false;
            tyler.GetComponent<NavMeshAgent>().enabled = false;
            Vector3 position = GetTylerPosition(player);
            Vector3 target = reaction == TylerReaction.Guide && tyler.nextMemory != null
                ? tyler.nextMemory.position : player.position;
            Vector3 direction = target - position;
            direction.y = 0f;
            tyler.transform.SetPositionAndRotation(position,
                direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity);
            tyler.ShowReaction(reaction, player.position + player.rotation * witchOffset);
            if (lightTyler)
            {
                var camera = Camera.main;
                Vector3 lightDirection = camera != null ? camera.transform.position - position : player.position - position;
                lightDirection.y = 0f;
                fillLight = new GameObject("Tyler Capture Light").AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 4f;
                fillLight.intensity = 2f;
                fillLight.color = new Color(0.65f, 0.8f, 1f);
                fillLight.transform.position = position + lightDirection.normalized * 1.2f + Vector3.up * 1.2f;
            }
            applied = true;
            Debug.Log($"{GameInfo.LogPrefix} Captura de Tyler: {reaction}");
        }

        public Vector3 GetTylerPosition(Transform player) =>
            GroundPosition(player.position + player.rotation * tylerOffset);

        public static Vector3 GroundPosition(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var hit,
                Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) position.y = hit.point.y;
            return position;
        }

        public void Clear()
        {
            if (fillLight != null)
            {
                fillLight.enabled = false;
                Destroy(fillLight.gameObject);
            }
            fillLight = null;
            if (applied && tyler != null) tyler.enabled = tylerWasEnabled;
            applied = false;
        }
    }
}
