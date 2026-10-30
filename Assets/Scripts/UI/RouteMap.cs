using UnityEngine;

namespace TiffanyGame
{
    public sealed class RouteMap : MonoBehaviour
    {
        public GameInterface gameInterface;
        public GameObject paper;
        public RectTransform playerMarker;
        public RectTransform nextMarker;
        readonly RouteMapRules rules = new RouteMapRules();

        public bool ReadInput(InterfaceScreen screen, bool echo)
        {
            bool closed = rules.IsOpen && GameInput.MapClosePressed;
            rules.Tick(screen, GameInput.MapPressed, GameInput.MapClosePressed, echo);
            ApplyVisibility();
            return closed;
        }

        public void SetAvailability(InterfaceScreen screen, bool echo)
        {
            rules.Tick(screen, false, false, echo);
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            if (paper.activeSelf != rules.IsOpen) paper.SetActive(rules.IsOpen);
        }

        void LateUpdate()
        {
            SetAvailability(gameInterface.Screen, gameInterface.memories != null && gameInterface.memories.IsEchoActive);
            if (!rules.IsOpen) return;
            var route = gameInterface.route;
            Place(playerMarker, rules.Project(route.transform.InverseTransformPoint(route.player.position)));
            playerMarker.localRotation = Quaternion.Euler(0f, 0f,
                RouteMapRules.Heading(route.transform.InverseTransformDirection(route.player.forward)));
            int next = RouteMapRules.NextClearing(route.LastVisitedClearingIndex, route.HasNextClearing);
            if (nextMarker.gameObject.activeSelf != (next >= 0)) nextMarker.gameObject.SetActive(next >= 0);
            if (next >= 0) Place(nextMarker, rules.Project(RouteLayout.Clearing(next)));
        }

        static void Place(RectTransform marker, Vector2 position)
        {
            marker.anchorMin = marker.anchorMax = position;
            marker.anchoredPosition = Vector2.zero;
        }
    }
}
