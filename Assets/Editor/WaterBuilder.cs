using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class WaterBuilder
    {
        public const float RiverWidth = RouteTerrain.RiverWidth;
        public const float CrossingWidth = RouteTerrain.CrossingWidth;
        public const float StoneLength = 0.9f;
        public const float StoneTop = 0.12f;

        public static void BuildLake(Transform clearing, Material floor)
        {
            Bank("Ground", clearing, Vector3.zero, new Vector2(36f, 36f), GroundType.Grass, floor);
            var center = RouteTerrain.LakeCenter;
            var origin = RouteLayout.Clearing(5);
            Water("Water", clearing, new Vector3(center.x - origin.x, -0.15f, center.y - origin.z),
                RouteTerrain.LakeRadius * 2f, "lake_loop");
            int segments = RouteTerrain.LakeSegments;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                Vector2 point = RouteTerrain.LakeShorePoint(i * Mathf.PI * 2f / segments) - center;
                vertices[i + 1] = new Vector3(point.x, 0f, point.y);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var water = clearing.Find("Water");
            water.localScale = Vector3.one;
            water.GetComponent<MeshFilter>().sharedMesh = TerrainBuilder.SaveMesh("Lake Water", vertices, triangles);
            water.GetComponent<AudioSource>().maxDistance = 120f;
            Capture("Water 1 Lake", clearing, new Vector3(2.3f, 0f, 1f), 240f,
                new Vector3(-40f, 38f, 12f), new Vector3(-40f, 0.4f, -12f));
        }

        public static void BuildRiver(Transform path, float pathWidth, float pathLength, Material floor, Material stone)
        {
            float bankLength = (pathLength - RiverWidth) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                Bank(side < 0 ? "South Bank" : "North Bank", path,
                    new Vector3(0f, 0f, side * (RiverWidth + bankLength) * 0.5f),
                    new Vector2(pathWidth, bankLength), GroundType.Dirt, floor);
            var river = Root("River", path, Vector3.zero);
            Water("Water", river, new Vector3(0f, -0.15f, 0f), new Vector2(12f, RiverWidth), "river_loop", true);
            float blockedWidth = (pathWidth - CrossingWidth) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
                Block($"Water Block {side}", river, new Vector3(side * (CrossingWidth + blockedWidth) * 0.5f, 1.5f, 0f),
                    new Vector3(blockedWidth, 3f, RiverWidth));
            var stones = CrossingLayout.Create(Vector3.back * RiverWidth * 0.5f, Vector3.forward * RiverWidth * 0.5f, 5);
            for (int i = 0; i < stones.Length; i++)
                CrossingPiece($"Stone {i + 1}", river, stones[i], new Vector3(CrossingWidth, StoneTop * 2f, StoneLength),
                    GroundType.Gravel, stone, i);
            CrossingPiece("Log", river, Vector3.up * 0.03f, new Vector3(CrossingWidth, 0.3f, 1.1f), GroundType.Branches,
                GeneratedMaterials.Get("WaterLog", new Color(0.19f, 0.105f, 0.045f)));
            Capture("Water 2 Crossing", river, stones[0] + Vector3.up * StoneTop, 330f,
                new Vector3(-13f, 19f, 5.5f), new Vector3(0f, 0.4f, 0f));
        }

        static void CrossingPiece(string name, Transform parent, Vector3 position, Vector3 size, GroundType ground, Material material,
            int index = 0)
        {
            // Los colliders se conservan al sustituir el arte.
            var piece = Root(name, parent, position);
            var collider = piece.gameObject.AddComponent<BoxCollider>();
            collider.size = size;
            piece.gameObject.AddComponent<GroundSurface>().type = ground;
            bool log = ground == GroundType.Branches;
            float height = 0.46f + index % 3 * 0.06f;
            var visualSize = log ? new Vector3(size.y, size.x * 0.5f, size.y)
                : new Vector3(size.x * (0.92f + index * 3 % 5 * 0.035f), height, size.z * (0.94f + index * 2 % 5 * 0.02f));
            var visual = SceneBuilder.CreatePrimitive("Visual", log ? PrimitiveType.Cylinder : PrimitiveType.Sphere,
                piece, log ? Vector3.zero : Vector3.up * ((size.y - height) * 0.5f), visualSize, material);
            visual.transform.localRotation = log ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.Euler(0f, -23f + index * 37 % 53, 0f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
        }

        static void Water(string name, Transform parent, Vector3 position, Vector2 size, string clip, bool river = false)
        {
            var water = SceneBuilder.CreatePrimitive(name, PrimitiveType.Plane, parent, position,
                new Vector3(size.x * 0.1f, 1f, size.y * 0.1f), GeneratedMaterials.GetWater(river));
            Object.DestroyImmediate(water.GetComponent<Collider>());
            var source = water.AddComponent<AudioSource>();
            source.clip = AudioBuilder.LoadClip(clip);
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 36f;
            source.maxDistance = 60f;
            source.dopplerLevel = 0f;
            source.volume = 0.5f;
        }

        static void Capture(string name, Transform parent, Vector3 position, float yaw, Vector3 camera, Vector3 focus)
        {
            var stop = Root(name, parent, position);
            stop.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var capture = stop.gameObject.AddComponent<CaptureStop>();
            capture.overrideCamera = true;
            capture.cameraPosition = parent.TransformPoint(camera);
            capture.cameraRotation = Quaternion.LookRotation(parent.TransformPoint(focus) - capture.cameraPosition);
            stop.gameObject.AddComponent<LightCaptureStage>();
        }

        static Transform Root(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            return root;
        }

        static void Bank(string name, Transform parent, Vector3 position, Vector2 size, GroundType ground, Material material)
        {
            var floor = SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, parent, position + Vector3.up * (TerrainBuilder.FloorOffset - 0.03f),
                new Vector3(size.x, 0.06f, size.y), material);
            floor.AddComponent<GroundSurface>().type = ground;
            TerrainBuilder.HideFloor(floor);
            if (ground == GroundType.Dirt) floor.GetComponent<Renderer>().enabled = true;
        }

        static void Block(string name, Transform parent, Vector3 position, Vector3 size) =>
            SceneBuilder.CreateBorder(name, parent, parent.TransformPoint(position), parent.rotation, size);
    }
}
