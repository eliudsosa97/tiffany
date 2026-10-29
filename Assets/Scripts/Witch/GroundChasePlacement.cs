using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public static class GroundChasePlacement
    {
        public struct Candidate
        {
            public Vector3 Position;
            public NavMeshPathStatus Status;
            public float PathLength;
        }

        public static int Choose(Candidate[] candidates, int count, Vector3 player, float minDistance)
        {
            int chosen = -1;
            float shortest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Candidate candidate = candidates[i];
                Vector3 separation = candidate.Position - player;
                separation.y = 0f;
                if (separation.sqrMagnitude < minDistance * minDistance
                    || candidate.Status != NavMeshPathStatus.PathComplete || candidate.PathLength >= shortest) continue;
                shortest = candidate.PathLength;
                chosen = i;
            }
            return chosen;
        }
    }
}
