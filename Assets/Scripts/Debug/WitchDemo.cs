using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-900)]
    public sealed partial class WitchDemo : MonoBehaviour
    {
        public const float NothingDuration = 24f;
        public const float HarassDuration = 24f;
        public bool chase;
        public bool stalk;
        public string mode;
        FollowCamera follow;
        Camera view;
        Storm storm;
        Light fill;
        bool followWasEnabled, stormWasEnabled;
        float previousFieldOfView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-witchdemo");
            if (index < 0) return;
            if (index + 1 >= args.Length || (!IsMode(args[index + 1]) && args[index + 1] != "stalk"))
            {
                Debug.LogError($"{GameInfo.LogPrefix} -witchdemo requiere chase, nothing, stalk, search, ambush, candle, bark, lightsoff o harass.");
                return;
            }
            var demo = new GameObject("WitchDemo").AddComponent<WitchDemo>();
            demo.chase = args[index + 1] == "chase";
            demo.stalk = args[index + 1] == "stalk";
            if (!demo.chase && !demo.stalk && args[index + 1] != "nothing") demo.mode = args[index + 1];
#endif
        }

        IEnumerator Start()
        {
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = false;
            GameInput.LightToggleOverride = GameInput.FlashOverride = GameInput.KindleOverride = false;
            GameInput.PoltergeistOverride = GameInput.FadeOverride = false;
            var gameInterface = FindAnyObjectByType<GameInterface>();
            if (gameInterface != null) gameInterface.StartGame();
            yield return null;

            var player = FindAnyObjectByType<PlayerController>();
            var witch = FindAnyObjectByType<WitchController>();
            var stop = GameObject.Find("Witch 0 Live");
            view = Camera.main;
            if (player == null || witch == null || stop == null || view == null)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo necesita la escena generada completa.");
                yield break;
            }
            if (!string.IsNullOrEmpty(mode))
            {
                yield return RunBehavior(player, witch, stop.transform.position);
                yield break;
            }
            Vector3 origin = stop.transform.position + Vector3.back * 12f;
            if (!NavMesh.SamplePosition(origin, out var playerPoint, 4f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(playerPoint.position + Vector3.right * (stalk ? 12f : 15f), out var witchPoint, 4f, NavMesh.AllAreas))
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo no encontró NavMesh para el encuentro.");
                yield break;
            }
            storm = FindAnyObjectByType<Storm>();
            if (storm != null)
            {
                stormWasEnabled = storm.enabled;
                storm.enabled = false;
            }
            var light = player.GetComponent<PlayerLight>();
            if (light != null && light.State.IsOn) light.Toggle();
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.SetPositionAndRotation(playerPoint.position, Quaternion.LookRotation(Vector3.right));
            character.enabled = true;
            witch.chapter = Chapter.Stable;
            witch.ResetBrain();
            var legacy = witch.Profile.Copy();
            legacy.SearchEnabled = legacy.LastSeenEnabled = legacy.AmbushEnabled = false;
            legacy.CandleWatchEnabled = legacy.BarkLureEnabled = false;
            witch.SetProfile(legacy);
            witch.transform.SetPositionAndRotation(witchPoint.position, Quaternion.LookRotation(Vector3.left));
            witch.playerLightOn = witch.playerHidden = witch.playerVanished = witch.hearingMuted = false;
            witch.raining = true;
            Physics.SyncTransforms();

            follow = view.GetComponent<FollowCamera>();
            if (follow != null)
            {
                followWasEnabled = follow.enabled;
                follow.enabled = false;
            }
            Vector3 midpoint = (playerPoint.position + witchPoint.position) * 0.5f + Vector3.up;
            if (!chase && !stalk) midpoint += Vector3.left * 4f;
            Vector3 cameraPosition = midpoint + (stalk ? new Vector3(0f, 5f, -14f) : new Vector3(0f, 9f, -22f));
            previousFieldOfView = view.fieldOfView;
            view.fieldOfView = 45f;
            view.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(midpoint - cameraPosition));
            fill = new GameObject("Witch Demo Light").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.transform.position = midpoint + new Vector3(0f, 5f, -4f);
            fill.type = LightType.Point;
            fill.range = 28f;
            fill.intensity = 60f;
            fill.color = new Color(0.65f, 0.8f, 1f);
            fill.shadows = LightShadows.None;

            float deadline = Time.time + 4f;
            while (witch.Decision.State != WitchState.Stalking && Time.time < deadline) yield return null;
            witch.raining = false;
            if (witch.Decision.State != WitchState.Stalking)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo no pudo preparar el acecho.");
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
            NoiseBus.Publish(new StepNoise(player.transform.position, 20f));
            Debug.Log($"{GameInfo.LogPrefix} WitchDemo primer ruido de Tiffany.");
            if (!chase && !stalk)
            {
                GameInput.SlowOverride = true;
                GameInput.MoveOverride = PlaythroughNavigation.CameraInput(Vector3.left, view.transform.eulerAngles.y);
                GameInput.AimOverride = GameInput.MoveOverride;
            }
            yield return new WaitForSeconds(1f);
            if (witch.Decision.State != WitchState.Suspicious)
            {
                Debug.LogError($"{GameInfo.LogPrefix} WitchDemo esperaba Suspicious tras el primer ruido.");
                yield break;
            }
            if (chase)
            {
                NoiseBus.Publish(new StepNoise(player.transform.position, 20f));
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo segundo ruido de Tiffany: persecución.");
            }
            else if (stalk)
            {
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo Tiffany se queda quieta: la bruja la acecha.");
            }
            else
            {
                deadline = Time.time + 9f;
                while (playerPoint.position.x - player.transform.position.x < 8f && Time.time < deadline)
                    yield return null;
                GameInput.MoveOverride = Vector2.zero;
                Debug.Log($"{GameInfo.LogPrefix} WitchDemo Tiffany se alejó despacio: la bruja revisa el origen vacío.");
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            RestoreBehavior();
            GameInput.ClearOverrides();
            if (fill != null) Destroy(fill.gameObject);
            if (storm != null) storm.enabled = stormWasEnabled;
            if (view != null && previousFieldOfView > 0f) view.fieldOfView = previousFieldOfView;
            if (follow != null) follow.enabled = followWasEnabled;
        }

        public static bool IsMode(string value) => value == "chase" || value == "nothing" || value == "search"
            || value == "ambush" || value == "candle" || value == "bark" || value == "lightsoff" || value == "harass";
    }
}
