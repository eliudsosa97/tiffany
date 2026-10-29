using System;
using UnityEngine;

namespace TiffanyGame
{
    public sealed class EndingController : MonoBehaviour
    {
        public PlayerSoul soul;
        public WitchController witch;
        public GameInterface gameInterface;

        public EndingRules Rules { get; } = new EndingRules();
        public event Action CreditsReached;
        bool creditsReported;

        void Update()
        {
            if (Rules.Stage == EndingStage.Idle)
            {
                if (gameInterface.IsFrozen || !Rules.TryManifest(soul.Powers.Has(Power.Manifest),
                    Vector3.Distance(soul.transform.position, transform.position), GameInput.InteractPressed)) return;
                witch.gameObject.SetActive(false);
            }
            Rules.Tick(Time.unscaledDeltaTime);
            gameInterface.ShowEnding(Rules);
            if (Rules.Stage == EndingStage.Credits && !creditsReported)
            {
                creditsReported = true;
                CreditsReached?.Invoke();
            }
        }
    }
}
