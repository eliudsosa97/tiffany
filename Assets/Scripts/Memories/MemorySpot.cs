using UnityEngine;

namespace TiffanyGame
{
    public enum MemorySpotKind { Memory, Ball }

    [RequireComponent(typeof(SphereCollider))]
    public sealed class MemorySpot : MonoBehaviour
    {
        public int memoryId;
        public MemorySpotKind kind;
        public GameObject visual;
        public EchoPlayer echoPlayer;

        void Awake() => Initialize();

        void Reset() => Initialize();

        void Initialize()
        {
            var sphere = GetComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.2f;
        }
    }
}
