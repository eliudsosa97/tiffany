using System;
using System.Collections.Generic;
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
        public const float SkyHeight = 7.9f;
        public const float ClearingSize = RouteLayout.ClearingSize;
        public const float TrailRadius = ClearingSize * 0.5f + 2f;
        const float PathWidth = RouteLayout.PathWidth;
        const string GraveyardFolder = "Assets/Art/Kenney/Graveyard/Models";
        const int HauntedRingPines = 16;
        public static readonly GroundType[] ClearingGrounds = { GroundType.Grass, GroundType.Slab, GroundType.Gravel,
            GroundType.Branches, GroundType.Leaves, GroundType.Grass, GroundType.Slab, GroundType.Dirt };

        public static Transform BuildRoute(Transform parent, PlayerController player, Storm storm,
            out Transform[] memories, out Vector3[] skyRoute)
        {
            var route = Root("Route", parent, new Vector3(160f, 0f, 0f));
            var names = new[] { "Cemetery", "Water Plant", "Train Tracks", "Stable", "Witch Hut", "Lake", "Road", "Tiffany House" };
            var pathFloor = GroundMaterials.For(GroundType.Dirt);
            var structure = GeneratedMaterials.Get("RouteStructure", new Color(0.4f, 0.4f, 0.4f));
            var dark = GeneratedMaterials.Get("RouteForest", new Color(0.09f, 0.09f, 0.09f));
            var bush = GeneratedMaterials.Get("HideBush", new Color(0.08f, 0.18f, 0.09f));
            var terrain = new RouteTerrain();
            memories = new Transform[5];
            var tracker = route.gameObject.AddComponent<WitchChapterTracker>();
            tracker.player = player.transform;
            tracker.clearings = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var clearing = Root($"Route {i + 1} {names[i]}", route, RouteLayout.Clearing(i));
                tracker.clearings[i] = clearing;
                var floor = GroundMaterials.For(ClearingGrounds[i]);
                float size = RouteLayout.ClearingSizeOf(i);
                if (i == 5) WaterBuilder.BuildLake(clearing, floor);
                else Ground("Ground", clearing, Vector3.zero, new Vector2(size, size), ClearingGrounds[i], floor);
                BuildEdges(clearing, i, terrain);
                if (i < names.Length - 1) BuildPath(clearing, i, pathFloor, GroundMaterials.For(GroundType.Gravel), terrain);
                Vector3 memoryPosition = BuildLandmark(i, clearing, structure, dark, bush);
                if (i < memories.Length) memories[i] = Root("Memory Point", clearing, memoryPosition);

                var stop = Root(clearing.name, clearing, new Vector3(0f, 0f, -8f));
                var capture = stop.gameObject.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                if (i == 0) capture.wait = MemoryEcho.Duration;
                stop.gameObject.AddComponent<LightCaptureStage>().energy = SoulEnergy.Max;
                Vector3 candlePosition = i == 7 ? new Vector3(-4f, 1.1f, -0.7f) : new Vector3(-6f, 0f, -8f);
                SceneBuilder.CreateCandle(clearing, i == 7 ? "Window Candle" : "Family Candle",
                    clearing.TransformPoint(candlePosition), CandleKind.Family, true, player.GetComponent<PlayerSoul>(), storm);
            }

            TerrainBuilder.Build(route, terrain, structure);
            var landscape = route.Find("Landscape");
            SceneBuilder.CreateCandle(landscape, RouteTerrain.RampCandleName, route.TransformPoint(terrain.RampCandle()),
                CandleKind.Family, true, player.GetComponent<PlayerSoul>(), storm);
            ChallengeBuilder.Build(tracker, player.GetComponent<PlayerSoul>(), storm);
            var spawn = Root("Spawn", route.GetChild(0), new Vector3(0f, 0f, -13f));
            spawn.rotation = Quaternion.LookRotation(memories[0].position - spawn.position);
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            foreach (var node in route.GetComponentsInChildren<Transform>(true))
                if (!ChallengeBuilder.IsMoving(node)) GameObjectUtility.SetStaticEditorFlags(node.gameObject, StaticEditorFlags.BatchingStatic);
            foreach (var renderer in route.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            Physics.SyncTransforms();
            skyRoute = BuildSkyRoute(route, tracker.clearings);
            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            foreach (Transform clearing in route)
            foreach (var collider in clearing.GetComponentsInChildren<Collider>())
            {
                if (clearing == landscape && collider.GetComponentInParent<CandleSpot>() == null) continue;
                if (collider.isTrigger || collider.TryGetComponent<GroundSurface>(out _)) continue;
                if (collider.bounds.max.y <= clearing.position.y + 1f && collider.bounds.size.y <= 1f) continue;
                if (collider.TryGetComponent<NavMeshModifier>(out var modifier) && modifier.ignoreFromBuild) continue;
                if (modifier == null) modifier = collider.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = notWalkable;
            }
            return route;
        }

        static void BuildEdges(Transform clearing, int index, RouteTerrain terrain)
        {
            ForestBuilder.Populate(clearing, 17389 + index, RouteLayout.ClearingSizeOf(index), PathWidth,
                ForestBuilder.RouteExclusions(index), ForestBuilder.RouteViewPoints(index), index == 4 ? HauntedRingPines : 0, terrain);
        }

        static void BuildPath(Transform clearing, int index, Material floor, Material stone, RouteTerrain terrain)
        {
            var points = RouteLayout.Path(index);
            Vector3 center = PathMidpoint(points);
            center.y = terrain.Height(center.x, center.z);
            var path = Root("Path to Next Clearing", clearing, center - RouteLayout.Clearing(index));
            RouteLayout.RiverSpan(out var riverStart, out var riverEnd);
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                if (a == riverStart && b == riverEnd)
                {
                    var crossing = Root("River Crossing", path, (a + b) * 0.5f - center);
                    crossing.localRotation = Quaternion.LookRotation(b - a);
                    WaterBuilder.BuildRiver(crossing, PathWidth, Vector3.Distance(a, b), floor, stone);
                }
                else TerrainBuilder.PathFloor(path, $"Ground {i}", a, b, center, terrain, floor);
            }
        }

        static Vector3 PathMidpoint(Vector3[] points)
        {
            float distance = 0f;
            for (int i = 1; i < points.Length; i++) distance += Vector3.Distance(points[i - 1], points[i]);
            distance *= 0.5f;
            for (int i = 1; i < points.Length; i++)
            {
                float length = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= length) return Vector3.Lerp(points[i - 1], points[i], distance / length);
                distance -= length;
            }
            return points[points.Length - 1];
        }

        static Vector3[] BuildSkyRoute(Transform route, Transform[] clearings)
        {
            var points = new List<Vector3>();
            var obstacles = route.GetComponentsInChildren<Collider>();
            for (int i = 0; i < clearings.Length; i++)
            {
                Vector3 start = clearings[i].position + Vector3.up * SkyHeight;
                Vector3 end = clearings[(i + 1) % clearings.Length].position + Vector3.up * SkyHeight;
                points.Add(start);
                Vector3 direction = Vector3.ProjectOnPlane(end - start, Vector3.up);
                float height = Mathf.Min(start.y, end.y);
                foreach (var obstacle in obstacles)
                {
                    if (obstacle.isTrigger || obstacle.TryGetComponent<GroundSurface>(out _)) continue;
                    var bounds = obstacle.bounds;
                    bounds.Expand(1.2f);
                    var origin = new Vector3(start.x, bounds.center.y, start.z);
                    if (bounds.IntersectRay(new Ray(origin, direction.normalized), out float distance) && distance <= direction.magnitude)
                        height = Mathf.Max(height, bounds.max.y + 0.5f);
                }
                if (height <= Mathf.Min(start.y, end.y)) continue;
                height = Mathf.Max(height, start.y, end.y);
                if (height > start.y) points.Add(new Vector3(start.x, height, start.z));
                if (height > end.y) points.Add(new Vector3(end.x, height, end.z));
            }
            return points.ToArray();
        }

        static Vector3 BuildLandmark(int index, Transform clearing, Material structure, Material dark, Material bush)
        {
            switch (index)
            {
                case 0:
                    BuildCemetery(clearing, structure);
                    HidingBuilder.BuildHideSpot(clearing, "Hide Gravestone", new Vector3(-9f, 0f, 5f), null);
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
                    HidingBuilder.BuildHideSpot(clearing, "Hide Fence Bush", new Vector3(6f, 0f, -1.5f), bush);
                    return new Vector3(0f, 0f, -1f);
                case 2:
                    var tracks = Root("Tracks", clearing, Vector3.back * 7f);
                    tracks.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    for (int i = 0; i < 7; i++)
                        Part($"Sleeper {i + 1}", tracks, new Vector3(-6.6f + i * 2.2f, 0.04f, 0f),
                            new Vector3(0.45f, 0.08f, RouteLayout.TrainWidth), GroundMaterials.For(GroundType.Gravel)).AddComponent<GroundSurface>().type = GroundType.Gravel;
                    for (int side = -1; side <= 1; side += 2)
                        Part($"Rail {side}", tracks, new Vector3(0f, TerrainBuilder.FloorOffset + 0.04f, side * 0.9f),
                            new Vector3(14f, 0.08f, 0.12f), GroundMaterials.For(GroundType.Gravel)).AddComponent<GroundSurface>().type = GroundType.Gravel;
                    HidingBuilder.BuildHideSpot(clearing, "Hide Tracks Bush", new Vector3(6f, 0f, -4f), bush);
                    return new Vector3(0f, 0f, -3f);
                case 3:
                    BuildShelter("Barn", clearing, 14f, 10f, 5f, structure);
                    HidingBuilder.BuildHideSpot(clearing, "Hide Stable Crate", new Vector3(-4.5f, 0f, 7f), structure, PrimitiveType.Cube);
                    return new Vector3(0f, 0f, 3f);
                case 4:
                    BuildShelter("Hut", clearing, 8f, 8f, 3.5f, structure);
                    HidingBuilder.BuildHideSpot(clearing, "Hide Hut Bush", new Vector3(-6f, 0f, 3f), bush);
                    return new Vector3(0f, 0f, 3f);
                case 6:
                    Part("Asphalt", clearing, new Vector3(0f, 0.02f, 0f), new Vector3(35f, 0.04f, 8f), GroundMaterials.For(GroundType.Slab))
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
                GraveyardModel($"Grave {i + 1}", models[i], clearing, new Vector3(i % 2 == 0 ? -9f : 9f, 0f, 3f + i / 2 * 5f), i == 0 ? 2f : 1.5f, material);
            for (int side = -1; side <= 1; side += 2)
            {
                GraveyardModel($"Iron Fence {side}", "iron-fence-damaged", clearing, new Vector3(side * 5f, 0f, -16f), 1.8f, material);
                GraveyardModel($"Wood Fence {side}", "fence-damaged", clearing, new Vector3(side * 13f, 0f, -11f), 1.2f, material);
            }
            var ball = SceneBuilder.CreatePrimitive("Ball", PrimitiveType.Sphere, clearing, new Vector3(3f, 0.2f, -6f), Vector3.one * 0.4f, ballMaterial);
            Object.DestroyImmediate(ball.GetComponent<Collider>());
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

        static void Ground(string name, Transform parent, Vector3 position, Vector2 size, GroundType type, Material material)
        {
            var ground = Part(name, parent, position + Vector3.up * (TerrainBuilder.FloorOffset - 0.03f),
                new Vector3(size.x, 0.06f, size.y), material);
            ground.AddComponent<GroundSurface>().type = type;
            TerrainBuilder.HideFloor(ground);
        }
    }
}
