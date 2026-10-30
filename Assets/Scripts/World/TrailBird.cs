using UnityEngine;

namespace TiffanyGame
{
    public sealed class TrailBird : MonoBehaviour
    {
        public int seed;
        BirdBob motion;
        Vector3 restPosition;
        Quaternion restRotation;
        bool initialized;

        void Update() => Apply(Time.deltaTime);

        public void Apply(float dt)
        {
            if (!initialized)
            {
                motion = new BirdBob(seed);
                restPosition = transform.localPosition;
                restRotation = transform.localRotation;
                initialized = true;
            }
            var pose = motion.Tick(dt);
            transform.localPosition = restPosition + restRotation * pose.Offset;
            transform.localRotation = restRotation * pose.Rotation;
        }
    }
}
