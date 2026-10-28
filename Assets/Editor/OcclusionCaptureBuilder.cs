using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiffanyGame.EditorTools
{
    public static class OcclusionCaptureBuilder
    {
        readonly struct Cover
        {
            public readonly Bounds Bounds;
            public readonly Collider Collider;
            public Cover(Bounds bounds, Collider collider = null) { Bounds = bounds; Collider = collider; }
            public bool Intersects(Ray ray, float distance) => Collider != null
                ? Collider.Raycast(ray, out _, distance)
                : Bounds.IntersectRay(ray, out float entry) && entry > 0f && entry < distance;

            public Vector3 FarSide(Vector3 direction)
            {
                float reach = Bounds.extents.magnitude + 1f;
                var ray = new Ray(Bounds.center + direction * reach, -direction);
                if (Collider != null && Collider.Raycast(ray, out var hit, reach * 2f))
                    return hit.point + hit.normal * 0.8f;
                Bounds.IntersectRay(ray, out float entry);
                return ray.GetPoint(entry) + direction * 0.8f;
            }
        }

        public static void Build(Transform world)
        {
            Physics.SyncTransforms();
            var trees = new List<Cover>();
            var buildings = new List<Cover>();
            foreach (var collider in world.GetComponentsInChildren<CapsuleCollider>())
                if (IsTree(collider.transform)) trees.Add(new Cover(collider.bounds, collider));
            foreach (var instances in world.GetComponentsInChildren<ForestInstances>())
            {
                if (instances.data == null) continue;
                foreach (var batch in instances.data.batches)
                {
                    if (!IsTreeName(batch.mesh.name) || batch.mesh.bounds.size.y < 2f
                        || batch.mesh.name.IndexOf("canopy", StringComparison.OrdinalIgnoreCase) >= 0
                        || batch.mesh.name.IndexOf("leaves", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    foreach (var matrix in batch.matrices)
                        trees.Add(new Cover(ForestInstances.TransformBounds(batch.mesh.bounds,
                            instances.transform.localToWorldMatrix * matrix)));
                }
            }
            foreach (var renderer in world.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.bounds.size.y < 2f) continue;
                if (IsBuilding(renderer.transform)) buildings.Add(new Cover(renderer.bounds, renderer.GetComponent<Collider>()));
            }
            var root = new GameObject("Cover Captures").transform;
            root.SetParent(world, false);
            CreateStop("Cover 1 Tree", trees, false, root);
            CreateStop("Cover 2 Building", buildings, false, root);
            CreateStop("Cover 3 Dense Forest", trees, true, root);
        }

        static bool IsTreeName(string name) => name.IndexOf("oak", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("ocote", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool IsTree(Transform node)
        {
            for (int i = 0; i < 3 && node != null; i++, node = node.parent)
                if (IsTreeName(node.name)) return true;
            return false;
        }

        static bool IsBuilding(Transform node)
        {
            for (; node != null; node = node.parent)
                if (node.name == "House" || node.name == "Barn" || node.name == "Hut") return true;
            return false;
        }

        static void CreateStop(string name, List<Cover> covers, bool dense, Transform parent)
        {
            int bestScore = 0;
            Vector3 bestPlayer = default, bestCamera = default, bestDirection = default;
            float pitch = dense ? 22f : FollowCamera.DefaultPitch;
            float coverDistance = FollowCamera.DefaultDistance - FollowCamera.OcclusionFocusMargin;
            var defaultDirection = Vector3.ProjectOnPlane(
                Quaternion.Euler(0f, FollowCamera.DefaultYaw, 0f) * Vector3.forward, Vector3.up);
            for (int i = 0; i < covers.Count; i++)
            {
                int directions = dense ? covers.Count : 1;
                for (int j = 0; j < directions; j++)
                {
                    var direction = dense && i != j
                        ? Vector3.ProjectOnPlane(covers[i].Bounds.center - covers[j].Bounds.center, Vector3.up)
                        : defaultDirection;
                    if (direction.sqrMagnitude < 0.01f || direction.sqrMagnitude > 18f * 18f) continue;
                    direction.Normalize();
                    var bounds = covers[i].Bounds;
                    var player = covers[i].FarSide(direction);
                    if (!TryGround(player, bounds.max.y, parent.parent, out player)) continue;
                    var focus = player + Vector3.up * FollowCamera.ChestHeight;
                    var cameraDirection = direction * Mathf.Cos(pitch * Mathf.Deg2Rad) - Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad);
                    var camera = focus - cameraDirection * FollowCamera.DefaultDistance;
                    var ray = new Ray(camera, cameraDirection);
                    if (!covers[i].Intersects(ray, coverDistance)) continue;
                    int count = 0, neighbors = 0;
                    for (int k = 0; k < covers.Count; k++)
                    {
                        if (covers[k].Intersects(ray, coverDistance)) count++;
                        if ((covers[k].Bounds.center - bounds.center).sqrMagnitude < 12f * 12f) neighbors++;
                    }
                    if (dense && (count < 2 || neighbors < 3)) continue;
                    int score = dense ? count * 100 + neighbors : 1;
                    if (score <= bestScore) continue;
                    bestScore = score;
                    bestPlayer = player;
                    bestCamera = camera;
                    bestDirection = direction;
                }
                if (!dense && bestScore > 0) break;
            }
            if (bestScore == 0)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró cobertura para '{name}'.");
            var stop = new GameObject(name);
            stop.transform.SetParent(parent, false);
            stop.transform.SetPositionAndRotation(bestPlayer, Quaternion.LookRotation(-bestDirection));
            var capture = stop.AddComponent<CaptureStop>();
            capture.overrideCamera = true;
            capture.cameraPosition = bestCamera;
            capture.cameraRotation = Quaternion.LookRotation(bestPlayer + Vector3.up * FollowCamera.ChestHeight - bestCamera);
            stop.AddComponent<LightCaptureStage>();
        }

        static bool TryGround(Vector3 position, float top, Transform world, out Vector3 grounded)
        {
            var hits = Physics.RaycastAll(new Vector3(position.x, top + 10f, position.z), Vector3.down,
                Mathf.Infinity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float highest = float.NegativeInfinity;
            foreach (var hit in hits)
                if (hit.collider.transform.IsChildOf(world)
                    && (hit.collider is TerrainCollider || hit.collider.GetComponent<GroundSurface>() != null)
                    && hit.point.y > highest) highest = hit.point.y;
            grounded = new Vector3(position.x, highest, position.z);
            return !float.IsNegativeInfinity(highest);
        }
    }
}
