namespace TiffanyGame
{
    public sealed class InterfaceFlow
    {
        public InterfaceScreen Screen { get; private set; } = InterfaceScreen.Playing;
        public bool IsFrozen => Screen != InterfaceScreen.Playing;

        public void PressPause()
        {
            if (Screen == InterfaceScreen.Ending) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Paused : InterfaceScreen.Playing;
        }

        public void PressNotebook()
        {
            if (Screen == InterfaceScreen.Paused || Screen == InterfaceScreen.Ending) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Notebook : InterfaceScreen.Playing;
        }

        public void SetEnding(bool active)
        {
            if (active) Screen = InterfaceScreen.Ending;
            else if (Screen == InterfaceScreen.Ending) Screen = InterfaceScreen.Playing;
        }
    }
}
