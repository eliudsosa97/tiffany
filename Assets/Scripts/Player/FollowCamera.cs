using UnityEngine;

namespace ElVelador
{
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 36f;
        public float pitch = 42f;
        public float yaw = 45f;
        public float fieldOfView = 24f;
        public float lookAhead = 2.5f;
        public float focusHeight = 0.8f;
        public float smoothTime = 0.25f;

        Vector3 focus;
        Vector3 focusVelocity;

        void Awake()
        {
            GetComponent<Camera>().fieldOfView = fieldOfView;
        }

        void OnEnable()
        {
            Snap();
        }

        void LateUpdate()
        {
            if (target == null) return;

            focus = Vector3.SmoothDamp(focus, DesiredFocus(), ref focusVelocity, smoothTime);
            ApplyPose();
        }

        public void Snap()
        {
            if (target == null) return;

            focus = DesiredFocus();
            focusVelocity = Vector3.zero;
            ApplyPose();
        }

        Vector3 DesiredFocus()
        {
            var forward = target.forward;
            forward.y = 0f;
            forward.Normalize();
            return target.position + Vector3.up * focusHeight + forward * lookAhead;
        }

        void ApplyPose()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
