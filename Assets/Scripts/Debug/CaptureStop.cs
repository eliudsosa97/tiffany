using UnityEngine;

namespace TiffanyGame
{
    public class CaptureStop : MonoBehaviour
    {
        public float wait = 1.5f;
        public bool overrideCamera;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation = Quaternion.identity;
    }
}
