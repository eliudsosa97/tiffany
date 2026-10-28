using System;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class RouteBuilder
    {
        const float ClearingSize = 36f;
        const float Spacing = 56f;
        const float PathWidth = 5f;
        const string GraveyardFolder = "Assets/Art/Kenney/Graveyard/Models";

        public static Transform BuildRoute(Transform parent, PlayerController player, Storm storm,
            out Transform[] memories, out Vector3[] skyRoute)
        {
            var route = Root("Route", parent, Vector3.zero);
            var names = new[] { "Cemetery", "Water Plant", "Train Tracks", "Stable", "Witch Hut", "Lake", "Road", "Tiffany House" };
            var grounds = new[] { GroundType.Grass, GroundType.Slab, GroundType.Gravel, GroundType.Branches,
                GroundType.Leaves, GroundType.Grass, GroundType.Slab, GroundType.Dirt };
            var floor = GeneratedMaterials.Get("RouteGround", new Color(0.25f, 0.25f, 0.25f));
            var structure = GeneratedMaterials.Get("RouteStructure", new Color(0.4f, 0.4f, 0.4f));
            var dark = GeneratedMaterials.Get("RouteForest", new Color(0.09f, 0.09f, 0.09f));
            memories = new Transform[5];
            skyRoute = new Vector3[names.Length];
            var tracker = route.gameObject.AddComponent<WitchChapterTracker>();
            tracker.player = player.transform;
            tracker.clearings = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var clearing = Root($"Route {i + 1} {names[i]}", route, new Vector3(108f, 0f, i * Spacing));
                tracker.clearings[i] = clearing;
                if (i == 5) BuildLake(clearing, floor);
                else Ground("Ground", clearing, Vector3.zero, new Vector2(ClearingSize, ClearingSize), grounds[i], floor);
                BuildEdges(clearing, i, dark);
                if (i < names.Length - 1) BuildPath(clearing, floor, dark);
                Vector3 memoryPosition = BuildLandmark(i, clearing, structure, dark);
                if (i < memories.Length) memories[i] = Root("Memory Point", clearing, memoryPosition);
                skyRoute[i] = clearing.position + Vector3.up * 14f;

                var stop = Root(clearing.name, clearing, new Vector3(0f, 0f, -8f));
                var capture = stop.gameObject.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                if (i == 0) capture.wait = MemoryEcho.Duration;
                stop.gameObject.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
                Vector3 candlePosition = i == 7 ? new Vector3(-4f, 1.1f, -0.7f) : new Vector3(-6f, 0f, -8f);
                SceneBuilder.CreateCandle(clearing, i == 7 ? "Window Candle" : "Family Candle",
                    clearing.TransformPoint(candlePosition), CandleKind.Family, true, player.GetComponent<PlayerSoul>(), storm);
            }

            var spawn = Root("Spawn", route.GetChild(0), new Vector3(0f, 0f, -13f));
            spawn.rotation = Quaternion.LookRotation(memories[0].position - spawn.position);
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            foreach (var node in route.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(node.gameObject, StaticEditorFlags.BatchingStatic);
            foreach (var renderer in route.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Physics.SyncTransforms();
            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            foreach (var collider in route.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || collider.bounds.max.y <= route.position.y + 1f) continue;
                var modifier = collider.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = notWalkable;
            }
            return route;
        }

        static void BuildEdges(Transform clearing, int index, Material forest)
        {
            float half = ClearingSize * 0.5f;
            Border("West Border", clearing, new Vector3(-half, 1.5f, 0f), new Vector3(0.5f, 3f, ClearingSize));
            Border("East Border", clearing, new Vector3(half, 1.5f, 0f), new Vector3(0.5f, 3f, ClearingSize));
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? -half : half;
                string name = end == 0 ? "Entrance Border" : "Exit Border";
                if ((index == 0 && end == 0) || (index == 7 && end == 1))
                    Border(name, clearing, new Vector3(0f, 1.5f, z), new Vector3(ClearingSize, 3f, 0.5f));
                else
                {
                    float width = (ClearingSize - PathWidth) * 0.5f;
                    for (int side = -1; side <= 1; side += 2)
                        Border($"{name} {side}", clearing, new Vector3(side * (half - width * 0.5f), 1.5f, z),
                            new Vector3(width, 3f, 0.5f));
                }
            }
            var trees = Root("Forest", clearing, Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
                for (int i = -1; i <= 1; i++)
                {
                    Part($"Trunk X {side} {i}", trees, new Vector3(side * 17f, 3f, i * 11f), new Vector3(1.2f, 6f, 1.2f), forest);
                    if (i != 0)
                        Part($"Trunk Z {side} {i}", trees, new Vector3(i * 11f, 3.5f, side * 17f), new Vector3(1.4f, 7f, 1.4f), forest);
                }
        }

        static void BuildPath(Transform clearing, Material floor, Material forest)
        {
            float length = Spacing - ClearingSize;
            var path = Root("Path to Next Clearing", clearing, new Vector3(0f, 0f, Spacing * 0.5f));
            Ground("Ground", path, Vector3.zero, new Vector2(PathWidth, length), GroundType.Dirt, floor);
            for (int side = -1; side <= 1; side += 2)
            {
                Border($"Border {side}", path, new Vector3(side * (PathWidth * 0.5f + 0.25f), 1.5f, 0f),
                    new Vector3(0.5f, 3f, length));
                for (int i = -1; i <= 1; i += 2)
                    Part($"Trunk {side} {i}", path, new Vector3(side * 3.6f, 3f, i * 6f), new Vector3(1.2f, 6f, 1.2f), forest);
            }
        }

        static Vector3 BuildLandmark(int index, Transform clearing, Material structure, Material dark)
        {
            switch (index)
            {
                case 0:
                    BuildCemetery(clearing, structure);
                    return new Vector3(0f, 0f, -3.2f);
                case 1:
                    var fence = Root("Fence", clearing, Vector3.zero);
                    for (int i = 0; i < 6; i++)
                        Part($"Post {i + 1}", fence, new Vector3(-10f + i * 4f, 1.5f, 0f), new Vector3(0.25f, 3f, 0.25f), structure);
                    for (int i = 0; i < 2; i++)
                        Part($"Rail {i + 1}", fence, new Vector3(0f, 0.8f + i * 1.4f, 0f), new Vector3(20f, 0.2f, 0.25f), structure);
                    for (int side = -1; side <= 1; side += 2)
                        SceneBuilder.CreatePrimitive($"Tank {side}", PrimitiveType.Cylinder, clearing,
                            new Vector3(side * 7f, 3f, 8f), new Vector3(5f, 3f, 5f), structure);
                    return new Vector3(0f, 0f, -1f);
                case 2:
                    var tracks = Root("Tracks", clearing, Vector3.zero);
                    for (int i = 0; i < 12; i++)
                        Part($"Sleeper {i + 1}", tracks, new Vector3(-12.1f + i * 2.2f, 0.04f, 0f),
                            new Vector3(0.45f, 0.08f, 5f), structure).AddComponent<GroundSurface>().type = GroundType.Gravel;
                    for (int side = -1; side <= 1; side += 2)
                        Part($"Rail {side}", tracks, new Vector3(0f, 0.1f, side * 1.6f),
                            new Vector3(28f, 0.2f, 0.15f), dark).AddComponent<GroundSurface>().type = GroundType.Gravel;
                    return new Vector3(0f, 0f, -3f);
                case 3:
                    BuildShelter("Barn", clearing, 14f, 10f, 5f, structure);
                    return new Vector3(0f, 0f, 3f);
                case 4:
                    BuildShelter("Hut", clearing, 8f, 8f, 3.5f, structure);
                    return new Vector3(0f, 0f, 3f);
                case 6:
                    Part("Asphalt", clearing, new Vector3(0f, 0.02f, 0f), new Vector3(35f, 0.04f, 8f), dark)
                        .AddComponent<GroundSurface>().type = GroundType.Slab;
                    for (int i = -1; i <= 1; i++)
                    {
                        var stripe = Part($"Road Marking {i + 2}", clearing, new Vector3(i * 10f, 0.05f, 0f),
                            new Vector3(4f, 0.02f, 0.15f), structure);
                        Object.DestroyImmediate(stripe.GetComponent<Collider>());
                    }
                    break;
                case 7:
                    var house = BuildShelter("House", clearing, 12f, 10f, 4f, structure);
                    Part("Window", house, new Vector3(-4f, 1.8f, -5.26f), new Vector3(2.5f, 1.8f, 0.1f), dark);
                    Part("Window Sill", house, new Vector3(-4f, 1f, -5.6f), new Vector3(2.7f, 0.2f, 0.8f), structure);
                    break;
            }
            return Vector3.zero;
        }

        static Transform BuildShelter(string name, Transform clearing, float width, float depth, float height, Material material)
        {
            var shelter = Root(name, clearing, new Vector3(0f, 0f, 5f));
            for (int side = -1; side <= 1; side += 2)
            {
                Wall($"Side Wall {side}", shelter, new Vector3(side * width * 0.5f, height * 0.5f, 0f),
                    new Vector3(0.5f, height, depth), material, side < 0);
                float frontWidth = (width - 4f) * 0.5f;
                Wall($"Front Wall {side}", shelter, new Vector3(side * (2f + frontWidth * 0.5f), height * 0.5f, -depth * 0.5f),
                    new Vector3(frontWidth, height, 0.5f), material, true);
            }
            Part("Back Wall", shelter, new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, 0.5f), material);
            Part("Roof", shelter, new Vector3(0f, height, 0f), new Vector3(width + 1f, 0.4f, depth + 1f), material)
                .GetComponent<Renderer>().enabled = false;
            return shelter;
        }

        static void Wall(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool cameraFacing)
        {
            float height = size.y;
            if (cameraFacing)
            {
                position.y -= (height - 1f) * 0.5f;
                size.y = 1f;
            }
            var wall = Part(name, parent, position, size, material);
            var collider = wall.GetComponent<BoxCollider>();
            collider.size = new Vector3(1f, height / size.y, 1f);
            collider.center = Vector3.up * ((height / size.y - 1f) * 0.5f);
        }

        static void BuildLake(Transform clearing, Material floor)
        {
            Ground("West Bank", clearing, new Vector3(-7.5f, 0f, 0f), new Vector2(21f, 36f), GroundType.Grass, floor);
            Ground("East Bank", clearing, new Vector3(16.5f, 0f, 0f), new Vector2(3f, 36f), GroundType.Grass, floor);
            Ground("South Bank", clearing, new Vector3(9f, 0f, -13f), new Vector2(12f, 10f), GroundType.Grass, floor);
            Ground("North Bank", clearing, new Vector3(9f, 0f, 16f), new Vector2(12f, 4f), GroundType.Grass, floor);
            var water = SceneBuilder.CreatePrimitive("Water", PrimitiveType.Plane, clearing, new Vector3(9f, -0.15f, 3f),
                new Vector3(1.2f, 1f, 2.2f), GeneratedMaterials.Get("RouteWater", new Color(0.025f, 0.025f, 0.025f), smoothness: 0.7f));
            Object.DestroyImmediate(water.GetComponent<Collider>());
            for (int side = -1; side <= 1; side += 2)
            {
                Border($"Lake Bank X {side}", clearing, new Vector3(9f + side * 6f, 1.5f, 3f), new Vector3(0.5f, 3f, 22.5f));
                Border($"Lake Bank Z {side}", clearing, new Vector3(9f, 1.5f, 3f + side * 11f), new Vector3(12.5f, 3f, 0.5f));
            }
        }

        static void BuildCemetery(Transform clearing, Material ballMaterial)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{GraveyardFolder}/Textures/colormap.png");
            if (texture == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta el colormap del cementerio.");
            var material = GeneratedMaterials.Get("RouteGraveyard", Color.white);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            GraveyardModel("Tiffany Gravestone", "gravestone-bevel", clearing, new Vector3(0f, 0f, -2f), 1.6f, material);
            var models = new[] { "gravestone-round", "gravestone-broken", "gravestone-cross", "cross", "cross-wood", "gravestone-bevel" };
            for (int i = 0; i < models.Length; i++)
                GraveyardModel($"Grave {i + 1}", models[i], clearing, new Vector3(i % 2 == 0 ? -9f : 9f, 0f, 3f + i / 2 * 5f), 1.5f, material);
            for (int side = -1; side <= 1; side += 2)
            {
                GraveyardModel($"Iron Fence {side}", "iron-fence-damaged", clearing, new Vector3(side * 5f, 0f, -16f), 1.8f, material);
                GraveyardModel($"Wood Fence {side}", "fence-damaged", clearing, new Vector3(side * 13f, 0f, -11f), 1.2f, material);
            }
            SceneBuilder.CreatePrimitive("Ball", PrimitiveType.Sphere, clearing, new Vector3(3f, 0.2f, -6f), Vector3.one * 0.4f, ballMaterial);
        }

        static void GraveyardModel(string name, string id, Transform parent, Vector3 position, float height, Material material)
        {
            string path = $"{GraveyardFolder}/{id}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta el modelo {path}.");
            var model = Object.Instantiate(asset, parent, false);
            model.name = name;
            model.transform.localPosition = position;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException($"{GameInfo.LogPrefix} El modelo {path} no tiene renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var materials = new Material[Math.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            float scale = height / Mathf.Max(bounds.size.y, 0.01f);
            model.transform.localScale = Vector3.one * scale;
            var collider = model.AddComponent<BoxCollider>();
            collider.center = bounds.center - model.transform.position;
            collider.size = bounds.size;
            model.transform.position += Vector3.up * ((parent.TransformPoint(position).y - bounds.min.y) * scale);
        }

        static Transform Root(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            return root;
        }

        static GameObject Part(string name, Transform parent, Vector3 position, Vector3 size, Material material) =>
            SceneBuilder.CreatePrimitive(name, PrimitiveType.Cube, parent, position, size, material);

        static void Ground(string name, Transform parent, Vector3 position, Vector2 size, GroundType type, Material material) =>
            Part(name, parent, position - Vector3.up * 0.25f, new Vector3(size.x, 0.5f, size.y), material)
                .AddComponent<GroundSurface>().type = type;

        static void Border(string name, Transform parent, Vector3 position, Vector3 size) =>
            SceneBuilder.CreateBorder(name, parent, parent.TransformPoint(position), parent.rotation, size);
    }
}
