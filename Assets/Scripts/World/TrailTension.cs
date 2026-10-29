using System.Collections;
using UnityEngine;
namespace TiffanyGame
{
    [DefaultExecutionOrder(200)]
    public sealed class TrailTension : MonoBehaviour
    {
        public WitchChapterTracker tracker;
        public PowerChallenge[] challenges = System.Array.Empty<PowerChallenge>();
        public float clearingRadius = 20f;
        public WitchController witch;
        public TylerController tyler;
        public AudioSource oneShot;
        public AudioClip[] phantomSteps, branchSnaps;
        public AudioClip skyFire;
        public Transform skyDecoy;
        private readonly TrailEvents rules = new TrailEvents();
        private static readonly WaitForSeconds StepPause = new WaitForSeconds(0.45f);
        private Camera view;
        private FollowCamera framing;
        private int step, branch;
        private bool paused, wasChasing, wasInChallenge, forestLeft;
        private void Start()
        {
            view = Camera.main;
            if (view != null) framing = view.GetComponent<FollowCamera>();
        }
        private void Update()
        {
            if (Time.timeScale == 0f)
            {
                if (!paused && oneShot != null) oneShot.Pause();
                paused = true;
                return;
            }
            if (paused && oneShot != null) oneShot.UnPause();
            paused = false;
            if (tracker == null || tracker.player == null) return;
            Vector3 position = tracker.player.position;
            bool onTrail = true;
            foreach (var clearing in tracker.clearings)
                if (clearing != null && HorizontalSquared(position - clearing.position) <= clearingRadius * clearingRadius)
                    { onTrail = false; break; }
            float distance = witch != null ? Mathf.Sqrt(HorizontalSquared(position - witch.transform.position)) : float.PositiveInfinity;
            bool chase = witch != null && TensionMusicRules.IsChasing(witch.Decision, witch.Profile.FollowSpeed);
            bool inChallenge = false;
            foreach (var challenge in challenges)
                if (challenge != null && challenge.Underway) { inChallenge = true; break; }
            if ((chase && !wasChasing) || (inChallenge && !wasInChallenge)) StopEffects();
            wasChasing = chase;
            wasInChallenge = inChallenge;
            TrailEvent result = rules.Tick(Time.deltaTime, onTrail, chase, inChallenge, distance, tyler != null && tyler.NearPlayer,
                witch != null && witch.Decision.Form == WitchForm.Flying && distance < TrailEvents.WitchFlyingNear);
            if (result == TrailEvent.None) return;
            Debug.Log($"{GameInfo.LogPrefix} Sendero: {result}");
            switch (result)
            {
                case TrailEvent.PhantomSteps: StartCoroutine(PlaySteps()); break;
                case TrailEvent.BranchSnap: Play(branchSnaps, ref branch, 0.6f); break;
                case TrailEvent.SkyCrossing: if (skyDecoy != null && framing != null) StartCoroutine(CrossSky()); break;
                case TrailEvent.TylerGrowl:
                    tyler.HearForest(position + tracker.player.right * (forestLeft ? -10f : 10f));
                    forestLeft = !forestLeft;
                    break;
            }
        }
        private IEnumerator PlaySteps()
        {
            for (int i = 0; i < 3; i++)
            {
                Play(phantomSteps, ref step, 0.35f);
                if (i < 2) yield return StepPause;
            }
        }
        private void Play(AudioClip[] clips, ref int index, float volume)
        {
            if (oneShot != null && clips != null && clips.Length > 0) oneShot.PlayOneShot(clips[index++ % clips.Length], volume);
        }
        private IEnumerator CrossSky()
        {
            Vector3 forward = view.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector2 offset = CameraFraming.DecoyOffset(framing.distance, framing.pitch, framing.fieldOfView);
            Vector3 center = view.transform.position + view.transform.forward * framing.distance
                + forward * offset.x + Vector3.up * offset.y;
            Vector3 across = view.transform.right * (view.WorldToViewportPoint(center).z
                * Mathf.Tan(framing.fieldOfView * Mathf.Deg2Rad * 0.5f) * view.aspect + framing.lookAhead + 0.5f);
            skyDecoy.position = center - across;
            skyDecoy.gameObject.SetActive(true);
            if (oneShot != null && skyFire != null) oneShot.PlayOneShot(skyFire, 0.25f);
            float elapsed = 0f;
            while (elapsed < 4f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                skyDecoy.position = Vector3.Lerp(center - across, center + across, elapsed / 4f);
            }
            skyDecoy.gameObject.SetActive(false);
        }
        private static float HorizontalSquared(Vector3 offset) => offset.x * offset.x + offset.z * offset.z;
        private void OnDisable() => StopEffects();
        private void StopEffects()
        {
            StopAllCoroutines();
            if (skyDecoy != null) skyDecoy.gameObject.SetActive(false);
            if (oneShot != null) oneShot.Stop();
            paused = false;
        }
    }
}
