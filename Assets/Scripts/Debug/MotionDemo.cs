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

        void Update()
        {
            float elapsed = Time.time - startedAt;
            int nextPhase = elapsed < 1f ? 0 : elapsed < 3.5f ? 1 : elapsed < 7f ? 2 : elapsed < 8f ? 3 : elapsed < 10f ? 4 : 5;
            float heading = nextPhase == 1 ? Mathf.Lerp(-30f, 60f, (elapsed - 1f) / 2.5f)
                : nextPhase == 2 ? Mathf.Lerp(60f, 240f, (elapsed - 3.5f) / 3.5f)
                : nextPhase == 4 ? Mathf.Lerp(240f, 60f, (elapsed - 8f) / 2f) : 240f;
            var direction = new Vector2(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
            GameInput.AimOverride = direction;
            GameInput.MoveOverride = nextPhase == 1 || nextPhase == 2 ? direction : Vector2.zero;
            GameInput.RunOverride = nextPhase == 2;
            GameInput.SlowOverride = false;
            if (nextPhase == phase) return;
            phase = nextPhase;
            string label = phase switch
            {
                0 => "quieta",
                1 => "camina en curva",
                2 => "corre en curva",
                3 => "frena",
                4 => "gira en el lugar",
                _ => "terminada"
            };
            Debug.Log($"{GameInfo.LogPrefix} MotionDemo {label}");
        }

        void OnDestroy() => GameInput.ClearOverrides();
    }
}
