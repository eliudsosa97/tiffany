namespace TiffanyGame
{
    public static class GameplayObjective
    {
        public static bool IsFollowingBirds(MemoryProgress progress) =>
            progress != null && progress.IsFound(1) && progress.IsFound(MemoryProgress.BallId);

        public static string Text(MemoryProgress progress)
        {
            if (progress == null || !progress.IsFound(1)) return InterfaceTexts.GraveObjective;
            return IsFollowingBirds(progress) ? InterfaceTexts.BirdsObjective : InterfaceTexts.BallObjective;
        }
    }
}
