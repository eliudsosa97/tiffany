using UnityEngine;

namespace TiffanyGame
{
    public sealed class StormCaptureStage : MonoBehaviour
    {
        public bool raining;
        public bool lightning;

        static Storm storm;

        public void Apply()
        {
            if (storm == null) storm = FindAnyObjectByType<Storm>();
            if (storm == null) return;
            storm.raining = raining;
            if (lightning) storm.Strike();
            Debug.Log($"{GameInfo.LogPrefix} Captura de tormenta: lluvia={raining}, relámpago={lightning}");
        }

        public void Clear()
        {
            if (storm != null) storm.raining = false;
        }
    }
}
