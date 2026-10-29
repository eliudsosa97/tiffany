using UnityEngine;

namespace TiffanyGame
{
    public class CaptureStop : MonoBehaviour
    {
        public float wait = 1.5f;
        public bool overrideCamera;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation = Quaternion.identity;

        public void ApplyPose(Transform player, FollowCamera follow)
        {
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                bool wasEnabled = controller != null && controller.enabled;
                if (controller != null) controller.enabled = false;
                player.SetPositionAndRotation(transform.position, transform.rotation);
                if (controller != null) controller.enabled = wasEnabled;
                Physics.SyncTransforms();
            }
            if (follow != null) follow.enabled = !overrideCamera;
            if (overrideCamera)
            {
                var camera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
                if (camera != null) camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }
            else if (follow != null) follow.Snap();
        }
    }
}
