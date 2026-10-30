using UnityEngine;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-900)]
    public sealed class MotionDemo : MonoBehaviour
    {
        float startedAt;
        int phase = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-motiondemo") < 0) return;
            var gameInterface = FindAnyObjectByType<GameInterface>();
            if (gameInterface != null) gameInterface.StartGame();
            new GameObject("MotionDemo").AddComponent<MotionDemo>();
#endif
        }

        void Awake() => startedAt = Time.time;

        void Start() => GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerSoul>().Powers.Unlock(Power.Light);

        void Update()
        {
            float elapsed = Time.time - startedAt;
            int nextPhase = elapsed < 1f ? 0 : elapsed < 3.5f ? 1 : elapsed < 7f ? 2 : elapsed < 8f ? 3 : elapsed < 10f ? 4
                : elapsed < 12.5f ? 5 : elapsed < 14.5f ? 6 : 7;
            float heading = nextPhase == 1 ? Mathf.Lerp(-30f, 60f, (elapsed - 1f) / 2.5f)
                : nextPhase == 2 ? Mathf.Lerp(60f, 240f, (elapsed - 3.5f) / 3.5f)
                : nextPhase == 3 ? 240f
                : nextPhase == 4 ? Mathf.Lerp(240f, 60f, (elapsed - 8f) / 2f) : 60f;
            float moveHeading = nextPhase == 5 ? heading + 180f : nextPhase == 6 ? heading + 90f : heading;
            var direction = new Vector2(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
            var move = new Vector2(Mathf.Sin(moveHeading * Mathf.Deg2Rad), Mathf.Cos(moveHeading * Mathf.Deg2Rad));
            GameInput.AimOverride = direction;
            GameInput.MoveOverride = nextPhase == 1 || nextPhase == 2 || nextPhase == 5 || nextPhase == 6 ? move : Vector2.zero;
            GameInput.RunOverride = nextPhase == 2;
            GameInput.SlowOverride = false;
            GameInput.LightHeldOverride = phase < 0 ? true : null;
            if (nextPhase == phase) return;
            phase = nextPhase;
            string label = phase switch
            {
                0 => "quieta",
                1 => "camina en curva",
                2 => "corre en curva",
                3 => "frena",
                4 => "gira en el lugar",
                5 => "camina hacia atrás",
                6 => "camina de lado",
                _ => "terminada"
            };
            Debug.Log($"{GameInfo.LogPrefix} MotionDemo {label}");
        }

        void OnDestroy() => GameInput.ClearOverrides();
    }
}
