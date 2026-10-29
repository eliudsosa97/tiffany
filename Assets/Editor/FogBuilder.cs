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
        const float ClearingLength = 40f;
        const float SideOffset = 10f;
        const float WaterDistance = 6f;
        const float TreeDistance = 8f;

        public static GroundFog Build(Transform world, WitchChapterTracker tracker)
        {
            bool closedLoop = tracker.clearings.Length > 1
                && tracker.clearings[tracker.clearings.Length - 1].Find("Path to Next Clearing") != null;
            var fog = BuildLayers(world, tracker.clearings, tracker.player, closedLoop);
            var captures = new GameObject("Fog Captures").transform;
            captures.SetParent(world, false);
            CapturePair(captures, fog, FindCapture(tracker.clearings, tracker.clearings[0].name), "Fog 1 Cemetery");
            CapturePair(captures, fog, FindCapture(tracker.clearings, "Water 2 Crossing"), "Fog 2 River");
            CapturePair(captures, fog, FindCapture(tracker.clearings, "Water 1 Lake"), "Fog 3 Lake");
            return fog;
        }

        public static GroundFog BuildLayers(Transform world, Transform[] clearings, Transform player = null, bool closedLoop = false)
        {
            closedLoop &= clearings.Length > 1;
            Physics.SyncTransforms();
            var water = new List<Bounds>();
            var waterSurfaces = new List<Renderer>();
            var trees = new List<Bounds>();
            foreach (var clearing in clearings)
            {
                if (clearing == null) throw new ArgumentException($"{GameInfo.LogPrefix} Falta un claro para generar la neblina.");
                foreach (var renderer in clearing.GetComponentsInChildren<Renderer>(true))
                {
                    var material = renderer.sharedMaterial;
                    if (renderer.name == "Water" || (material != null && material.shader != null
                        && material.shader.name == "Universal Render Pipeline/Tiffany Water"))
                    {
                        water.Add(renderer.bounds);
                        waterSurfaces.Add(renderer);
                    }
                    for (var node = renderer.transform; node != clearing; node = node.parent)
                    {
                        if (!TreeName(node.name)) continue;
                        trees.Add(renderer.bounds);
                        break;
                    }
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
                float length = Vector3.Distance(center, next);
                for (int card = 0; card < CardsPerSegment; card++)
                    AddLayer($"Segment {i + 1} Fog {card + 1}", Vector3.Lerp(center, next, (card + 1f) / (CardsPerSegment + 1f)),
                        direction, new Vector2(12f, Mathf.Max(4f, length - ClearingLength + 8f)), 0.5f,
                        center, (center + next) * 0.5f, next);
            }
            for (int i = 0; i < waterSurfaces.Count; i++)
            {
                var surface = waterSurfaces[i];
                WaterFootprint(surface, out var footprint, out var axis);
                Vector3 forward = new Vector3(-axis.y, 0f, axis.x);
                Vector3 center = surface.bounds.center;
                AddLayer($"Water Fog {i + 1}", center, forward, new Vector2(footprint.z * 2f + 4f, footprint.w * 2f + 4f),
                    WaterHeight, center - forward * footprint.w, center, center + forward * footprint.w, surface);
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
                layers.Add(new GroundFog.Layer
                {
                    renderer = renderer,
                    groundHeight = groundHeight,
                    density = onWater ? FogDensity.Ambient(true, false)
                        : FogDensity.Ambient(Near(renderer.bounds, water, WaterDistance), Near(renderer.bounds, trees, TreeDistance)),
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
