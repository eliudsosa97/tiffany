using UnityEngine;

namespace TiffanyGame
{
    public sealed class EchoCaptureStage : MonoBehaviour
    {
        public MemoryCollector collector;
        public MemorySpot spot;

        PlayerController player;
        CharacterController character;
        FollowCamera follow;
        Camera viewCamera;
        CaptureStop stop;
        GameObject witch;
        GameObject tyler;
        Renderer[] playerRenderers;
        bool[] rendererStates;
        Vector3 playerPosition;
        Quaternion playerRotation;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        bool playerEnabled;
        bool followEnabled;
        bool witchActive;
        bool tylerActive;
        bool spotVisible;
        bool? previousInteract;
        bool applied;

        public bool Apply()
        {
            Clear();
            if (collector.ActiveEcho != null) return false;
            player = collector.GetComponent<PlayerController>();
            character = collector.GetComponent<CharacterController>();
            viewCamera = collector.viewCamera;
            follow = viewCamera.GetComponent<FollowCamera>();
            stop = GetComponent<CaptureStop>();
            playerPosition = player.transform.position;
            playerRotation = player.transform.rotation;
            cameraPosition = viewCamera.transform.position;
            cameraRotation = viewCamera.transform.rotation;
            playerEnabled = player.enabled;
            followEnabled = follow != null && follow.enabled;
            previousInteract = GameInput.InteractOverride;
            GameInput.InteractOverride = false;
            player.enabled = false;
            Teleport(transform.position, transform.rotation);
            if (follow != null) follow.enabled = false;
            viewCamera.transform.SetPositionAndRotation(stop.cameraPosition, stop.cameraRotation);
            playerRenderers = player.GetComponentsInChildren<Renderer>(true);
            rendererStates = new bool[playerRenderers.Length];
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                rendererStates[i] = playerRenderers[i].enabled;
                playerRenderers[i].enabled = false;
            }
            if (collector.tyler != null)
            {
                tyler = collector.tyler.gameObject;
                witch = collector.tyler.witch != null ? collector.tyler.witch.gameObject : null;
            }
            tylerActive = tyler != null && tyler.activeSelf;
            witchActive = witch != null && witch.activeSelf;
            if (tyler != null) tyler.SetActive(false);
            if (witch != null) witch.SetActive(false);
            spotVisible = spot.visual != null && spot.visual.activeSelf;
            if (spot.visual != null) spot.visual.SetActive(false);
            applied = true;
            if (collector.PlayEcho(spot)) return true;
            Clear();
            return false;
        }

        public void Clear()
        {
            if (!applied) return;
            applied = false;
            collector.StopEcho();
            Teleport(playerPosition, playerRotation);
            player.enabled = playerEnabled;
            for (int i = 0; i < playerRenderers.Length; i++)
                if (playerRenderers[i] != null) playerRenderers[i].enabled = rendererStates[i];
            if (follow != null) follow.enabled = followEnabled;
            viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            if (tyler != null) tyler.SetActive(tylerActive);
            if (witch != null) witch.SetActive(witchActive);
            if (spot.visual != null) spot.visual.SetActive(spotVisible);
            GameInput.InteractOverride = previousInteract;
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = character.enabled;
            character.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            character.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        void OnDisable() => Clear();
    }
}
