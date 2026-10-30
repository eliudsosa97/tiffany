using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class PlaythroughNavigation
    {
        readonly NavMeshPath path = new NavMeshPath();
        readonly Vector3[] corners = new Vector3[256];
        readonly NavMeshQueryFilter filter = CreateFilter();
        Vector3 target;
        int count, corner;

        public NavMeshPathStatus Status => path.status;
        public int GetCorners(Vector3[] buffer) => path.GetCornersNonAlloc(buffer);

        public bool Calculate(Vector3 position, Vector3 destination)
        {
            target = destination;
            corner = count = 0;
            path.ClearCorners();
            Vector3 start = position;
            if (SafeZones.Contains(position) && !SafeZones.TryPushOut(position, 0.5f, out start)) return false;
            if (SafeZones.Contains(destination) && !SafeZones.TryPushOut(destination, 0.5f, out destination)) return false;
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

        public Vector2 Move(Vector3 position, float yaw, float dt, out bool run, bool forceRun = false)
        {
            while (corner < count && PlaythroughPlan.HorizontalDistance(position, corners[corner]) < 0.3f
                && Mathf.Abs(position.y - corners[corner].y) < 0.8f) corner++;
            // La zona segura recorta el NavMesh de la bruja, pero Tiffany sí puede entrar caminando.
            Vector3 offset = (corner < count ? corners[corner] : target) - position;
            offset.y = 0f;
            float distance = offset.magnitude;
            run = forceRun || distance > 2f;
            float strength = PlayerMotion.InputForSpeed(distance / Mathf.Max(dt, PlayerMotion.SmoothTime),
                run ? Gait.Run : Gait.Walk);
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
