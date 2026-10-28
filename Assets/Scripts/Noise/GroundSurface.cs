using UnityEngine;

namespace TiffanyGame
{
    public class GroundSurface : MonoBehaviour
    {
        public GroundType type;

        public static GroundType TypeOf(Collider collider)
        {
            return collider != null && collider.TryGetComponent<GroundSurface>(out var surface)
                ? surface.type
                : GroundType.Dirt;
        }
    }
}
