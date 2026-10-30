using UnityEngine;

namespace TiffanyGame
{
    public sealed class RouteMapRules
    {
        public const float Margin = 0.12f;
        readonly Vector2 center;
        readonly float span;
        public bool IsOpen { get; private set; }

        public RouteMapRules()
        {
            var bounds = new Bounds(RouteLayout.Clearing(0), Vector3.zero);
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                bounds.Encapsulate(RouteLayout.Clearing(i));
                if (i == RouteLayout.Count - 1) continue;
                var path = RouteLayout.Path(i);
                for (int j = 0; j < path.Length; j++) bounds.Encapsulate(path[j]);
            }
            center = new Vector2(bounds.center.x, bounds.center.z);
            span = Mathf.Max(1f, Mathf.Max(bounds.size.x, bounds.size.z));
        }

        public Vector2 Project(Vector3 position) => Vector2.one * 0.5f
            + (new Vector2(position.x, position.z) - center) * ((1f - 2f * Margin) / span);

        public static float Heading(Vector3 forward) => -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        public static int NextClearing(int lastVisited, bool hasNext) =>
            hasNext && lastVisited >= -1 && lastVisited < RouteLayout.Count - 1 ? lastVisited + 1 : -1;

        public void Tick(InterfaceScreen screen, bool toggle, bool close, bool echo = false)
        {
            if (screen != InterfaceScreen.Playing || echo || close) IsOpen = false;
            else if (toggle) IsOpen = !IsOpen;
        }
    }
}
