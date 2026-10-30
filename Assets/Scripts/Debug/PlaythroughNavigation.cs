using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class PlaythroughNavigation
    {
        readonly NavMeshPath path = new NavMeshPath();
        readonly Vector3[] segmentCorners = new Vector3[256];
        readonly Vector3[] corners = new Vector3[256 * RouteLayout.Count];
        readonly Vector3[] clearings;
        readonly NavMeshQueryFilter filter = CreateFilter();
        Vector3 target;
        int count, corner;

        public NavMeshPathStatus Status => path.status;
        public int GetCorners(Vector3[] buffer)
        {
            int copied = Mathf.Min(count, buffer.Length);
            System.Array.Copy(corners, buffer, copied);
            return copied;
        }

        public PlaythroughNavigation(Vector3[] clearings = null) =>
            this.clearings = clearings == null ? System.Array.Empty<Vector3>() : (Vector3[])clearings.Clone();

        public bool Calculate(Vector3 position, Vector3 destination)
        {
            target = destination;
            corner = count = 0;
            path.ClearCorners();
            Vector3 start = position;
            if (SafeZones.Contains(position) && !SafeZones.TryPushOut(position, 0.5f, out start)) return false;
            if (SafeZones.Contains(destination) && !SafeZones.TryPushOut(destination, 0.5f, out destination)) return false;
            int first = NearestClearing(start), last = NearestClearing(destination);
            if (first != last)
            {
                int step = first < last ? 1 : -1;
                for (int i = first + step; i != last; i += step)
                    if (!TryPath(ref start, clearings[i])) { count = 0; return false; }
            }
            if (TryPath(ref start, destination)) return true;
            count = 0;
            return false;
        }

        int NearestClearing(Vector3 position)
        {
            int nearest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < clearings.Length; i++)
            {
                Vector3 offset = clearings[i] - position;
                offset.y = 0f;
                if (offset.sqrMagnitude >= distance) continue;
                distance = offset.sqrMagnitude;
                nearest = i;
            }
            return nearest;
        }

        bool TryPath(ref Vector3 start, Vector3 destination)
        {
            if (SafeZones.Contains(destination) && !SafeZones.TryPushOut(destination, 0.5f, out destination)) return false;
            if (!NavMesh.SamplePosition(start, out var from, 5f, filter)
                || !NavMesh.SamplePosition(destination, out var to, 5f, filter)
                || !NavMesh.CalculatePath(from.position, to.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            int added = path.GetCornersNonAlloc(segmentCorners);
            if (added == 0 || added == segmentCorners.Length || count + added > corners.Length) return false;
            int skip = count > 0 ? 1 : 0;
            System.Array.Copy(segmentCorners, skip, corners, count, added - skip);
            count += added - skip;
            start = to.position;
            return true;
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
