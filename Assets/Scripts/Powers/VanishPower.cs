using UnityEngine;

namespace TiffanyGame
{
    [RequireComponent(typeof(PlayerSoul))]
    public sealed class VanishPower : MonoBehaviour
    {
        public Renderer[] figure = new Renderer[0];
        public PlayerLight playerLight;
        public WitchController witch;

        public Vanish State { get; } = new Vanish();

        private PlayerSoul soul;
        private PlayerHiding hiding;
        private bool figureShown = true;

        private void Awake()
        {
            soul = GetComponent<PlayerSoul>();
            TryGetComponent(out hiding);
        }

        private void Update()
        {
            State.Tick(Time.deltaTime);
            if (Time.timeScale != 0f && GameInput.FadePressed) TryStart();
            if (witch != null) witch.playerVanished = State.IsActive;
            ApplyVisibility(VanishLook.Visibility(State.Remaining, Vanish.Duration));
        }

        public bool TryStart()
        {
            if (!State.TryStart(soul.Powers, soul.Energy)) return false;
            Debug.Log($"{GameInfo.LogPrefix} Tiffany se desvanece");
            if (witch != null) witch.playerVanished = true;
            return true;
        }

        public void Cancel()
        {
            State.Cancel();
            ApplyVisibility(1f);
            if (witch != null) witch.playerVanished = false;
        }

        private void OnDisable() => Cancel();

        private void ApplyVisibility(float visibility)
        {
            if (playerLight != null) playerLight.visibility = visibility;
            bool shown = VanishLook.FigureShown(visibility, Time.time);
            if (shown == figureShown) return;
            figureShown = shown;
            if (shown && hiding != null && hiding.Hidden) return;
            for (int i = 0; i < figure.Length; i++)
            {
                if (figure[i] != null && figure[i].enabled != shown) figure[i].enabled = shown;
            }
        }
    }
}
