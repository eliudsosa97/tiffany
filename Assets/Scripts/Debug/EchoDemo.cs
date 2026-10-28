using System.Collections;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class EchoDemo : MonoBehaviour
    {
        int memoryId;
        EchoCaptureStage stage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-echodemo");
            if (index < 0) return;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out int id) || id < 1 || id > 5)
            {
                Debug.LogError($"{GameInfo.LogPrefix} -echodemo requiere un número de 1 a 5.");
                return;
            }
            new GameObject("EchoDemo").AddComponent<EchoDemo>().memoryId = id;
#endif
        }

        IEnumerator Start()
        {
            var stages = FindObjectsByType<EchoCaptureStage>(FindObjectsSortMode.None);
            for (int i = 0; i < stages.Length; i++)
                if (stages[i].spot.memoryId == memoryId) { stage = stages[i]; break; }
            if (stage == null || !stage.Apply())
            {
                Debug.LogError($"{GameInfo.LogPrefix} No se pudo iniciar el eco {memoryId}. Regenera la escena.");
                yield break;
            }
            Debug.Log($"{GameInfo.LogPrefix} EchoDemo {memoryId} iniciado.");
            while (stage.spot.echoPlayer.IsPlaying) yield return null;
            stage.Clear();
        }

        void OnDisable()
        {
            if (stage != null) stage.Clear();
        }
    }
}
