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

        public void PressStart(bool showStory = false)
        {
            if (Screen == InterfaceScreen.Title)
                Screen = showStory && !ContinueSelected ? InterfaceScreen.Story : InterfaceScreen.Playing;
        }

        public void FinishStory()
        {
            if (Screen == InterfaceScreen.Story) Screen = InterfaceScreen.Playing;
        }

        public void PressPause()
        {
            if (Screen == InterfaceScreen.Title || Screen == InterfaceScreen.Story
                || Screen == InterfaceScreen.Ending) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Paused : InterfaceScreen.Playing;
        }

        public void PressNotebook()
        {
            if (Screen == InterfaceScreen.Title || Screen == InterfaceScreen.Story || Screen == InterfaceScreen.Paused
                || Screen == InterfaceScreen.Ending) return;
            Screen = Screen == InterfaceScreen.Playing ? InterfaceScreen.Notebook : InterfaceScreen.Playing;
        }

        public void SetEnding(bool active)
        {
            if (active) Screen = InterfaceScreen.Ending;
            else if (Screen == InterfaceScreen.Ending) Screen = InterfaceScreen.Playing;
        }
    }
}
