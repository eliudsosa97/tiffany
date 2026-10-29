using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera viewCamera;
        public bool raining;
        public float turnSpeed = 720f;
        public float returnTurnSpeed = 300f;
        public float backwardSpeedFactor = 0.7f;
        public float gravity = 20f;

        public Gait CurrentGait { get; private set; }
        public float HorizontalSpeed { get; private set; }
        public float AngularSpeed => isActiveAndEnabled ? facingTurn.AngularSpeed : 0f;
        public bool Grounded => controller.enabled && controller.isGrounded;
        public event System.Action<GroundType, Gait> Stepped;

        CharacterController controller;
        RaycastHit[] groundHits;
        PlayerMotion.VelocityState motion;
        float verticalVelocity;
        Collider lastGroundCollider;
        GroundType lastGroundType = GroundType.Dirt;
        AimSource aimSource;
        PlayerFacing.TurnState facingTurn;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            // A muchos cuadros por segundo cada Move mide menos que el mínimo por defecto (0.001 m).
            controller.minMoveDistance = 0f;
            groundHits = new RaycastHit[4];
        }

        void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        void OnEnable()
        {
            motion = default;
            HorizontalSpeed = 0f;
            CurrentGait = Gait.Slow;
            verticalVelocity = 0f;
            facingTurn = default;
        }

        void OnDisable() => HorizontalSpeed = 0f;

        void Update()
        {
            var inputGait = PlayerMotion.ResolveGait(GameInput.Slow, GameInput.Run);
            float dt = Time.deltaTime;
            if (dt <= 0f || !controller.enabled) return;
            float yaw = viewCamera != null ? viewCamera.transform.eulerAngles.y : 0f;
            var direction = PlayerMotion.CameraRelative(GameInput.Move, yaw);
            float speedFactor = PlayerFacing.SpeedFactor(direction, transform.forward,
                PlayerFacing.FacesMovement(inputGait, direction), backwardSpeedFactor);
            var targetVelocity = direction.normalized * (PlayerMotion.TargetSpeed(direction.magnitude, inputGait) * speedFactor);
            var horizontalVelocity = motion.Advance(targetVelocity, dt);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            else verticalVelocity -= gravity * dt;

            var previousPosition = transform.position;
            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            var displacement = transform.position - previousPosition;
            displacement.y = 0f;
            HorizontalSpeed = displacement.magnitude / dt;
            CurrentGait = PlayerMotion.NoiseGait(HorizontalSpeed);

            TurnTowardsAim(inputGait, direction, yaw, dt);
        }

        public void PublishFootstep()
        {
            if (!isActiveAndEnabled || !Grounded || Time.deltaTime <= 0f) return;
            var ground = FindGround();
            NoiseBus.Publish(new StepNoise(transform.position, NoiseModel.StepRadius(ground, CurrentGait, raining)));
            Stepped?.Invoke(ground, CurrentGait);
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

        void TurnTowardsAim(Gait inputGait, Vector3 moveDirection, float yaw, float dt)
        {
            aimSource = PlayerAim.NextSource(aimSource, GameInput.AimStick, GameInput.PointerMoved,
                GameInput.AimOverride == null);
            bool running = PlayerFacing.FacesMovement(inputGait, moveDirection);
            var stickDirection = PlayerMotion.CameraRelative(GameInput.AimStick, yaw);
            var pointerOffset = Vector3.zero;

            if (!running && aimSource == AimSource.Pointer && viewCamera != null)
            {
                var plane = new Plane(Vector3.up, transform.position);
                var ray = viewCamera.ScreenPointToRay(GameInput.PointerPosition);
                if (plane.Raycast(ray, out float distance)) pointerOffset = ray.GetPoint(distance) - transform.position;
            }

            var direction = moveDirection;
            bool hasDirection = running || PlayerAim.TryDirection(aimSource, moveDirection, stickDirection,
                pointerOffset, out direction);
            float currentYaw = transform.eulerAngles.y;
            float targetYaw = hasDirection ? Quaternion.LookRotation(direction).eulerAngles.y : currentYaw;
            float nextYaw = facingTurn.Advance(currentYaw, targetYaw, running, turnSpeed, returnTurnSpeed, dt);
            transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
        }
    }
}
