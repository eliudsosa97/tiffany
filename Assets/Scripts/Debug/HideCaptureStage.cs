using UnityEngine;

namespace TiffanyGame
{
    public sealed class HideCaptureStage : MonoBehaviour
    {
        public PlayerHiding player;
        public HideVolume volume;

        public void Apply() => player.Hide(volume);

        public void Clear() => player.Leave();
    }
}
