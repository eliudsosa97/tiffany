using System;
using UnityEngine;

namespace TiffanyGame
{
    public static class NoiseBus
    {
        public static event Action<StepNoise> Published;

        public static void Publish(in StepNoise noise)
        {
            Published?.Invoke(noise);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Published = null;
        }
    }
}
