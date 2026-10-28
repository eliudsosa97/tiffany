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
            new GameObject("MotionDemo").AddComponent<MotionDemo>();
#endif
        }

        void Awake() => startedAt = Time.time;

        void Update()
        {
            float elapsed = Time.time - startedAt;
            int nextPhase = elapsed < 1f ? 0 : elapsed < 3f ? 1 : elapsed < 6f ? 2 : elapsed < 8.5f ? 3 : 4;
            GameInput.AimOverride = Vector2.up;
            GameInput.MoveOverride = nextPhase >= 1 && nextPhase <= 3 ? Vector2.down : Vector2.zero;
            GameInput.RunOverride = nextPhase == 2;
            GameInput.SlowOverride = false;
            if (nextPhase == phase) return;
            phase = nextPhase;
            string label = phase switch
            {
                0 => "quieta",
                1 => "camina de espaldas",
                2 => "corre",
                3 => "regresa al apuntado",
                _ => "terminada"
            };
            Debug.Log($"{GameInfo.LogPrefix} MotionDemo {label}");
        }

        void OnDestroy() => GameInput.ClearOverrides();
    }
}
