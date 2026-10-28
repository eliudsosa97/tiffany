using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class WitchCaptureStage : MonoBehaviour
    {
        public WitchState state;
        public bool fireLit;
        public bool live;
        public Vector3 witchOffset;
        public bool lightWitch = true;

        private static WitchController witch;
        private Light fillLight;

        public void Apply(Transform player)
        {
            Clear();
            if (witch == null) witch = FindAnyObjectByType<WitchController>();
            if (witch == null) return;
            if (live)
            {
                var controller = player.GetComponent<CharacterController>();
                bool controllerWasEnabled = controller != null && controller.enabled;
                if (controller != null) controller.enabled = false;
                if (Physics.Raycast(witch.transform.position, Vector3.down, out var ground,
                    Mathf.Infinity, witch.groundMask, QueryTriggerInteraction.Ignore)) player.position = ground.point;
                if (controller != null) controller.enabled = controllerWasEnabled;
                Physics.SyncTransforms();
                return;
            }
            witch.enabled = false;
            witch.GetComponent<NavMeshAgent>().enabled = false;
            Vector3 position = player.position + player.rotation * witchOffset;
            if (witchOffset.y == 0f && Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var hit,
                Mathf.Infinity, witch.groundMask, QueryTriggerInteraction.Ignore)) position.y = hit.point.y;
            Vector3 direction = player.position - position;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            witch.transform.SetPositionAndRotation(position, rotation);
            witch.ShowPose(state, fireLit);
            if (lightWitch && WitchPoses.For(state, fireLit).FigureVisible)
            {
                var camera = Camera.main;
                Vector3 lightDirection = camera != null ? camera.transform.position - position : direction;
                lightDirection.y = 0f;
                fillLight = new GameObject("Witch Capture Light").AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 5f;
                fillLight.intensity = 8f;
                fillLight.color = new Color(0.65f, 0.8f, 1f);
                fillLight.transform.position = position + lightDirection.normalized * 1.2f + Vector3.up * 1.8f;
            }
            Debug.Log($"{GameInfo.LogPrefix} Captura de la bruja: {state}");
        }

        public void Clear()
        {
            if (fillLight != null) Destroy(fillLight.gameObject);
            fillLight = null;
        }
    }
}
