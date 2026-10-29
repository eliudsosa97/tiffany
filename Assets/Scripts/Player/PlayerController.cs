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
        TiffanyAnimator tiffanyAnimator;
        VanishPower vanish;
        StepGuarantee steps;
        float horizontalDistance;

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
            tiffanyAnimator = GetComponent<TiffanyAnimator>();
            vanish = GetComponent<VanishPower>();
        }

        void OnEnable()
        {
            motion = default;
            HorizontalSpeed = 0f;
            CurrentGait = Gait.Slow;
            verticalVelocity = 0f;
            facingTurn = default;
            steps = default;
            horizontalDistance = 0f;
        }

        void OnDisable() => HorizontalSpeed = 0f;

        void Update() => Move(Time.deltaTime);

        void Move(float dt)
        {
            var inputGait = PlayerMotion.ResolveGait(GameInput.Slow, GameInput.Run);
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
            horizontalDistance = displacement.magnitude;
            HorizontalSpeed = horizontalDistance / dt;
            CurrentGait = PlayerMotion.NoiseGait(HorizontalSpeed);

            TurnTowardsAim(inputGait, direction, yaw, dt);
        }

        void LateUpdate() => UpdateFootsteps(Time.deltaTime);

        bool CanStep => isActiveAndEnabled && Grounded && (vanish == null || !vanish.State.IsActive);

        void UpdateFootsteps(float dt)
        {
            if (dt <= 0f) return;
            int contacts = tiffanyAnimator != null && tiffanyAnimator.isActiveAndEnabled ? tiffanyAnimator.ReadFootContacts() : 0;
            if (CanStep)
                PublishSteps(steps.Advance(horizontalDistance, CurrentGait, contacts));
            else
                steps = default;
            horizontalDistance = 0f;
        }

        public void PublishFootstep()
        {
            if (!CanStep || Time.deltaTime <= 0f) return;
            PublishSteps(steps.Advance(0f, CurrentGait, 1));
        }

        void PublishSteps(int count)
        {
            if (count == 0) return;
            var ground = FindGround();
            for (int i = 0; i < count; i++)
            {
                NoiseBus.Publish(new StepNoise(transform.position, NoiseModel.StepRadius(ground, CurrentGait, raining)));
                Stepped?.Invoke(ground, CurrentGait);
            }
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
