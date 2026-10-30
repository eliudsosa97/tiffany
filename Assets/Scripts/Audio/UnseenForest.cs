using System.Collections;
using UnityEngine;
namespace TiffanyGame
{
    [DefaultExecutionOrder(201)]
    public sealed class UnseenForest : MonoBehaviour
    {
        public WitchChapterTracker tracker;
        public WitchController witch;
        public TrailTension trailTension;
        public Transform view;
        public AudioSource source;
        public AudioClip[] branches, steps, whispers, knocks, names;
        public int seed = 18;
        private UnseenForestRules rules;
        private RouteTerrain terrain;
        private Vector3[][] paths;
        private static readonly WaitForSeconds StepPause = new WaitForSeconds(0.45f);
        private int branch, step, whisper, knock, nameIndex;
        private bool paused, wasChasing;
        private void Start()
        {
            rules = new UnseenForestRules(seed);
            terrain = new RouteTerrain();
            paths = new Vector3[RouteLayout.Count - 1][];
            for (int i = 0; i < paths.Length; i++) paths[i] = RouteLayout.Path(i);
        }
        private void Update()
        {
            if (Time.timeScale == 0f)
            {
                if (!paused && source != null) source.Pause();
                paused = true;
                return;
            }
            if (paused && source != null) source.UnPause();
            paused = false;
            if (tracker == null || tracker.player == null || view == null || source == null) return;
            bool chasing = witch != null && witch.isActiveAndEnabled
                && TensionMusicRules.IsChasing(witch.Decision, witch.Profile.FollowSpeed);
            if (chasing && !wasChasing) StopEffects();
            wasChasing = chasing;
            Vector3 player = tracker.player.position;
            UnseenZone zone = Zone(player);
            float sinceScare = trailTension != null ? Time.time - trailTension.LastEventTime : float.PositiveInfinity;
            bool nameAvailable = false;
            if (names != null)
                for (int i = 0; i < names.Length; i++)
                    if (names[i] != null) { nameAvailable = true; break; }
            if (!rules.Tick(Time.deltaTime, zone, chasing, sinceScare, nameAvailable)) return;
            Vector3 forward = view.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0f ? forward.normalized : Vector3.forward;
            Vector3 position = player + Quaternion.Euler(0f, rules.Angle, 0f) * forward * rules.Distance;
            position.y = terrain.Height(position.x, position.z) + 1f;
            source.transform.position = position;
            switch (rules.Sound)
            {
                case UnseenSound.Branch: Play(branches, ref branch); break;
                case UnseenSound.Steps: StartCoroutine(PlaySteps()); break;
                case UnseenSound.Whisper: Play(whispers, ref whisper); break;
                case UnseenSound.Knock: Play(knocks, ref knock); break;
                case UnseenSound.Name: Play(names, ref nameIndex); break;
            }
            Debug.Log($"{GameInfo.LogPrefix} Bosque: {rules.Sound} a {rules.Distance:0} m, {rules.Angle:0}°, zona {zone}");
        }
        private UnseenZone Zone(Vector3 player)
        {
            if (terrain.Town.Contains(new Vector2(player.x, player.z))) return UnseenZone.Town;
            if (RouteChapters.FindClearing(player, tracker.ClearingBounds) >= 0) return UnseenZone.Trail;
            foreach (var path in paths)
                for (int i = 1; i < path.Length; i++)
                    if (FogDensity.DistanceToSegment(player, path[i - 1], path[i]) <= RouteLayout.PathWidth) return UnseenZone.Trail;
            return UnseenZone.Forest;
        }
        private IEnumerator PlaySteps()
        {
            for (int i = 0; i < 3; i++)
            {
                Play(steps, ref step);
                if (i < 2) yield return StepPause;
            }
        }
        private void Play(AudioClip[] clips, ref int index)
        {
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[index++ % clips.Length];
                if (clip == null) continue;
                source.PlayOneShot(clip);
                return;
            }
        }
        private void OnDisable() => StopEffects();
        private void StopEffects()
        {
            StopAllCoroutines();
            if (source != null) source.Stop();
            paused = false;
        }
    }
}
