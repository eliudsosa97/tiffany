using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public static class NavMeshPlacement
    {
        public static bool TrySample(Vector3 position, float radius, int areaMask, out NavMeshHit hit)
        {
            for (; radius <= 24f; radius *= 2f)
            {
                if (NavMesh.SamplePosition(position, out hit, radius, areaMask)) return true;
            }
            hit = default;
            return false;
        }

        public static bool EnsureOnNavMesh(NavMeshAgent agent, Transform transform, ref bool warned,
            string warningFormat, out bool repositioned)
        {
            repositioned = false;
            if (agent.enabled)
            {
                if (agent.isOnNavMesh) return true;
                agent.enabled = false;
            }
            if (!TrySample(transform.position, 3f, agent.areaMask, out var hit))
            {
                if (!warned)
                {
                    Debug.LogWarning(string.Format(warningFormat, transform.position));
                    warned = true;
                }
                return false;
            }
            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
            repositioned = true;
            if (agent.isOnNavMesh) return true;
            agent.enabled = false;
            return false;
        }
    }
}
