using UnityEngine;
using UnityEngine.UI;
using static TiffanyGame.EditorTools.SceneBuilder;

namespace TiffanyGame.EditorTools
{
    public static class RouteMapBuilder
    {
        static readonly Color Ink = new Color(0.26f, 0.14f, 0.07f);
        static readonly Color Highlight = new Color(0.65f, 0.24f, 0.06f);

        public static void Build(GameInterface ui)
        {
            var map = ui.gameObject.AddComponent<RouteMap>();
            ui.routeMap = map;
            map.gameInterface = ui;
            var paper = InterfaceRect("Route Map", ui.transform, Vector2.one * 0.025f, Vector2.one * 0.975f);
            map.paper = paper.gameObject;
            paper.gameObject.AddComponent<Image>().color = new Color(0.66f, 0.53f, 0.34f, 0.96f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Label("Title", paper, font, "El camino a casa", 36, new Vector2(0.2f, 0.9f), new Vector2(0.8f, 0.98f));
            Label("Hint", paper, font, "M / L1 / Esc: cerrar · El mundo sigue corriendo", 24,
                new Vector2(0.05f, 0.01f), new Vector2(0.95f, 0.07f));
            Label("Legend", paper, font, "Doble círculo: siguiente claro · Flecha: Tiffany · Norte arriba", 22,
                new Vector2(0.05f, 0.07f), new Vector2(0.95f, 0.12f));
            var frame = InterfaceRect("Map Frame", paper, new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.9f));
            var area = InterfaceRect("Route", frame, Vector2.zero, Vector2.one);
            var fit = area.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1f;
            var projection = new RouteMapRules();
            for (int i = 0; i < RouteLayout.Count - 1; i++)
            {
                var path = RouteLayout.Path(i);
                Vector3 previous = RouteLayout.Clearing(i);
                for (int j = 0; j <= path.Length; j++)
                {
                    Vector3 point = j < path.Length ? path[j] : RouteLayout.Clearing(i + 1);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, point) / 3f));
                    for (int k = 0; k <= steps; k++)
                    {
                        var dot = At("Path", area, projection.Project(Vector3.Lerp(previous, point, (float)k / steps)));
                        Stroke(dot, Vector2.zero, new Vector2(4f, 4f), 0f, Ink);
                    }
                    previous = point;
                }
            }
            string[] names = { "Cementerio", "Planta de agua", "Vías del tren", "Establo", "Jacal", "Lago", "Carretera", "Casa" };
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                Vector2 position = projection.Project(RouteLayout.Clearing(i));
                Ring(At("Clearing", area, position), 10f, 2f, Ink);
                var label = Label("Name", area, font, names[i] + (i == 7 ? " (meta)" : ""), 22, position, position);
                label.rectTransform.sizeDelta = new Vector2(200f, 38f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            }
            map.nextMarker = At("Next Clearing", area, Vector2.zero);
            Ring(map.nextMarker, 18f, 4f, Highlight);
            map.playerMarker = At("Tiffany", area, Vector2.zero);
            Stroke(map.playerMarker, new Vector2(0f, -3f), new Vector2(4f, 20f), 0f, Highlight);
            Stroke(map.playerMarker, new Vector2(-4f, 4f), new Vector2(4f, 14f), -40f, Highlight);
            Stroke(map.playerMarker, new Vector2(4f, 4f), new Vector2(4f, 14f), 40f, Highlight);
            map.paper.SetActive(false);
        }

        static RectTransform At(string name, Transform parent, Vector2 position) => InterfaceRect(name, parent, position, position);

        static Text Label(string name, Transform parent, Font font, string value, int size, Vector2 min, Vector2 max)
        {
            var text = InterfaceText(name, parent, font, value, size, min, max);
            text.color = Ink;
            return text;
        }

        static void Ring(RectTransform parent, float radius, float width, Color color)
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Stroke(parent, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    new Vector2(width, radius * 0.41f), angle * Mathf.Rad2Deg, color);
            }
        }

        static void Stroke(Transform parent, Vector2 position, Vector2 size, float rotation, Color color)
        {
            var rect = At("Ink", parent, Vector2.one * 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
