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
        public bool shareLight;
        public Light fillLight;

        private TylerController tyler;
        private bool applied;
        private bool tylerWasEnabled;
        private Transform previousMemory;

        public void Apply(Transform player)
        {
            Clear();
            if (tyler == null) tyler = FindAnyObjectByType<TylerController>(FindObjectsInactive.Include);
            if (tyler == null) return;
            if (!tyler.gameObject.activeSelf) tyler.Appear(player.position);
            tylerWasEnabled = tyler.enabled;
            previousMemory = tyler.nextMemory;
            if (shareLight) tyler.nextMemory = null;
            tyler.enabled = shareLight;
            tyler.GetComponent<NavMeshAgent>().enabled = false;
            Vector3 position = GetTylerPosition(player);
            Vector3 target = reaction == TylerReaction.Guide && tyler.nextMemory != null
                ? tyler.nextMemory.position : player.position;
            Vector3 direction = target - position;
            direction.y = 0f;
            tyler.transform.SetPositionAndRotation(position,
                direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity);
            tyler.ShowReaction(reaction, player.position + player.rotation * witchOffset);
            if (lightTyler && !shareLight && fillLight != null)
            {
                var camera = Camera.main;
                Vector3 lightDirection = camera != null ? camera.transform.position - position : player.position - position;
                lightDirection.y = 0f;
                fillLight.transform.position = position + lightDirection.normalized * 1.2f + Vector3.up * 1.2f;
                fillLight.enabled = true;
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
            if (fillLight != null) fillLight.enabled = false;
            if (applied && tyler != null)
            {
                tyler.nextMemory = previousMemory;
                if (shareLight) tyler.enabled = false;
                tyler.enabled = tylerWasEnabled;
            }
            applied = false;
        }
    }
}
