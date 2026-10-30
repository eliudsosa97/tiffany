using UnityEngine;

namespace TiffanyGame
{
    public sealed class TylerIcon
    {
        public const float BarkDuration = 1.2f;
        private float barkRemaining;
        public bool Barking => barkRemaining > 0f;
        public void Bark() => barkRemaining = BarkDuration;
        public void Reset() => barkRemaining = 0f;

        public string Tick(float dt, TylerReaction reaction)
        {
            barkRemaining = Mathf.Max(0f, barkRemaining - Mathf.Max(0f, dt));
            if (Barking) return "!";
            return reaction switch
            {
                TylerReaction.Growl => "!!",
                TylerReaction.Whine => "...",
                TylerReaction.Guide => "↑",
                _ => ""
            };
        }

        public static Color TintFor(string symbol) => symbol switch
        {
            "!" => Color.yellow,
            "!!" => Color.red,
            "..." => new Color(0.65f, 0.8f, 1f),
            _ => Color.white
        };
    }
}
