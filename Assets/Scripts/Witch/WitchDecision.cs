using UnityEngine;

namespace TiffanyGame
{
    public struct WitchDecision
    {
        public WitchForm Form;
        public WitchState State;
        public Vector3 MoveTarget;
        public float Speed;
        public bool FireLit;
        public bool VoiceLure;
        public bool InspectingHide;
    }
}
