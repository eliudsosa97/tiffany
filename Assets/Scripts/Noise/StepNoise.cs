using UnityEngine;

namespace ElVelador
{
    public readonly struct StepNoise
    {
        public readonly Vector3 Position;
        public readonly float Radius;

        public StepNoise(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }
}
