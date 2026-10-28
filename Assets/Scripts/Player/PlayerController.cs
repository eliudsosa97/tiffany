using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera viewCamera;
        public bool raining;
        public float acceleration = 25f;
        public float turnSpeed = 720f;
        public float gravity = 20f;

        public Gait CurrentGait { get; private set; }

        CharacterController controller;
        StepCadence cadence;
        RaycastHit[] groundHits;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        Collider lastGroundCollider;
        GroundType lastGroundType = GroundType.Dirt;
        AimSource aimSource;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            // A muchos cuadros por segundo cada Move mide menos que el mínimo por defecto (0.001 m).
            controller.minMoveDistance = 0f;
            cadence = new StepCadence();
            groundHits = new RaycastHit[4];
        }

        void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        void Update()
        {
            CurrentGait = PlayerMotion.ResolveGait(GameInput.Slow, GameInput.Run);
            float dt = Time.deltaTime;
            float yaw = viewCamera != null ? viewCamera.transform.eulerAngles.y : 0f;
            var direction = PlayerMotion.CameraRelative(GameInput.Move, yaw);
            var targetVelocity = direction * PlayerMotion.Speed(CurrentGait);
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * dt);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity -= gravity * dt;

            var previousPosition = transform.position;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            var displacement = transform.position - previousPosition;
            displacement.y = 0f;
            if (cadence.Advance(displacement.magnitude, CurrentGait))
            {
                var ground = FindGround();
                NoiseBus.Publish(new StepNoise(transform.position, NoiseModel.StepRadius(ground, CurrentGait, raining)));
            }

            TurnTowardsAim(direction, yaw, dt);
        }

        GroundType FindGround()
        {
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.5f, Vector3.down,
                groundHits, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider == controller || hit.distance >= closestDistance) continue;
                closest = hit.collider;
                closestDistance = hit.distance;
            }

            if (closest == null || closest != lastGroundCollider)
            {
                lastGroundCollider = closest;
                lastGroundType = GroundSurface.TypeOf(closest);
            }

            return lastGroundType;
        }

        void TurnTowardsAim(Vector3 moveDirection, float yaw, float dt)
        {
            aimSource = PlayerAim.NextSource(aimSource, GameInput.AimStick, GameInput.PointerMoved,
                GameInput.AimOverride == null);
            var stickDirection = PlayerMotion.CameraRelative(GameInput.AimStick, yaw);
            var pointerOffset = Vector3.zero;

            if (aimSource == AimSource.Pointer)
            {
                if (viewCamera == null) return;
                var plane = new Plane(Vector3.up, transform.position);
                var ray = viewCamera.ScreenPointToRay(GameInput.PointerPosition);
                if (!plane.Raycast(ray, out float distance)) return;
                pointerOffset = ray.GetPoint(distance) - transform.position;
            }

            if (PlayerAim.TryDirection(aimSource, moveDirection, stickDirection, pointerOffset, out var direction))
            {
                var rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                transform.rotation = Quaternion.RotateTowards(rotation,
                    Quaternion.LookRotation(direction), turnSpeed * dt);
            }
        }
    }
}
