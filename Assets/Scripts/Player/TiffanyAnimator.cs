using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerController))]
    public sealed class TiffanyAnimator : MonoBehaviour
    {
        public Animator animator;
        public Vector2 locomotionContacts = new Vector2(0f, 0.5f);
        public Vector2 leftTurnContacts = new Vector2(0f, 0.5f);
        public Vector2 rightTurnContacts = new Vector2(0f, 0.5f);
        public Vector2 authoredTurnSpeeds = new Vector2(90f, 90f);
        public float flashDuration = 0.7f;

        static readonly int SpeedHash = Animator.StringToHash(TiffanyAnimation.SpeedParameter);
        static readonly int TurnRateHash = Animator.StringToHash(TiffanyAnimation.TurnRateParameter);
        static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
        static readonly int LeftHash = Animator.StringToHash("Base Layer.TurnLeft");
        static readonly int RightHash = Animator.StringToHash("Base Layer.TurnRight");
        static readonly int HideHash = Animator.StringToHash("Base Layer.Hide");
        static readonly int CaughtHash = Animator.StringToHash("Base Layer.Caught");
        static readonly int VanishHash = Animator.StringToHash("Base Layer.Vanish");
        static readonly int EmptyHash = Animator.StringToHash("Arms.Empty");
        static readonly int CarryHash = Animator.StringToHash("Arms.CarryLight");
        static readonly int FlashHash = Animator.StringToHash("Arms.Flash");

        PlayerController player;
        PlayerLight playerLight;
        PlayerHiding hiding;
        PlayerRespawn respawn;
        VanishPower vanish;
        FootContacts contacts;
        PlayerLean lean;
        TiffanyAnimation.ArmsState arms;
        Quaternion restRotation;
        int baseState;
        int armsState;

        void Start()
        {
            player = GetComponent<PlayerController>();
            playerLight = GetComponent<PlayerLight>();
            hiding = GetComponent<PlayerHiding>();
            respawn = GetComponent<PlayerRespawn>();
            vanish = GetComponent<VanishPower>();
            if (animator == null) return;
            restRotation = animator.transform.localRotation;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void OnEnable()
        {
            baseState = armsState = 0;
            contacts = default;
            arms = default;
            lean = default;
        }

        void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f) return;
            bool caught = respawn != null && respawn.Active;
            bool vanished = vanish != null && vanish.State.IsActive;
            bool hidden = hiding != null && hiding.Hidden;
            float speed = player.HorizontalSpeed;
            float angularSpeed = player.AngularSpeed;
            animator.SetFloat(SpeedHash, speed);
            animator.SetFloat(TurnRateHash, TiffanyAnimation.TurnRate(angularSpeed,
                angularSpeed < 0f ? authoredTurnSpeeds.x : authoredTurnSpeeds.y));
            string state = TiffanyAnimation.StateFor(caught, vanished, hidden, speed, angularSpeed);
            ApplyState(state, TiffanyAnimation.BaseLayer, ref baseState);
            var light = playerLight != null ? playerLight.State : null;
            string armState = arms.Advance(caught || vanished || hidden, light != null && light.IsOn,
                light != null ? light.FlashRemaining : 0f, flashDuration, Time.deltaTime);
            ApplyState(armState, TiffanyAnimation.ArmsLayer, ref armsState, arms.RestartFlash);
        }

        void LateUpdate()
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f) return;
            float speed = baseState == WalkHash ? player.HorizontalSpeed : 0f;
            animator.transform.localRotation = restRotation * Quaternion.Euler(0f, 0f,
                lean.Advance(speed, player.AngularSpeed, Time.deltaTime));
            var state = animator.IsInTransition(TiffanyAnimation.BaseLayer)
                ? animator.GetNextAnimatorStateInfo(TiffanyAnimation.BaseLayer)
                : animator.GetCurrentAnimatorStateInfo(TiffanyAnimation.BaseLayer);
            bool stepping = baseState == WalkHash || baseState == LeftHash || baseState == RightHash;
            if (!stepping || !player.isActiveAndEnabled)
            {
                contacts = default;
                return;
            }
            if (state.fullPathHash != baseState) return;
            var phases = baseState == LeftHash ? leftTurnContacts
                : baseState == RightHash ? rightTurnContacts : locomotionContacts;
            int count = contacts.Advance(baseState, state.normalizedTime, phases);
            if (!player.Grounded) return;
            for (int i = 0; i < count; i++) player.PublishFootstep();
        }

        void ApplyState(string state, int layer, ref int previous, bool restart = false)
        {
            int hash = state switch
            {
                TiffanyAnimation.Walk => WalkHash,
                TiffanyAnimation.TurnLeft => LeftHash,
                TiffanyAnimation.TurnRight => RightHash,
                TiffanyAnimation.Hide => HideHash,
                TiffanyAnimation.Caught => CaughtHash,
                TiffanyAnimation.Vanish => VanishHash,
                TiffanyAnimation.Empty => EmptyHash,
                TiffanyAnimation.CarryLight => CarryHash,
                TiffanyAnimation.Flash => FlashHash,
                _ => IdleHash
            };
            if (hash == previous && !restart) return;
            animator.CrossFadeInFixedTime(hash, TiffanyAnimation.BlendFor(state), layer, 0f);
            previous = hash;
        }
    }
}
