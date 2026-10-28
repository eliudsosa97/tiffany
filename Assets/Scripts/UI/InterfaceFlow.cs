namespace TiffanyGame
{
    public sealed class InterfaceFlow
    {
        public InterfaceScreen Screen { get; private set; } = InterfaceScreen.Playing;
        public bool IsFrozen => Screen != InterfaceScreen.Playing;

        public void PressPause()
        {
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Paused : InterfaceScreen.Playing;
        }

        public void PressNotebook()
        {
            if (Screen == InterfaceScreen.Paused) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Notebook : InterfaceScreen.Playing;
        }
    }
}
