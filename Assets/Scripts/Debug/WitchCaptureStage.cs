using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class WitchCaptureStage : MonoBehaviour
    {
        public WitchState state;
        public bool fireLit;
        public Vector3 witchOffset;
        public bool lightWitch = true;

        private static WitchController witch;
        private Light fillLight;

        public void Apply(Transform player)
        {
            Clear();
            if (witch == null) witch = FindAnyObjectByType<WitchController>();
            if (witch == null) return;
            witch.enabled = false;
            witch.GetComponent<NavMeshAgent>().enabled = false;
            Vector3 position = player.position + player.rotation * witchOffset;
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
