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
        static readonly int WalkBackHash = Animator.StringToHash("Base Layer.WalkBack");
        static readonly int LeftHash = Animator.StringToHash("Base Layer.TurnLeft");
        static readonly int RightHash = Animator.StringToHash("Base Layer.TurnRight");
        static readonly int HideHash = Animator.StringToHash("Base Layer.Hide");
        static readonly int CaughtHash = Animator.StringToHash("Base Layer.Caught");
        static readonly int VanishHash = Animator.StringToHash("Base Layer.Vanish");
        static readonly int EmptyHash = Animator.StringToHash("Arms.Empty");
        static readonly int CarryHash = Animator.StringToHash("Arms.CarryLight");
        static readonly int FlashHash = Animator.StringToHash("Arms.Flash");
        static readonly string[] TwistNames = { "Spine", "Chest", "Neck", "Head" };
        static readonly float[] TwistShare = { 0.3f, 0.35f, 0.15f, 0.2f };
        readonly Transform[] twistBones = new Transform[4];
        readonly Quaternion[] animated = new Quaternion[4], written = new Quaternion[4];

        PlayerController player;
        PlayerLight playerLight;
        PlayerHiding hiding;
        PlayerRespawn respawn;
        VanishPower vanish;
        FootContacts contacts;
        PlayerLean lean;
        LegFacing legs;
        Vector3 previousPosition;
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
            for (int i = 0; i < twistBones.Length; i++) twistBones[i] = FindBone(animator.transform, TwistNames[i]);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void OnEnable()
        {
            baseState = armsState = 0;
            contacts = default;
            arms = default;
            lean = default;
            legs = default;
            previousPosition = transform.position;
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
            string state = TiffanyAnimation.StateFor(caught, vanished, hidden, speed, angularSpeed, legs.Backward);
            ApplyState(state, TiffanyAnimation.BaseLayer, ref baseState);
            var light = playerLight != null ? playerLight.State : null;
            string armState = arms.Advance(caught || vanished || hidden, light != null && light.IsOn,
                light != null ? light.FlashRemaining : 0f, flashDuration, Time.deltaTime);
            ApplyState(armState, TiffanyAnimation.ArmsLayer, ref armsState, arms.RestartFlash);
        }

        void LateUpdate()
        {
            var displacement = transform.position - previousPosition;
            previousPosition = transform.position;
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f) return;
            legs.Advance(transform.eulerAngles.y, displacement / Time.deltaTime, player.CurrentGait == Gait.Run, Time.deltaTime);
            float speed = baseState == WalkHash ? player.HorizontalSpeed : 0f;
            animator.transform.localRotation = Quaternion.Euler(0f, legs.LegsOffset, 0f) * restRotation * Quaternion.Euler(0f, 0f,
                lean.Advance(speed, player.AngularSpeed, Time.deltaTime));
            ApplyTwist();
        }

        static Transform FindBone(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindBone(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        void ApplyTwist()
        {
            for (int i = 0; i < twistBones.Length; i++)
            {
                var bone = twistBones[i];
                if (bone == null) continue;
                if (bone.localRotation.Equals(written[i])) bone.localRotation = animated[i];
                animated[i] = bone.localRotation;
            }
            for (int i = 0; i < twistBones.Length; i++)
            {
                var bone = twistBones[i];
                if (bone == null) continue;
                bone.rotation = Quaternion.AngleAxis(legs.Twist * TwistShare[i], Vector3.up) * bone.rotation;
                written[i] = bone.localRotation;
            }
        }

        public int ReadFootContacts()
        {
            if (animator == null || !animator.isActiveAndEnabled) return 0;
            var state = animator.IsInTransition(TiffanyAnimation.BaseLayer)
                ? animator.GetNextAnimatorStateInfo(TiffanyAnimation.BaseLayer)
                : animator.GetCurrentAnimatorStateInfo(TiffanyAnimation.BaseLayer);
            bool stepping = baseState == WalkHash || baseState == WalkBackHash || baseState == LeftHash || baseState == RightHash;
            if (!stepping || !player.isActiveAndEnabled)
            {
                contacts = default;
                return 0;
            }
            if (state.fullPathHash != baseState) return 0;
            var phases = baseState == LeftHash ? leftTurnContacts
                : baseState == RightHash ? rightTurnContacts : locomotionContacts;
            return contacts.Advance(baseState, state.normalizedTime, phases, baseState == WalkBackHash);
        }

        void ApplyState(string state, int layer, ref int previous, bool restart = false)
        {
            int hash = state switch
            {
                TiffanyAnimation.Walk => WalkHash,
                TiffanyAnimation.WalkBack => WalkBackHash,
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
