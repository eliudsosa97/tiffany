using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(Collider))]
    public sealed class HideVolume : MonoBehaviour
    {
        Collider volumeCollider;

        public Vector3 HidePoint
        {
            get
            {
                if (volumeCollider == null) Initialize();
                var bounds = volumeCollider.bounds;
                return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
        }

        void Awake() => Initialize();

        void Reset() => Initialize();

        void Initialize()
        {
            volumeCollider = GetComponent<Collider>();
            volumeCollider.isTrigger = true;
        }
    }
}
