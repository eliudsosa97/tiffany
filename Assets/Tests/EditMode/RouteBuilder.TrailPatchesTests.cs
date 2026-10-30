using System.Reflection;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TiffanyGame.Tests
{
    public sealed partial class RouteBuilderTests
    {
        Transform PatchRoot(int i) => route.GetChild(TrailPatches.Get(i).From).Find("Trail Patches");

        [Test]
        public void NoisyPatchesCoverTheTrailAndFootstepsReadThemBeforeDirt()
        {
            var terrain = new RouteTerrain();
            var find = typeof(PlayerController).GetMethod("FindGround", BindingFlags.Instance | BindingFlags.NonPublic);
            int built = 0;
            for (int from = 0; from < RouteLayout.Count; from++)
            {
                var patches = route.GetChild(from).Find("Trail Patches");
                if (patches != null) built += patches.GetComponentsInChildren<GroundSurface>().Length;
            }
            Assert.AreEqual(TrailPatches.Count, built);
            for (int i = 0; i < TrailPatches.Count; i++)
            {
                var patch = TrailPatches.Get(i);
                var floor = PatchRoot(i).Find($"Trail Patch {i + 1}").GetComponent<GroundSurface>();
                Assert.AreEqual(patch.Type, floor.type);
                AssertGround(floor.transform, patch.Type);
                Assert.IsTrue(floor.GetComponent<NavMeshModifier>().ignoreFromBuild);
                Assert.IsNull(floor.GetComponent<NavMeshObstacle>());
                var mesh = floor.GetComponent<MeshCollider>().sharedMesh;
                float minAlong = float.PositiveInfinity, maxAlong = float.NegativeInfinity;
                float minAcross = float.PositiveInfinity, maxAcross = float.NegativeInfinity;
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 point = route.InverseTransformPoint(floor.transform.TransformPoint(vertex));
                    Assert.AreEqual(terrain.Height(point.x, point.z) + TerrainBuilder.FloorOffset + TrailPatches.FloorLift, point.y, 0.001f);
                    float along = Vector3.Dot(point - patch.Center, patch.Forward);
                    float across = Vector3.Dot(point - patch.Center, patch.Right);
                    minAlong = Mathf.Min(minAlong, along);
                    maxAlong = Mathf.Max(maxAlong, along);
                    minAcross = Mathf.Min(minAcross, across);
                    maxAcross = Mathf.Max(maxAcross, across);
                }
                Assert.AreEqual(patch.Length, maxAlong - minAlong, 0.001f);
                Assert.AreEqual(RouteLayout.PathWidth, maxAcross - minAcross, 0.001f);
                for (int step = 0; step <= 4; step++)
                for (int lane = -1; lane <= 1; lane++)
                {
                    Vector3 point = Vector3.Lerp(patch.Start, patch.End, 0.05f + step * 0.225f) + patch.Right * (lane * 2.4f);
                    point.y = terrain.Height(point.x, point.z) + TerrainBuilder.FloorOffset + TrailPatches.FloorLift;
                    player.transform.position = route.TransformPoint(point);
                    Physics.SyncTransforms();
                    Assert.AreEqual(patch.Type, (GroundType)find.Invoke(player, null), $"Parche {i}, fila {step}, carril {lane}");
                }
            }
        }

        [Test]
        public void PatchFootprintsStaySixMetersFromChallengesCandlesCrossingAndTrainBridge()
        {
            var challenges = route.GetComponentsInChildren<PowerChallenge>();
            var candles = route.GetComponentsInChildren<CandleSpot>();
            var terrain = new RouteTerrain();
            Assert.AreEqual(4, challenges.Length);
            RouteLayout.RiverSpan(out var riverStart, out var riverEnd);
            for (int i = 0; i < TrailPatches.Count; i++)
            {
                var patch = TrailPatches.Get(i);
                foreach (var challenge in challenges)
                    Assert.GreaterOrEqual(PatchDistance(patch, route.InverseTransformPoint(challenge.transform.position)),
                        TrailPatches.Clearance - 0.001f, challenge.name);
                foreach (var candle in candles)
                    Assert.GreaterOrEqual(PatchDistance(patch, route.InverseTransformPoint(candle.transform.position)),
                        TrailPatches.Clearance - 0.001f, candle.name);
                foreach (var bank in new[] { riverStart, riverEnd })
                    Assert.GreaterOrEqual(PatchDistance(patch, bank), TrailPatches.Clearance);
                for (int step = 0; step <= 8; step++)
                for (int lane = -1; lane <= 1; lane++)
                {
                    Vector3 point = Vector3.Lerp(patch.Start, patch.End, step / 8f) + patch.Right * (lane * patch.Width * 0.5f);
                    Assert.IsFalse(ChallengeBuilder.Reserves(point, 0f), $"Parche {i} sobre un reto");
                    Assert.GreaterOrEqual(terrain.TrainDistance(point.x, point.z), RouteLayout.TrainWidth * 0.5f + TrailPatches.Clearance);
                }
            }
        }

        [Test]
        public void EachPatchHasASideHideReachableByThePlaythroughNavigation()
        {
            BakeRouteNavMesh();
            var terrain = new RouteTerrain();
            var navigation = new PlaythroughNavigation();
            for (int i = 0; i < TrailPatches.Count; i++)
            {
                var patch = TrailPatches.Get(i);
                var hide = PatchRoot(i).Find($"Hide Trail Patch {i + 1}").GetComponent<HideVolume>();
                Vector3 position = route.InverseTransformPoint(hide.HidePoint);
                Assert.AreEqual(patch.HidePosition.x, position.x, 0.001f);
                Assert.AreEqual(patch.HidePosition.z, position.z, 0.001f);
                Assert.AreEqual(terrain.Height(position.x, position.z), position.y, 0.001f);
                Assert.That(Mathf.Abs(Vector3.Dot(position - patch.Center, patch.Right)) - patch.Width * 0.5f, Is.InRange(2f, 4f));
                Assert.AreEqual("HideBush", hide.GetComponentInChildren<Renderer>().sharedMaterial.name);
                Assert.AreEqual(1, hide.GetComponentsInChildren<Collider>().Length);
                Assert.IsTrue(hide.GetComponent<Collider>().isTrigger);
                Assert.IsTrue(NavMesh.SamplePosition(hide.HidePoint, out _, 0.5f, NavMesh.AllAreas), hide.name);
                Vector3 start = patch.Center;
                start.y = terrain.Height(start.x, start.z);
                Assert.IsTrue(navigation.Calculate(route.TransformPoint(start), hide.HidePoint), hide.name);
                Assert.AreEqual(NavMeshPathStatus.PathComplete, navigation.Status, hide.name);
            }
        }

        [Test]
        public void ClearingAndFillForestLeaveTheNewHideApproachesClear()
        {
            for (int clearing = 0; clearing < RouteLayout.Count; clearing++)
            foreach (var filter in route.GetChild(clearing).Find("Forest").GetComponentsInChildren<MeshFilter>())
            {
                CheckPatchApproaches(ForestInstances.TransformBounds(filter.sharedMesh.bounds,
                    route.worldToLocalMatrix * filter.transform.localToWorldMatrix));
            }
            foreach (var instances in route.GetComponentsInChildren<ForestInstances>())
            foreach (var batch in instances.data.batches)
            foreach (var matrix in batch.matrices)
                CheckPatchApproaches(ForestInstances.TransformBounds(batch.mesh.bounds,
                    route.worldToLocalMatrix * instances.transform.localToWorldMatrix * matrix));
        }

        static void CheckPatchApproaches(Bounds bounds)
        {
            var footprint = Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
            for (int i = 0; i < TrailPatches.Count; i++)
            {
                var patch = TrailPatches.Get(i);
                for (int step = 0; step <= 6; step++)
                {
                    Vector3 point = Vector3.Lerp(patch.Center, patch.HidePosition, step / 6f);
                    Assert.GreaterOrEqual(RouteTerrain.RectangleDistance(new Vector2(point.x, point.z), footprint),
                        TrailPatches.ApproachRadius - 0.01f, $"Vegetación ante escondite {i}: {bounds}");
                }
            }
        }

        static float PatchDistance(TrailPatches.Patch patch, Vector3 point)
        {
            Vector3 delta = point - patch.Center;
            float across = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(delta, patch.Right)) - patch.Width * 0.5f);
            float along = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(delta, patch.Forward)) - patch.Length * 0.5f);
            return new Vector2(across, along).magnitude;
        }
    }
}
