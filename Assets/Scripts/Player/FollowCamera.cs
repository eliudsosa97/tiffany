using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame
{
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        public const float DefaultPitch = 42f;
        public const float DefaultYaw = 45f;
        public const float DefaultDistance = 36f;
        public const float DefaultLookAhead = 2.5f;
        public const float ChestHeight = 0.8f;
        public const float OcclusionRadius = 1.6f;
        public const float OcclusionSoftness = 0.85f;
        public const float OcclusionFocusMargin = 0.2f;
        public const float OcclusionNearMargin = 0.2f;
        public const float OcclusionMinimumKeep = 0.125f;

        static readonly int FocusId = Shader.PropertyToID("_TiffanyFocusWS");
        static readonly int CameraId = Shader.PropertyToID("_TiffanyCameraWS");
        static readonly int ParametersId = Shader.PropertyToID("_TiffanyOcclusionParams");

        public Transform target;
        public float distance = DefaultDistance;
        public float pitch = DefaultPitch;
        public float yaw = DefaultYaw;
        public float fieldOfView = 24f;
        public float lookAhead = DefaultLookAhead;
        public float focusHeight = 0.8f;
        public float smoothTime = 0.25f;

        Vector3 focus;
        Vector3 focusVelocity;
        Camera viewCamera;

        void Awake()
        {
            viewCamera = GetComponent<Camera>();
            viewCamera.fieldOfView = fieldOfView;
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            Shader.SetGlobalVector(FocusId, Vector4.zero);
        }

        // Las capturas desactivan el seguimiento, pero conservan esta cámara.
        void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == viewCamera) PublishOcclusionFocus();
        }

        public void PublishOcclusionFocus()
        {
            if (target == null)
            {
                Shader.SetGlobalVector(FocusId, Vector4.zero);
                return;
            }
            var chest = target.position + Vector3.up * ChestHeight;
            var position = transform.position;
            Shader.SetGlobalVector(FocusId, new Vector4(chest.x, chest.y, chest.z, OcclusionRadius));
            Shader.SetGlobalVector(CameraId, new Vector4(position.x, position.y, position.z, 1f));
            Shader.SetGlobalVector(ParametersId, new Vector4(OcclusionSoftness, OcclusionFocusMargin,
                OcclusionMinimumKeep, OcclusionNearMargin));
        }

        void OnEnable()
        {
            Snap();
        }

        void LateUpdate()
        {
            if (target == null)
            {
                PublishOcclusionFocus();
                return;
            }

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
            PublishOcclusionFocus();
        }
    }
}
