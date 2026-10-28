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
                fillLight = new GameObject("Witch Capture Light").AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.range = 7f;
                fillLight.intensity = 4f;
                fillLight.color = new Color(0.65f, 0.8f, 1f);
                fillLight.transform.position = position + direction.normalized * 2f + Vector3.up * 2f;
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
