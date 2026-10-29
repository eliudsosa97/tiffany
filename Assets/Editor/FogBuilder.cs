using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.EditorTools
{
    public static class FogBuilder
    {
        public const int CardsPerClearing = 2;
        public const int CardsPerSegment = 1;
        public const float MinHeight = 0.45f;
        public const float MaxHeight = 0.6f;
        public const float WaterHeight = 0.55f;
        public const float PathCardLength = 12f;
        public const float WaterCardSize = 32f;
        public const int HauntedCards = 8;
        const float ClearingLength = 40f;
        const float SideOffset = 10f;
        const float WaterDistance = 6f;
        const float TreeDistance = 8f;

        public static GroundFog Build(Transform world, WitchChapterTracker tracker)
        {
            bool closedLoop = tracker.clearings.Length > 1
                && tracker.clearings[tracker.clearings.Length - 1].Find("Path to Next Clearing") != null;
            var fog = BuildLayers(world, tracker.clearings, tracker.player, closedLoop, tracker.transform);
            var captures = new GameObject("Fog Captures").transform;
            captures.SetParent(world, false);
            CapturePair(captures, fog, FindCapture(tracker.clearings, tracker.clearings[0].name), "Fog 1 Cemetery");
            CapturePair(captures, fog, FindCapture(tracker.clearings, "Water 2 Crossing"), "Fog 2 River");
            CapturePair(captures, fog, FindCapture(tracker.clearings, "Water 1 Lake"), "Fog 3 Lake");
            return fog;
        }

        public static GroundFog BuildLayers(Transform world, Transform[] clearings, Transform player = null,
            bool closedLoop = false, Transform route = null)
        {
            closedLoop &= clearings.Length > 1;
            Physics.SyncTransforms();
            var water = new List<Bounds>();
            var waterSurfaces = new List<Renderer>();
            var trees = new List<Bounds>();
            foreach (var clearing in clearings)
                if (clearing == null) throw new ArgumentException($"{GameInfo.LogPrefix} Falta un claro para generar la neblina.");
            var scenery = route != null ? route : world;
            foreach (var renderer in scenery.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (renderer.name == "Water" || (material != null && material.shader != null
                    && material.shader.name == "Universal Render Pipeline/Tiffany Water"))
                {
                    water.Add(renderer.bounds);
                    waterSurfaces.Add(renderer);
                }
                for (var node = renderer.transform; node != scenery; node = node.parent)
                {
                    if (!TreeName(node.name)) continue;
                    trees.Add(renderer.bounds);
                    break;
                }
            }

            var root = new GameObject(GroundFog.RootName);
            root.transform.SetParent(world, false);
            var fog = root.AddComponent<GroundFog>();
            fog.player = player;
            var layers = new List<GroundFog.Layer>(MaxCards(clearings.Length, closedLoop, waterSurfaces.Count));
            var materialAsset = GeneratedMaterials.GetGroundFog();
            for (int i = 0; i < clearings.Length; i++)
            {
                Vector3 center = clearings[i].position;
                Vector3 previous = clearings[i > 0 ? i - 1 : closedLoop ? clearings.Length - 1 : 0].position;
                Vector3 next = clearings[i + 1 < clearings.Length ? i + 1 : closedLoop ? 0 : i].position;
                if (route != null)
                {
                    var openings = RouteLayout.Openings(i);
                    previous = i > 0 ? route.TransformPoint(openings[0]) : center;
                    next = i + 1 < clearings.Length ? route.TransformPoint(openings[openings.Length - 1]) : center;
                }
                Vector3 direction = Vector3.ProjectOnPlane(i + 1 < clearings.Length || closedLoop ? next - center : center - previous, Vector3.up).normalized;
                if (direction.sqrMagnitude < 0.001f) direction = clearings[i].forward;
                Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
                if (i == 0 && !closedLoop) previous = center - direction * ClearingLength * 0.5f;
                if (i == clearings.Length - 1 && !closedLoop) next = center + direction * ClearingLength * 0.5f;
                for (int card = 0; card < CardsPerClearing; card++)
                {
                    float offset = (card * 2f - 1f) * SideOffset;
                    AddLayer($"Clearing {i + 1} Fog {card + 1}", center + side * offset, direction,
                        new Vector2(24f, ClearingLength), Mathf.Lerp(MinHeight, MaxHeight, card),
                        previous, center, next);
                }
                if (i + 1 >= clearings.Length && !closedLoop) continue;
                if (route != null)
                {
                    var points = RouteLayout.Path(i);
                    for (int segment = 1; segment < points.Length; segment++)
                    {
                        Vector3 start = route.TransformPoint(points[segment - 1]);
                        Vector3 corner = route.TransformPoint(points[segment]);
                        Vector3 end = route.TransformPoint(points[Mathf.Min(segment + 1, points.Length - 1)]);
                        float distance = Vector3.ProjectOnPlane(corner - start, Vector3.up).magnitude;
                        int cards = Mathf.CeilToInt(distance / PathCardLength);
                        for (int card = 0; card < cards; card++)
                            AddLayer($"Segment {i + 1} Fog {segment}-{card + 1}",
                                Vector3.Lerp(start, corner, (card + 0.5f) / cards), corner - start,
                                new Vector2(12f, distance / cards + 4f), 0.5f, start, corner, end);
                    }
                    continue;
                }
                float length = Vector3.Distance(center, next);
                for (int card = 0; card < CardsPerSegment; card++)
                    AddLayer($"Segment {i + 1} Fog {card + 1}", Vector3.Lerp(center, next, (card + 1f) / (CardsPerSegment + 1f)),
                        direction, new Vector2(12f, Mathf.Max(4f, length - ClearingLength + 8f)), 0.5f,
                        center, (center + next) * 0.5f, next);
            }
            if (route != null)
                for (int i = 0; i < HauntedCards; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, i * 360f / HauntedCards, 0f) * Vector3.forward;
                    Vector3 center = clearings[4].position;
                    AddLayer($"Haunted Forest Fog {i + 1}", center + direction * 32f, direction,
                        new Vector2(28f, 32f), 0.5f, center, center, center);
                }
            for (int i = 0; i < waterSurfaces.Count; i++)
            {
                var surface = waterSurfaces[i];
                WaterFootprint(surface, out var footprint, out var axis);
                Vector3 forward = new Vector3(-axis.y, 0f, axis.x);
                Vector3 right = new Vector3(axis.x, 0f, axis.y);
                Vector3 center = surface.bounds.center;
                int columns = Mathf.Max(1, Mathf.CeilToInt(footprint.z * 2f / WaterCardSize));
                int rows = Mathf.Max(1, Mathf.CeilToInt(footprint.w * 2f / WaterCardSize));
                float width = footprint.z * 2f / columns, length = footprint.w * 2f / rows;
                for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    AddLayer($"Water Fog {i + 1} {column + 1}-{row + 1}",
                        center + right * ((column + 0.5f) * width - footprint.z)
                            + forward * ((row + 0.5f) * length - footprint.w),
                        forward, new Vector2(width + 4f, length + 4f), WaterHeight,
                        center - forward * footprint.w, center, center + forward * footprint.w, surface);
            }
            fog.layers = layers.ToArray();
            fog.ApplyProperties();
            return fog;

            void AddLayer(string name, Vector3 position, Vector3 forward, Vector2 size, float height,
                Vector3 start, Vector3 corner, Vector3 end, Renderer waterSurface = null)
            {
                bool onWater = waterSurface != null;
                float groundHeight = onWater ? waterSurface.bounds.max.y : GroundHeight(world, position);
                position.y = groundHeight + height;
                if (!onWater)
                {
                    float nearest = float.PositiveInfinity;
                    foreach (var surface in waterSurfaces)
                    {
                        float distance = (surface.bounds.ClosestPoint(position) - position).sqrMagnitude;
                        if (distance >= nearest) continue;
                        waterSurface = surface;
                        nearest = distance;
                    }
                }
                WaterFootprint(waterSurface, out var footprint, out var axis);
                var card = SceneBuilder.CreatePrimitive(name, PrimitiveType.Quad, root.transform, Vector3.zero,
                    new Vector3(size.x, size.y, 1f), materialAsset);
                float tilt = Mathf.Atan2(0.3f, size.x) * Mathf.Rad2Deg;
                card.transform.SetPositionAndRotation(position,
                    Quaternion.LookRotation(Vector3.up, forward) * Quaternion.Euler(0f, tilt, 0f));
                Object.DestroyImmediate(card.GetComponent<Collider>());
                GameObjectUtility.SetStaticEditorFlags(card, StaticEditorFlags.BatchingStatic);
                var renderer = card.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                float density = FogDensity.Ambient(Near(renderer.bounds, water, WaterDistance), Near(renderer.bounds, trees, TreeDistance));
                if (route != null && Vector3.ProjectOnPlane(position - clearings[4].position, Vector3.up).magnitude <= 45f)
                    density = Mathf.Clamp01(Mathf.Max(density, FogDensity.Ambient(false, true)) + FogDensity.TreeBoost);
                layers.Add(new GroundFog.Layer
                {
                    renderer = renderer,
                    groundHeight = groundHeight,
                    density = onWater ? FogDensity.Ambient(true, false) : density,
                    water = onWater,
                    waterFootprint = footprint,
                    waterAxis = axis,
                    pathStart = start,
                    pathCorner = corner,
                    pathEnd = end
                });
            }
        }

        public static int MaxCards(int clearingCount, bool closedLoop = false, int waterCount = 0) =>
            Mathf.Max(0, clearingCount) * CardsPerClearing
            + (closedLoop && clearingCount > 1 ? clearingCount : Mathf.Max(0, clearingCount - 1)) * CardsPerSegment
            + Mathf.Max(0, waterCount);

        static void WaterFootprint(Renderer surface, out Vector4 footprint, out Vector2 axis)
        {
            footprint = Vector4.zero;
            axis = Vector2.right;
            if (surface == null) return;
            Vector3 right = surface.transform.TransformVector(Vector3.right);
            Vector3 forward = surface.transform.TransformVector(Vector3.forward);
            footprint = new Vector4(surface.bounds.center.x, surface.bounds.center.z,
                surface.localBounds.extents.x * right.magnitude, surface.localBounds.extents.z * forward.magnitude);
            axis = new Vector2(right.x, right.z).normalized;
        }

        static bool TreeName(string name) => name.StartsWith("Forest", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Tree", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Trunk", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Pine", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Oak", StringComparison.OrdinalIgnoreCase);

        static bool Near(Bounds footprint, List<Bounds> bounds, float radius)
        {
            foreach (var area in bounds)
            {
                if (footprint.min.x <= area.max.x + radius && footprint.max.x >= area.min.x - radius
                    && footprint.min.z <= area.max.z + radius && footprint.max.z >= area.min.z - radius) return true;
            }
            return false;
        }

        static float GroundHeight(Transform world, Vector3 position)
        {
            var hits = Physics.RaycastAll(position + Vector3.up * 8f, Vector3.down, 32f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            float height = position.y;
            foreach (var hit in hits)
            {
                if (hit.distance >= nearest || !hit.transform.IsChildOf(world)
                    || hit.collider.GetComponent<GroundSurface>() == null) continue;
                height = hit.point.y;
                nearest = hit.distance;
            }
            return height;
        }

        static CaptureStop FindCapture(Transform[] clearings, string name)
        {
            foreach (var clearing in clearings)
                foreach (var stop in clearing.GetComponentsInChildren<CaptureStop>(true))
                    if (stop.name == name) return stop;
            throw new InvalidOperationException($"{GameInfo.LogPrefix} Falta la parada {name} para capturar la neblina.");
        }

        static void CapturePair(Transform parent, GroundFog fog, CaptureStop source, string name)
        {
            for (int i = 0; i < 2; i++)
            {
                bool enabled = i == 0;
                var stop = new GameObject(name + (enabled ? " On" : " Off"));
                stop.transform.SetParent(parent, false);
                stop.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = source.overrideCamera;
                capture.cameraPosition = source.cameraPosition;
                capture.cameraRotation = source.cameraRotation;
                stop.AddComponent<LightCaptureStage>();
                var stage = stop.AddComponent<FogCaptureStage>();
                stage.fog = fog;
                stage.fogEnabled = enabled;
            }
        }
    }
}
