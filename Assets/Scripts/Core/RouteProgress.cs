namespace TiffanyGame
{
    public static class RouteProgress
    {
        public static bool StartsReturn(int memoryId) => memoryId == 5;

        public static bool CanCollectMemory(int memoryId, int completedChallenges)
        {
            int required = memoryId == 3 ? 1 : memoryId == 4 ? 3 : memoryId == 5 ? 15 : 0;
            return (completedChallenges & required) == required;
        }
    }
}
