using UnityEngine;

namespace TiffanyGame
{
    public sealed class StormCaptureStage : MonoBehaviour
    {
        public bool raining;
        public bool lightning;

        static Storm storm;
        bool applied;
        bool wasRaining;
        bool followedChapter;

        public void Apply()
        {
            if (storm == null) storm = FindAnyObjectByType<Storm>();
            if (storm == null) return;
            if (!applied)
            {
                wasRaining = storm.raining;
                followedChapter = storm.followChapter;
                applied = true;
            }
            storm.followChapter = false;
            storm.raining = raining;
            if (lightning) storm.Strike();
            Debug.Log($"{GameInfo.LogPrefix} Captura de tormenta: lluvia={raining}, relámpago={lightning}");
        }

        public void Clear()
        {
            if (!applied || storm == null) return;
            storm.raining = wasRaining;
            storm.followChapter = followedChapter;
            applied = false;
        }
    }
}
