namespace TiffanyGame
{
    public static class GameplayObjective
    {
        public static string Text(MemoryProgress progress)
        {
            if (progress == null || !progress.IsFound(1)) return InterfaceTexts.GraveObjective;
            return progress.IsFound(MemoryProgress.BallId) ? InterfaceTexts.BirdsObjective : InterfaceTexts.BallObjective;
        }
    }
}
