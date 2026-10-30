namespace TiffanyGame
{
    public sealed class InterfaceFlow
    {
        public InterfaceScreen Screen { get; private set; }
        public bool IsFrozen => Screen != InterfaceScreen.Playing;
        public bool HasContinue { get; private set; }
        public bool ContinueSelected { get; private set; }

        public void ConfigureTitle(bool hasValidSave)
        {
            HasContinue = hasValidSave;
            ContinueSelected = hasValidSave;
        }

        public void SelectTitleOption(bool continueGame) => ContinueSelected = HasContinue && continueGame;

        public InterfaceFlow(bool startOnTitle = false)
        {
            Screen = startOnTitle ? InterfaceScreen.Title : InterfaceScreen.Playing;
        }

        public void PressStart()
        {
            if (Screen == InterfaceScreen.Title) Screen = InterfaceScreen.Playing;
        }

        public void PressPause()
        {
            if (Screen == InterfaceScreen.Title || Screen == InterfaceScreen.Ending || Screen == InterfaceScreen.Memory) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Paused : InterfaceScreen.Playing;
        }

        public void PressNotebook()
        {
            if (Screen == InterfaceScreen.Title || Screen == InterfaceScreen.Paused
                || Screen == InterfaceScreen.Ending || Screen == InterfaceScreen.Memory) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Notebook : InterfaceScreen.Playing;
        }

        public void SetMemory(bool active)
        {
            if (active && Screen == InterfaceScreen.Playing) Screen = InterfaceScreen.Memory;
            else if (!active && Screen == InterfaceScreen.Memory) Screen = InterfaceScreen.Playing;
        }

        public void SetEnding(bool active)
        {
            if (active) Screen = InterfaceScreen.Ending;
            else if (Screen == InterfaceScreen.Ending) Screen = InterfaceScreen.Playing;
        }
    }
}
