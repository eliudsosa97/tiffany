using System;
using System.Collections.Generic;
using UnityEngine;
using Cover = TiffanyGame.OcclusionCaptureStage.Cover;

namespace TiffanyGame.EditorTools
{
    public static class OcclusionCaptureBuilder
    {
        public static void Build(Transform world)
        {
            Physics.SyncTransforms();
            var trees = new List<Cover>();
            var buildings = new List<Cover>();
            foreach (var collider in world.GetComponentsInChildren<CapsuleCollider>())
                if (IsTree(collider.transform) && collider.enabled && !collider.isTrigger)
                    trees.Add(new Cover { bounds = collider.bounds, collider = collider });
            foreach (var instances in world.GetComponentsInChildren<ForestInstances>())
            {
                if (instances.data == null) continue;
                foreach (var batch in instances.data.batches)
                {
                    if (!IsTreeName(batch.mesh.name) || batch.mesh.bounds.size.y < 2f
                        || batch.mesh.name.IndexOf("canopy", StringComparison.OrdinalIgnoreCase) >= 0
                        || batch.mesh.name.IndexOf("leaves", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    foreach (var matrix in batch.matrices)
                        trees.Add(new Cover { bounds = ForestInstances.TransformBounds(batch.mesh.bounds,
                            instances.transform.localToWorldMatrix * matrix) });
                }
            }
            foreach (var renderer in world.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.bounds.size.y < 2f) continue;
                if (IsBuilding(renderer.transform)) buildings.Add(new Cover { bounds = renderer.bounds, collider = renderer.GetComponent<Collider>() });
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
                if (node.name == "House") return true;
            return false;
        }

        static void CreateStop(string name, List<Cover> covers, bool dense, Transform parent)
        {
            int bestScore = 0;
            Vector3 bestPlayer = default, bestCamera = default;
            Cover[] bestCovers = null;
            var rotation = Quaternion.Euler(FollowCamera.DefaultPitch, FollowCamera.DefaultYaw, 0f);
            var direction = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            var playerRotation = Quaternion.LookRotation(-direction);
            var side = Vector3.Cross(Vector3.up, direction) * 0.2f;
            for (int i = 0; i < covers.Count; i++)
            {
                var bounds = covers[i].bounds;
                var player = FarSide(covers[i], direction);
                if (!TryGround(player, bounds.max.y, parent.parent, out player)) continue;
                var focus = player + Vector3.up * FollowCamera.ChestHeight - direction * FollowCamera.DefaultLookAhead;
                var camera = focus - rotation * Vector3.forward * FollowCamera.DefaultDistance;
                bool hidden = true;
                for (int height = 0; height < 3; height++)
                    for (int width = -1; width <= 1; width++)
                        hidden &= covers[i].Intersects(camera,
                            player + Vector3.up * (height == 0 ? 0.15f : height == 1 ? FollowCamera.ChestHeight : 1.2f) + side * width);
                if (!hidden) continue;
                var head = player + Vector3.up * 1.2f;
                var blocking = new List<Cover> { covers[i] };
                int neighbors = 0;
                for (int k = 0; k < covers.Count; k++)
                {
                    if (k != i && covers[k].Intersects(camera, head)) blocking.Add(covers[k]);
                    if ((covers[k].bounds.center - bounds.center).sqrMagnitude < 12f * 12f) neighbors++;
                }
                if (dense && (blocking.Count < 2 || neighbors < 3)) continue;
                int score = dense ? blocking.Count * 100 + neighbors : Mathf.RoundToInt(bounds.size.y * 100f);
                if (score <= bestScore) continue;
                bestScore = score;
                bestPlayer = player;
                bestCamera = camera;
                bestCovers = blocking.ToArray();
            }
            if (bestScore == 0)
                throw new InvalidOperationException($"{GameInfo.LogPrefix} No se encontró cobertura para '{name}'.");
            var pair = new GameObject(name).transform;
            pair.SetParent(parent, false);
            for (int i = 0; i < 2; i++)
            {
                var stop = new GameObject(name + (i == 0 ? " Before" : " After"));
                stop.transform.SetParent(pair, false);
                stop.transform.SetPositionAndRotation(bestPlayer, playerRotation);
                var capture = stop.AddComponent<CaptureStop>();
                capture.overrideCamera = false;
                capture.cameraPosition = bestCamera;
                capture.cameraRotation = rotation;
                stop.AddComponent<LightCaptureStage>();
                var stage = stop.AddComponent<OcclusionCaptureStage>();
                stage.after = i == 1;
                stage.covers = bestCovers;
            }
        }

        static Vector3 FarSide(Cover cover, Vector3 direction)
        {
            float reach = cover.bounds.extents.magnitude + 1f;
            var ray = new Ray(cover.bounds.center + direction * reach, -direction);
            if (cover.collider != null && cover.collider.Raycast(ray, out var hit, reach * 2f))
                return hit.point + direction * 0.8f;
            cover.bounds.IntersectRay(ray, out float entry);
            return ray.GetPoint(entry) + direction * 0.8f;
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
