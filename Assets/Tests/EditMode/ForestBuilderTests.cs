using System.Collections.Generic;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed class ForestBuilderTests
    {
        const string Temp = "Assets/ForestBuilderTestTemp";
        GameObject root;
        string previousFolder;
        [SetUp]
        public void SetUp()
        {
            previousFolder = GeneratedMaterials.folder;
            GeneratedMaterials.folder = Temp + "/Materials";
            root = new GameObject("Clearing");
            root.transform.SetPositionAndRotation(new Vector3(108f, 4f, 56f), Quaternion.Euler(0f, 31f, 0f));
        }
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(Temp);
            GeneratedMaterials.folder = previousFolder;
        }
        void Populate(int seed = 17393) => ForestBuilder.Populate(root.transform, seed, 36f, 5f,
            ForestBuilder.RouteExclusions(4), ForestBuilder.RouteViewPoints(4));
        ForestInstances Understory => root.GetComponentInChildren<ForestInstances>();

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void PopulateLeavesPathAndReservedObjectsClear(int index)
        {
            var hide = new GameObject("Reserved Hide", typeof(SphereCollider), typeof(HideVolume));
            hide.transform.SetParent(root.transform, false);
            hide.transform.localPosition = new Vector3(11f, 0f, -4f);
            hide.GetComponent<SphereCollider>().radius = 3f;
            var zones = new List<(Vector2 center, float radius)>(ForestBuilder.RouteExclusions(index));
            zones.Add((new Vector2(11f, -4f), 3f));
            ForestBuilder.Populate(root.transform, 17389 + index, 36f, 5f, zones.ToArray());
            foreach (Vector3 opening in RouteLayout.Openings(index))
            {
                Vector3 local = opening - RouteLayout.Clearing(index);
                for (int sample = 0; sample <= 40; sample++)
                    zones.Add((new Vector2(local.x, local.z) * (sample / 40f), RouteLayout.PathWidth * 0.5f));
            }
            var filters = root.GetComponentsInChildren<MeshFilter>();
            Assert.IsNotEmpty(filters);
            foreach (var filter in filters)
                CheckFootprint(filter.sharedMesh, root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix, zones);
            foreach (var batch in Understory.data.batches)
            foreach (var matrix in batch.matrices) CheckFootprint(batch.mesh, matrix, zones);
        }
        static void CheckFootprint(Mesh mesh, Matrix4x4 matrix, List<(Vector2 center, float radius)> zones)
        {
            var bounds = ForestInstances.TransformBounds(mesh.bounds, matrix);
            Assert.IsTrue(bounds.min.x >= 2.5f || bounds.max.x <= -2.5f, mesh.name);
            Assert.GreaterOrEqual(Mathf.Min(bounds.min.x, bounds.min.z), -18f);
            Assert.LessOrEqual(Mathf.Max(bounds.max.x, bounds.max.z), 18f);
            foreach (var zone in zones)
            {
                var local = matrix.inverse.MultiplyPoint3x4(new Vector3(zone.center.x, 0f, zone.center.y));
                local.x = Mathf.Clamp(local.x, mesh.bounds.min.x, mesh.bounds.max.x);
                local.z = Mathf.Clamp(local.z, mesh.bounds.min.z, mesh.bounds.max.z);
                var closest = matrix.MultiplyPoint3x4(local);
                Assert.GreaterOrEqual((new Vector2(closest.x, closest.z) - zone.center).sqrMagnitude + 0.001f,
                    zone.radius * zone.radius, mesh.name);
            }
        }
        [Test]
        public void CanopiesProvideCoverAbovePlayerAndTrunksRemainSolid()
        {
            Populate();
            Physics.SyncTransforms();
            var canopies = root.GetComponentsInChildren<BoxCollider>();
            Assert.IsNotEmpty(canopies);
            Assert.AreEqual(canopies.Length, root.GetComponentsInChildren<CapsuleCollider>().Length);
            foreach (var canopy in canopies)
            {
                Assert.AreEqual(LayerMask.NameToLayer("Cover"), canopy.gameObject.layer);
                Assert.Greater(canopy.bounds.min.y - root.transform.position.y, 2.5f);
                Assert.IsFalse(canopy.isTrigger);
                var trunk = canopy.transform.parent.GetComponent<CapsuleCollider>();
                Assert.IsFalse(trunk.isTrigger);
                Assert.AreNotEqual(canopy.gameObject.layer, trunk.gameObject.layer);
            }
        }
        [TestCase(0f)]
        [TestCase(180f)]
        [TestCase(90f)]
        public void RotatedClearingKeepsCanopiesOutOfWorldCameraView(float yaw)
        {
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var point = new Vector2(6f, -1.5f);
            var target = new GameObject("Protected Point").transform;
            target.SetParent(root.transform, false);
            target.localPosition = new Vector3(point.x, 0f, point.y);
            var cameraObject = new GameObject("Camera");
            try
            {
                var followCamera = cameraObject.AddComponent<FollowCamera>();
                followCamera.target = target;
                followCamera.Snap();
                ForestBuilder.Populate(root.transform, 17400, 36f, 5f,
                    new (Vector2 center, float radius)[0], new[] { point });
                Physics.SyncTransforms();
                var canopies = root.GetComponentsInChildren<BoxCollider>();
                Assert.IsNotEmpty(canopies);
                var toPoint = target.position - cameraObject.transform.position;
                var ray = new Ray(cameraObject.transform.position, toPoint);
                foreach (var canopy in canopies)
                    Assert.IsFalse(canopy.Raycast(ray, out _, toPoint.magnitude),
                        $"Una copa en {canopy.transform.position} tapa el punto protegido.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
        [Test]
        public void UnderstoryUsesSavedInstancedBatchesWithoutPerPlantObjects()
        {
            Populate();
            Assert.AreEqual(0, Understory.transform.childCount);
            Assert.IsEmpty(Understory.GetComponentsInChildren<Renderer>());
            Assert.IsEmpty(Understory.GetComponentsInChildren<Collider>());
            Assert.IsTrue(AssetDatabase.Contains(Understory.data));
            Assert.AreEqual(4, Understory.data.batches.Length);
            foreach (var batch in Understory.data.batches)
            {
                Assert.IsTrue(batch.material.enableInstancing);
                Assert.IsTrue(AssetDatabase.Contains(batch.material));
                Assert.IsNotEmpty(batch.matrices);
                var bounds = batch.bounds;
                bounds.Expand(0.001f);
                foreach (var matrix in batch.matrices)
                foreach (var vertex in batch.mesh.vertices) Assert.IsTrue(bounds.Contains(matrix.MultiplyPoint3x4(vertex)));
            }
        }
        [Test]
        public void SameSeedRebuildsIdenticallyAndDifferentSeedChangesPlacement()
        {
            Populate();
            var first = Snapshot();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Understory.data));
            Populate();
            CollectionAssert.AreEqual(first, Snapshot());
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Understory.data)));
            Populate(17394);
            CollectionAssert.AreNotEqual(first, Snapshot());
        }
        Matrix4x4[] Snapshot()
        {
            var matrices = new List<Matrix4x4>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) matrices.Add(filter.transform.localToWorldMatrix);
            foreach (var batch in Understory.data.batches) matrices.AddRange(batch.matrices);
            return matrices.ToArray();
        }
        [TestCase(0)] [TestCase(180)] [TestCase(199)]
        public void PopulationFitsRemainingRendererBudgetAndKeepsStaticGeometryUnshadowed(int existing)
        {
            for (int i = 0; i < existing; i++) new GameObject("Existing", typeof(MeshRenderer)).transform.SetParent(root.transform);
            Populate();
            Assert.LessOrEqual(root.GetComponentsInChildren<Renderer>(true).Length, 200);
            var forest = root.transform.Find("Forest");
            long triangles = 0;
            foreach (var filter in forest.GetComponentsInChildren<MeshFilter>()) triangles += filter.sharedMesh.GetIndexCount(0) / 3;
            foreach (var batch in Understory.data.batches) triangles += batch.mesh.GetIndexCount(0) / 3 * batch.matrices.Length;
            Assert.That(triangles, Is.InRange(1L, 110000L));
            foreach (var renderer in forest.GetComponentsInChildren<Renderer>())
            {
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic));
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
            }
        }
    }
}
