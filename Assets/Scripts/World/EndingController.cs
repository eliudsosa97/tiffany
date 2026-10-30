using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class EndingController : MonoBehaviour
    {
        public PlayerSoul soul;
        public WitchController witch;
        public GameInterface gameInterface;

        public EndingRules Rules { get; } = new EndingRules();
        public event Action CreditsReached;
        bool creditsReported;
        PlayerController player;
        FollowCamera follow;
        Transform view;
        Vector3 fromPosition, toPosition;
        Quaternion fromRotation, toRotation;

        void Start()
        {
            player = soul.GetComponent<PlayerController>();
            var camera = player != null && player.viewCamera != null ? player.viewCamera : Camera.main;
            if (camera == null) return;
            view = camera.transform;
            follow = camera.GetComponent<FollowCamera>();
        }

        void BeginApproach()
        {
            if (player != null) player.enabled = false;
            if (view == null) return;
            fromPosition = view.position;
            fromRotation = view.rotation;
            EndingCamera.ClosePose(soul.transform.position, transform.position, view.eulerAngles.y,
                EndingCamera.DefaultDistance, EndingCamera.DefaultPitch, out toPosition, out toRotation);
            if (follow != null) follow.enabled = false;
        }

        void Update()
        {
            if (Rules.Stage == EndingStage.Idle)
            {
                if (gameInterface.IsFrozen || !Rules.TryManifest(soul.Powers.Has(Power.Manifest),
                    Vector3.Distance(soul.transform.position, transform.position), GameInput.InteractPressed)) return;
                witch.gameObject.SetActive(false);
                BeginApproach();
            }
            bool approaching = Rules.Stage == EndingStage.Approach;
            Rules.Tick(Time.unscaledDeltaTime);
            if (approaching && view != null)
            {
                EndingCamera.Blend(fromPosition, fromRotation, toPosition, toRotation, Rules.ApproachProgress,
                    out var position, out var rotation);
                view.SetPositionAndRotation(position, rotation);
            }
            gameInterface.ShowEnding(Rules);
            if (Rules.Stage == EndingStage.Credits && !creditsReported)
            {
                creditsReported = true;
                CreditsReached?.Invoke();
            }
        }
    }
}
