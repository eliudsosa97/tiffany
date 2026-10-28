using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame
{
    public sealed class PlaythroughNavigation
    {
        readonly NavMeshPath path = new NavMeshPath();
        readonly Vector3[] corners = new Vector3[256];
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
            if (!NavMesh.SamplePosition(start, out var from, 5f, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(destination, out var to, 5f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            count = path.GetCornersNonAlloc(corners);
            return count > 0 && count < corners.Length;
        }

        public Vector2 Move(Vector3 position, float yaw, float dt, out bool run)
        {
            while (corner < count && PlaythroughPlan.HorizontalDistance(position, corners[corner]) < 0.3f) corner++;
            // La zona segura recorta el NavMesh de la bruja, pero Tiffany sí puede entrar caminando.
            Vector3 offset = (corner < count ? corners[corner] : target) - position;
            offset.y = 0f;
            float distance = offset.magnitude;
            run = distance > 2f;
            float speed = run ? PlayerMotion.RunSpeed : PlayerMotion.WalkSpeed;
            float strength = Mathf.Clamp01(distance / (speed * Mathf.Max(dt, 0.05f)));
            return CameraInput(offset.normalized * strength, yaw);
        }

        public static Vector2 CameraInput(Vector3 worldDirection, float yaw)
        {
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * worldDirection;
            return new Vector2(local.x, local.z);
        }
    }
}
