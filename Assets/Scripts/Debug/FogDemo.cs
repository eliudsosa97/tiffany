using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class FogDemo : MonoBehaviour
    {
        public const float WalkSeconds = 8f;
        public int clearingNumber = 1;

        PlayerController player;
        CharacterController character;
        PlayerSoul soul;
        LightCaptureStage lightStage;
        FogCaptureStage fogStage;
        FollowCamera follow;
        Vector3 playerPosition;
        Quaternion playerRotation;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        Vector2? previousMove;
        Vector2? previousAim;
        bool? previousSlow;
        bool? previousRun;
        bool playerEnabled;
        bool followEnabled;
        float energy;
        bool prepared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-fogdemo");
            if (index < 0) return;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int number) || number < 1)
            {
                Debug.LogError($"{GameInfo.LogPrefix} -fogdemo requiere un número de claro, empezando en 1.");
                return;
            }
            new GameObject("FogDemo").AddComponent<FogDemo>().clearingNumber = number;
#endif
        }

        IEnumerator Start()
        {
            var gameInterface = FindAnyObjectByType<GameInterface>();
            if (gameInterface != null) gameInterface.StartGame();
            yield return null;
            var tracker = FindAnyObjectByType<WitchChapterTracker>();
            var fog = FindAnyObjectByType<GroundFog>(FindObjectsInactive.Include);
            if (tracker == null || fog == null || tracker.player == null || tracker.clearings.Length < 2
                || clearingNumber < 1 || clearingNumber > tracker.clearings.Length)
            {
                Debug.LogError($"{GameInfo.LogPrefix} No existe el claro {clearingNumber} o falta la neblina. Regenera la escena.");
                yield break;
            }
            int index = clearingNumber - 1;
            var clearing = tracker.clearings[index];
            foreach (var stage in clearing.GetComponentsInChildren<LightCaptureStage>())
                if (stage.name == clearing.name) { lightStage = stage; break; }
            player = tracker.player.GetComponent<PlayerController>();
            if (lightStage == null || player == null || player.viewCamera == null)
            {
                Debug.LogError($"{GameInfo.LogPrefix} Falta la preparación de luz o cámara para FogDemo {clearingNumber}.");
                yield break;
            }

            character = player.GetComponent<CharacterController>();
            soul = player.GetComponent<PlayerSoul>();
            follow = player.viewCamera.GetComponent<FollowCamera>();
            playerPosition = player.transform.position;
            playerRotation = player.transform.rotation;
            cameraPosition = player.viewCamera.transform.position;
            cameraRotation = player.viewCamera.transform.rotation;
            playerEnabled = player.enabled;
            followEnabled = follow != null && follow.enabled;
            energy = soul.Energy.Current;
            previousMove = GameInput.MoveOverride;
            previousAim = GameInput.AimOverride;
            previousSlow = GameInput.SlowOverride;
            previousRun = GameInput.RunOverride;
            prepared = true;
            bool closesLoop = clearing.Find("Path to Next Clearing") != null;
            int nextIndex = index + 1 < tracker.clearings.Length ? index + 1 : closesLoop ? 0 : index - 1;
            Vector3 next = tracker.clearings[nextIndex].position;
            Vector3 direction = Vector3.ProjectOnPlane(next - clearing.position, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = clearing.forward;
            player.enabled = false;
            Teleport(Vector3.Lerp(clearing.position, next, 0.25f) + Vector3.up * 0.03f, Quaternion.LookRotation(direction));
            player.enabled = true;
            if (follow != null)
            {
                follow.enabled = true;
                follow.Snap();
            }
            Vector3 input = Quaternion.Euler(0f, -player.viewCamera.transform.eulerAngles.y, 0f) * direction;
            GameInput.MoveOverride = Vector2.zero;
            GameInput.AimOverride = new Vector2(input.x, input.z);
            GameInput.SlowOverride = true;
            GameInput.RunOverride = false;
            lightStage.Apply(player.transform);
            fogStage = gameObject.AddComponent<FogCaptureStage>();
            fogStage.fog = fog;
            fogStage.Apply();
            Debug.Log($"{GameInfo.LogPrefix} FogDemo {clearingNumber} iniciado.");
            yield return new WaitForSeconds(1f);
            GameInput.MoveOverride = new Vector2(input.x, input.z);
            yield return new WaitForSeconds(Mathf.Min(WalkSeconds, Vector3.Distance(clearing.position, next) * 0.25f / PlayerMotion.SlowSpeed));
            GameInput.MoveOverride = Vector2.zero;
            Debug.Log($"{GameInfo.LogPrefix} FogDemo {clearingNumber} terminó el paseo.");
        }

        void Teleport(Vector3 position, Quaternion rotation)
        {
            bool enabled = character.enabled;
            character.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            character.enabled = enabled;
            Physics.SyncTransforms();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (!prepared) return;
            prepared = false;
            if (fogStage != null) fogStage.Clear();
            if (lightStage != null) lightStage.Clear();
            if (soul != null) soul.Energy.ResetTo(energy);
            if (player != null)
            {
                player.enabled = false;
                Teleport(playerPosition, playerRotation);
                player.enabled = playerEnabled;
                if (follow != null)
                {
                    follow.enabled = followEnabled;
                    if (followEnabled) follow.Snap();
                }
                if (player.viewCamera != null) player.viewCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            }
            GameInput.MoveOverride = previousMove;
            GameInput.AimOverride = previousAim;
            GameInput.SlowOverride = previousSlow;
            GameInput.RunOverride = previousRun;
        }
    }
}
