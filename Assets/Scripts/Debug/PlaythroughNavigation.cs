using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class PlaythroughNavigation
    {
        readonly NavMeshPath path = new NavMeshPath();
        readonly Vector3[] corners = new Vector3[256];
        NavMeshQueryFilter filter = CreateFilter();
        Vector3 target;
        int count, corner;

        public bool Calculate(Vector3 position, Vector3 destination)
        {
            target = destination;
            corner = count = 0;
            Vector3 start = position;
            if (SafeZones.Contains(position))
            {
                Vector3 exit = destination - position;
                exit.y = 0f;
                start += Quaternion.Euler(0f, 45f, 0f) * exit.normalized * 4f;
            }
            int forest = NavMesh.GetAreaFromName(RouteTerrain.ForestArea);
            filter.areaMask = forest < 0 ? NavMesh.AllAreas : NavMesh.AllAreas & ~(1 << forest);
            if (TryPath(start, destination)) return true;
            filter.areaMask = NavMesh.AllAreas;
            return TryPath(start, destination);
        }

        bool TryPath(Vector3 start, Vector3 destination)
        {
            if (!NavMesh.SamplePosition(start, out var from, 5f, filter)
                || !NavMesh.SamplePosition(destination, out var to, 5f, filter)
                || !NavMesh.CalculatePath(from.position, to.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            count = path.GetCornersNonAlloc(corners);
            return count > 0 && count < corners.Length;
        }

        public Vector2 Move(Vector3 position, float yaw, float dt, out bool run)
        {
            while (corner < count && PlaythroughPlan.HorizontalDistance(position, corners[corner]) < 0.3f
                && Mathf.Abs(position.y - corners[corner].y) < 0.8f) corner++;
            // La zona segura recorta el NavMesh de la bruja, pero Tiffany sí puede entrar caminando.
            Vector3 offset = (corner < count ? corners[corner] : target) - position;
            offset.y = 0f;
            float distance = offset.magnitude;
            run = distance > 2f;
            float speed = run ? PlayerMotion.RunSpeed : PlayerMotion.WalkSpeed;
            float strength = Mathf.Clamp01(distance / (speed * Mathf.Max(dt, 0.05f)));
            return CameraInput(offset.normalized * strength, yaw);
        }

        public static bool CanMove(Vector3 position, Vector3 direction, float distance) =>
            NavMesh.SamplePosition(position, out var from, 1f, NavMesh.AllAreas)
            && !NavMesh.Raycast(from.position, from.position + direction * distance, out _, NavMesh.AllAreas);

        public static Vector2 CameraInput(Vector3 worldDirection, float yaw)
        {
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * worldDirection;
            return new Vector2(local.x, local.z);
        }

        public static NavMeshQueryFilter CreateFilter()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            for (int i = 0; i < 32; i++) filter.SetAreaCost(i, NavMesh.GetAreaCost(i));
            int forest = NavMesh.GetAreaFromName(RouteTerrain.ForestArea);
            if (forest >= 0) filter.SetAreaCost(forest, RouteTerrain.ForestCost);
            return filter;
        }
    }
}
