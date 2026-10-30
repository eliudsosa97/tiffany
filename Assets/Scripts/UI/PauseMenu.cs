namespace TiffanyGame
{
    public enum PauseAction { None, Resume, Respawn, Restart }

    public sealed class PauseMenu
    {
        public const int OptionCount = 3;
        public int Index { get; private set; }
        public bool ConfirmingRestart { get; private set; }

        public void Reset()
        {
            Index = 0;
            ConfirmingRestart = false;
        }

        public void Select(int index)
        {
            if (index < 0 || index >= OptionCount || index == Index) return;
            Index = index;
            ConfirmingRestart = false;
        }

        public void Next() => Select((Index + 1) % OptionCount);
        public void Previous() => Select((Index + OptionCount - 1) % OptionCount);

        public PauseAction Submit()
        {
            if (Index == 0) return PauseAction.Resume;
            if (Index == 1) return PauseAction.Respawn;
            ConfirmingRestart = !ConfirmingRestart;
            return ConfirmingRestart ? PauseAction.None : PauseAction.Restart;
        }
    }
}
