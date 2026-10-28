using UnityEngine;

namespace TiffanyGame
{
    public sealed class MemoryCaptureStage : MonoBehaviour
    {
        public MemoryCollector collector;
        public MemorySpot spot;
        public bool collect = true;

        public void Apply()
        {
            var controller = collector.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            collector.transform.SetPositionAndRotation(transform.position, transform.rotation);
            if (controller != null) controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            if (collect) collector.Collect(spot.memoryId);
        }

        public void Clear() { }
    }
}
