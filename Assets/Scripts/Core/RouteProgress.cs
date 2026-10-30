namespace TiffanyGame
{
    public static class RouteProgress
    {
        public static bool StartsReturn(int memoryId) => memoryId == 5;

        public static bool CanCollectMemory(int memoryId, int completedChallenges)
        {
            // Los retos son opcionales: ningún recuerdo queda bloqueado.
            return true;
        }
    }
}
