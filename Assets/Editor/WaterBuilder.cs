using UnityEngine;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class WaterBuilder
    {
        public const float RiverWidth = 4f;
        public const float CrossingWidth = 1.6f;
        public const float StoneLength = 0.9f;
        public const float StoneTop = 0.12f;

        public static void BuildLake(Transform clearing, Material floor)
        {
            Bank("West Bank", clearing, new Vector3(-7.5f, 0f, 0f), new Vector2(21f, 36f), GroundType.Grass, floor);
            Bank("East Bank", clearing, new Vector3(16.5f, 0f, 0f), new Vector2(3f, 36f), GroundType.Grass, floor);
            Bank("South Bank", clearing, new Vector3(9f, 0f, -13f), new Vector2(12f, 10f), GroundType.Grass, floor);
            Bank("North Bank", clearing, new Vector3(9f, 0f, 16f), new Vector2(12f, 4f), GroundType.Grass, floor);
            Water("Water", clearing, new Vector3(9f, -0.15f, 3f), new Vector2(12f, 22f), "lake_loop");
            for (int side = -1; side <= 1; side += 2)
            {
                Block($"Lake Bank X {side}", clearing, new Vector3(9f + side * 6f, 1.5f, 3f), new Vector3(0.5f, 3f, 22.5f));
                Block($"Lake Bank Z {side}", clearing, new Vector3(9f, 1.5f, 3f + side * 11f), new Vector3(12.5f, 3f, 0.5f));
            }
            Capture("Water 1 Lake", clearing, new Vector3(2.3f, 0f, 1f), 60f,
                new Vector3(-20f, 34f, 26f), new Vector3(7f, 0.4f, 5f));
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
            source.minDistance = 2f;
            source.maxDistance = 25f;
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

        static void Bank(string name, Transform parent, Vector3 position, Vector2 size, GroundType ground, Material material) =>
            SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, parent, position - Vector3.up * 0.25f,
                new Vector3(size.x, 0.5f, size.y), material).AddComponent<GroundSurface>().type = ground;

        static void Block(string name, Transform parent, Vector3 position, Vector3 size) =>
            SceneBuilder.CreateBorder(name, parent, parent.TransformPoint(position), parent.rotation, size);
    }
}
