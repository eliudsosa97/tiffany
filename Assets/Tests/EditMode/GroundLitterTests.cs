using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TiffanyGame.Tests
{
    public sealed class GroundLitterTests
    {
        const string Temp = "Assets/GroundLitterTestTemp";
        GameObject root;
        string previousFolder;
        ForestInstances Litter => root.transform.Find("Ground Litter").GetComponent<ForestInstances>();

        [SetUp]
        public void SetUp()
        {
            previousFolder = GeneratedMaterials.folder;
            GeneratedMaterials.folder = Temp + "/Materials";
            root = new GameObject(nameof(GroundLitterTests));
            // Los tests comparten la física con la escena abierta.
            root.transform.position = Vector3.up * 1000f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(Temp);
            GeneratedMaterials.folder = previousFolder;
        }

        [Test]
        public void DensitiesKeepEachGroundRecognizableAndEveryCellProbabilityBelowOne()
        {
            float Total(GroundType ground)
            {
                float total = 0f;
                foreach (LitterKind kind in Enum.GetValues(typeof(LitterKind))) total += GroundLitter.Density(ground, kind);
                return total;
            }
            foreach (LitterKind kind in Enum.GetValues(typeof(LitterKind)))
            {
                if (kind != LitterKind.Leaves)
                    Assert.Greater(GroundLitter.Density(GroundType.Leaves, LitterKind.Leaves), GroundLitter.Density(GroundType.Leaves, kind));
                if (kind != LitterKind.Twig)
                    Assert.Greater(GroundLitter.Density(GroundType.Branches, LitterKind.Twig), GroundLitter.Density(GroundType.Branches, kind));
                if (kind != LitterKind.Stone)
                    Assert.Greater(GroundLitter.Density(GroundType.Gravel, LitterKind.Stone), GroundLitter.Density(GroundType.Gravel, kind));
                foreach (GroundType ground in Enum.GetValues(typeof(GroundType)))
                    Assert.That(GroundLitter.Density(ground, kind) * Mathf.Pow(GroundLitter.CellSize(kind), 2f), Is.InRange(0f, 0.99999f));
            }
            foreach (GroundType ground in Enum.GetValues(typeof(GroundType)))
            {
                if (ground != GroundType.Branches) Assert.Greater(Total(GroundType.Branches), Total(ground));
                if (ground != GroundType.Slab) Assert.Less(Total(GroundType.Slab), Total(ground));
            }
            Assert.Less(Total(GroundType.Dirt), Total(GroundType.Leaves));
            Assert.Less(Total(GroundType.Dirt), Total(GroundType.Branches));
        }

        [Test]
        public void SamplingDependsOnlyOnTheCellAndKindIncludingNegativeCoordinates()
        {
            foreach (LitterKind kind in Enum.GetValues(typeof(LitterKind)))
            for (int z = -19; z < 20; z++)
            for (int x = -19; x < 20; x++)
            {
                var first = GroundLitter.Sample(kind, x, z);
                GroundLitter.Sample(kind, x + 7, z - 13);
                var second = GroundLitter.Sample(kind, x, z);
                Assert.AreEqual(first.Position, second.Position);
                Assert.AreEqual(first.Yaw, second.Yaw);
                Assert.AreEqual(first.Scale, second.Scale);
                Assert.AreEqual(first.Variant, second.Variant);
                float size = GroundLitter.CellSize(kind);
                Assert.That(first.Position.x, Is.InRange(x * size, (x + 1) * size));
                Assert.That(first.Position.z, Is.InRange(z * size, (z + 1) * size));
                foreach (GroundType ground in Enum.GetValues(typeof(GroundType)))
                    Assert.AreEqual(first.IsPresent(ground), second.IsPresent(ground));
            }
            Assert.AreEqual(new Vector2Int(-1, -1), GroundLitter.Chunk(new Vector3(-0.01f, 20f, -GroundLitter.ChunkSize)));
            Assert.AreEqual(new Vector2Int(1, 0), GroundLitter.Chunk(new Vector3(GroundLitter.ChunkSize, -20f, 0f)));
        }

        [TestCase(GroundType.Leaves)]
        [TestCase(GroundType.Branches)]
        [TestCase(GroundType.Gravel)]
        public void CuttingOneFloorIntoFourKeepsMatricesAndSerializedAssetInBothCreationOrders(GroundType ground)
        {
            var whole = Floor(Vector3.zero, new Vector2(32f, 32f), ground);
            Scatter();
            var expected = Snapshot();
            Assert.IsNotEmpty(expected);
            string path = AssetDatabase.GetAssetPath(Litter.data);
            string serialized = File.ReadAllText(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Object.DestroyImmediate(whole);
            for (int order = 0; order < 2; order++)
            {
                var pieces = new List<GameObject>();
                for (int step = 0; step < 4; step++)
                {
                    int i = order == 0 ? step : 3 - step;
                    pieces.Add(Floor(new Vector3(i % 2 == 0 ? -8f : 8f, 0f, i < 2 ? -8f : 8f), new Vector2(16f, 16f), ground));
                }
                Scatter();
                CollectionAssert.AreEqual(expected, Snapshot());
                Assert.AreEqual(serialized, File.ReadAllText(path));
                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
                foreach (var piece in pieces) Object.DestroyImmediate(piece);
            }
        }

        [Test]
        public void NarrowGroundAndAnOccludingColliderNeverReceiveLitter()
        {
            var narrow = Floor(Vector3.zero, new Vector2(0.45f, 5f), GroundType.Gravel);
            Scatter();
            Assert.IsEmpty(Litter.data.batches);
            Object.DestroyImmediate(narrow);
            Floor(Vector3.zero, new Vector2(16f, 16f));
            Scatter();
            Assert.IsNotEmpty(Snapshot());
            var obstacle = Child("Obstacle", Vector3.up * 12f);
            obstacle.AddComponent<BoxCollider>().size = new Vector3(16f, 1f, 16f);
            LogAssert.Expect(LogType.Log, new Regex(@"tapado por otro collider: 848 \(Obstacle: 848\)"));
            Scatter();
            Assert.IsEmpty(Litter.data.batches);
        }

        [TestCase("Cover")]
        [TestCase("Ignore Raycast")]
        public void CanopiesAndIgnoredVolumesPreserveAllFloorInstances(string layer)
        {
            Floor(Vector3.zero, new Vector2(16f, 16f), GroundType.Branches);
            Scatter();
            var expected = Snapshot();
            Assert.IsNotEmpty(expected);
            var canopy = Child("Canopy", Vector3.up * 12f);
            canopy.layer = LayerMask.NameToLayer(layer);
            canopy.AddComponent<BoxCollider>().size = new Vector3(16f, 1f, 16f);
            LogAssert.Expect(LogType.Log, new Regex(@"tapado por otro collider: 0 \(ninguno\)"));
            Scatter();
            CollectionAssert.AreEqual(expected, Snapshot());
        }

        [TestCase("Memory Point")]
        [TestCase("Candle Point")]
        [TestCase("MemorySpot")]
        [TestCase("CandleSpot")]
        [TestCase("MovableObject")]
        public void InteractiveObjectsKeepTheirClearanceEvenWhenInactive(string kind)
        {
            Floor(Vector3.zero, new Vector2(16f, 16f));
            Scatter();
            var occupied = Snapshot()[0].MultiplyPoint3x4(Vector3.zero);
            var interactive = Child(kind, occupied);
            if (kind == "MemorySpot") interactive.AddComponent<MemorySpot>();
            if (kind == "CandleSpot") interactive.AddComponent<CandleSpot>();
            if (kind == "MovableObject")
            {
                LogAssert.Expect(LogType.Error, "Failed to call static function Reset because an object was provided");
                interactive.AddComponent<MovableObject>();
            }
            interactive.SetActive(false);
            LogAssert.Expect(LogType.Log, new Regex(@"reservado: [1-9]\d*"));
            Scatter();
            Assert.IsNotEmpty(Snapshot());
            foreach (var matrix in Snapshot())
            {
                var position = matrix.MultiplyPoint3x4(Vector3.zero);
                Assert.Greater(new Vector2(position.x - occupied.x, position.z - occupied.z).magnitude, 1.2f);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaterExcludesBothSubmergedGroundAndItsOwnSurface(bool hasCollider)
        {
            Floor(Vector3.zero, new Vector2(16f, 16f));
            Scatter();
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.transform.SetParent(root.transform, false);
            water.transform.localPosition = Vector3.up * 0.25f;
            water.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            water.GetComponent<Renderer>().sharedMaterial = GeneratedMaterials.GetWater();
            if (hasCollider) water.AddComponent<GroundSurface>().type = GroundType.Leaves;
            else Object.DestroyImmediate(water.GetComponent<Collider>());
            LogAssert.Expect(LogType.Log, new Regex(@"bajo agua: [1-9]\d*"));
            Scatter();
            Assert.IsEmpty(Litter.data.batches);
        }

        [Test]
        public void WaterUsesItsTrianglesInsteadOfFloodingTheWholeBoundingBox()
        {
            Floor(Vector3.zero, new Vector2(16f, 16f));
            var water = Child("Irregular Water", Vector3.up * 0.25f);
            var mesh = new Mesh { vertices = new[] { new Vector3(-8f, 0f, -8f), new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, -8f) },
                triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateBounds();
            GeneratedMaterials.EnsureFolder(Temp);
            AssetDatabase.CreateAsset(mesh, Temp + "/Water.asset");
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            water.AddComponent<MeshRenderer>().sharedMaterial = GeneratedMaterials.GetWater();
            Scatter();
            Assert.IsNotEmpty(Snapshot());
            foreach (var matrix in Snapshot())
            {
                var position = matrix.MultiplyPoint3x4(Vector3.zero);
                Assert.Greater(position.x + position.z, 0f);
            }
        }

        [TestCase(15f, true)]
        [TestCase(40f, false)]
        public void LitterFollowsGentleNormalsAndRejectsSteepGround(float angle, bool accepted)
        {
            var floor = Floor(Vector3.zero, new Vector2(16f, 16f));
            floor.transform.localRotation = Quaternion.Euler(angle, 0f, 0f);
            Scatter();
            if (!accepted) { Assert.IsEmpty(Snapshot()); return; }
            Assert.IsNotEmpty(Snapshot());
            var normal = floor.transform.up;
            var top = floor.transform.TransformPoint(Vector3.up * 0.25f);
            foreach (var batch in Litter.data.batches)
            foreach (var matrix in batch.matrices)
            {
                var world = root.transform.localToWorldMatrix * matrix;
                Assert.Greater(Vector3.Dot(normal, world.MultiplyVector(Vector3.up).normalized), 0.9999f);
                var bottom = world.MultiplyPoint3x4(Vector3.up * batch.mesh.bounds.min.y);
                if (batch.mesh.name.StartsWith("LitterRoot", StringComparison.Ordinal))
                {
                    Assert.Less(Vector3.Dot(bottom - top, normal), -0.04f);
                    Assert.Greater(Vector3.Dot(world.MultiplyPoint3x4(Vector3.up * batch.mesh.bounds.max.y) - top, normal), 0.1f);
                }
                else Assert.AreEqual(-0.002f, Vector3.Dot(bottom - top, normal), 0.0003f);
            }
        }

        [Test]
        public void SlopedGroundKeepsCandidateHorizontalCoordinatesAndRoundsHeightToATenthOfAMillimeter()
        {
            var floor = Floor(Vector3.zero, new Vector2(32f, 32f), GroundType.Branches);
            floor.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            Scatter();
            Assert.IsNotEmpty(Snapshot());
            foreach (var batch in Litter.data.batches)
            {
                var kind = batch.mesh.name.StartsWith("LitterLeaves", StringComparison.Ordinal) ? LitterKind.Leaves
                    : batch.mesh.name.StartsWith("LitterTwig", StringComparison.Ordinal) ? LitterKind.Twig
                    : batch.mesh.name.StartsWith("LitterRoot", StringComparison.Ordinal) ? LitterKind.Root
                    : batch.mesh.name == "LitterPineCone" ? LitterKind.PineCone : LitterKind.Stone;
                float cell = GroundLitter.CellSize(kind);
                foreach (var matrix in batch.matrices)
                {
                    var position = root.transform.TransformPoint(matrix.MultiplyPoint3x4(Vector3.zero));
                    var candidate = GroundLitter.Sample(kind, Mathf.FloorToInt(position.x / cell), Mathf.FloorToInt(position.z / cell));
                    Assert.AreEqual(candidate.Position.x, position.x);
                    Assert.AreEqual(candidate.Position.z, position.z);
                    Assert.AreEqual((float)Math.Round(position.y, 4, MidpointRounding.ToEven), position.y);
                }
            }
        }

        [Test]
        public void BatchesUseTightBoundsSavedInstancedMaterialsAndNoPerObjectRenderersOrColliders()
        {
            root.transform.rotation = Quaternion.Euler(0f, 23f, 0f);
            root.transform.localScale = Vector3.one * 1.2f;
            Floor(Vector3.zero, new Vector2(32f, 32f), GroundType.Branches);
            Scatter();
            Assert.AreEqual(0, Litter.transform.childCount);
            Assert.IsEmpty(Litter.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(Litter.GetComponentsInChildren<Renderer>(true));
            Assert.AreEqual(Temp + "/Forest/GroundLitter.asset", AssetDatabase.GetAssetPath(Litter.data));
            Assert.IsNotEmpty(Litter.data.batches);
            foreach (var batch in Litter.data.batches)
            {
                Assert.IsTrue(AssetDatabase.Contains(batch.mesh));
                Assert.IsTrue(AssetDatabase.Contains(batch.material));
                Assert.IsTrue(batch.material.enableInstancing);
                Assert.IsFalse(batch.material.GetShaderPassEnabled("ShadowCaster"));
                var expected = ForestInstances.TransformBounds(batch.mesh.bounds, batch.matrices[0]);
                for (int i = 1; i < batch.matrices.Length; i++)
                    expected.Encapsulate(ForestInstances.TransformBounds(batch.mesh.bounds, batch.matrices[i]));
                Assert.AreEqual(expected.center, batch.bounds.center);
                Assert.AreEqual(expected.size, batch.bounds.size);
                var chunk = GroundLitter.Chunk(Litter.transform.TransformPoint(batch.matrices[0].MultiplyPoint3x4(Vector3.zero)));
                foreach (var matrix in batch.matrices)
                    Assert.AreEqual(chunk, GroundLitter.Chunk(Litter.transform.TransformPoint(matrix.MultiplyPoint3x4(Vector3.zero))));
            }
            string serialized = File.ReadAllText(AssetDatabase.GetAssetPath(Litter.data));
            Scatter();
            Assert.AreEqual(serialized, File.ReadAllText(AssetDatabase.GetAssetPath(Litter.data)));
            Assert.AreEqual(1, root.GetComponentsInChildren<ForestInstances>().Length);
        }

        [Test]
        public void GeneratedMeshesHaveNormalsUvsAndStayWithinTheirTriangleLimits()
        {
            Scatter();
            foreach (var spec in new[] { ("LitterLeaves0", 80), ("LitterLeaves1", 80), ("LitterLeaves2", 80),
                ("LitterTwig0", 40), ("LitterTwig1", 40), ("LitterTwigPile", 96), ("LitterPineCone", 48),
                ("LitterStone", 12), ("LitterGravel", 48), ("LitterRoot0", 48), ("LitterRoot1", 48), ("LitterRoot2", 48) })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Temp}/Forest/{spec.Item1}.asset");
                Assert.IsNotNull(mesh);
                Assert.That(mesh.GetIndexCount(0) / 3, Is.InRange(1u, (uint)spec.Item2));
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length);
                foreach (var normal in mesh.normals) Assert.AreEqual(1f, normal.magnitude, 0.001f);
                foreach (var uv in mesh.uv)
                {
                    Assert.That(uv.x, Is.InRange(0f, 1f));
                    Assert.That(uv.y, Is.InRange(0f, 1f));
                }
                if (spec.Item1.StartsWith("LitterLeaves", StringComparison.Ordinal))
                {
                    Assert.Greater(mesh.bounds.size.y, 0.08f);
                    Assert.That(mesh.GetIndexCount(0) / 6, Is.InRange(25u, 40u));
                    Assert.That(Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z) * 0.9f, Is.GreaterThanOrEqualTo(0.8f));
                    Assert.That(Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z) * 1.05f, Is.LessThanOrEqualTo(1.4f));
                }
                string before = File.ReadAllText(AssetDatabase.GetAssetPath(mesh));
                Scatter();
                Assert.AreEqual(before, File.ReadAllText(AssetDatabase.GetAssetPath(mesh)));
            }
        }

        [Test]
        public void ExistingMeshAssetsAreRebuiltInPlaceWhenGeometryChanges()
        {
            Scatter();
            string path = Temp + "/Forest/LitterTwig0.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            string expected = File.ReadAllText(path);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] *= 0.1f;
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            Assert.AreNotEqual(expected, File.ReadAllText(path));
            Scatter();
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(expected, File.ReadAllText(path));
        }

        [TestCase(GroundType.Leaves, 4, 8, 20)]
        [TestCase(GroundType.Branches, 3, 8, 20)]
        [TestCase(GroundType.Dirt, 7, 3, 5)]
        [TestCase(GroundType.Grass, 0, 3, 5)]
        [TestCase(GroundType.Grass, 5, 3, 5)]
        [TestCase(GroundType.Gravel, 2, 8, 20)]
        [TestCase(GroundType.Slab, 1, 0, 1)]
        [TestCase(GroundType.Slab, 6, 0, 1)]
        public void OpenGroundAtRouteCapturePositionsHasReadableObjectsInThePlayerLight(GroundType ground, int clearing, int minimum, int maximum)
        {
            root.transform.position += new Vector3(108f, 0f, clearing * 56f);
            Floor(Vector3.zero, new Vector2(36f, 36f), ground);
            Scatter();
            int count = 0;
            foreach (var matrix in Snapshot())
            {
                var position = matrix.MultiplyPoint3x4(Vector3.zero);
                if (position.x * position.x + (position.z + 8f) * (position.z + 8f) <= 16f) count++;
            }
            Assert.That(count, Is.InRange(minimum, maximum));
            foreach (var batch in Litter.data.batches)
            foreach (var matrix in batch.matrices)
            {
                float scale = matrix.MultiplyVector(Vector3.up).magnitude;
                var size = batch.mesh.bounds.size * scale;
                Assert.GreaterOrEqual(Mathf.Min(size.x, size.z) * 39f, 6f, batch.mesh.name);
                if (batch.mesh.name.StartsWith("LitterTwig", StringComparison.Ordinal)
                    || batch.mesh.name.StartsWith("LitterRoot", StringComparison.Ordinal))
                    Assert.GreaterOrEqual(Mathf.Max(size.x, size.z) * 39f, 15f, batch.mesh.name);
            }
        }

        [Test]
        public void MainRouteClearingsIncludingNeighboringChunksFitTheLitterBudget()
        {
            var girl = Child("Player", Vector3.zero);
            girl.AddComponent<PlayerSoul>();
            girl.AddComponent<PlayerHiding>();
            var player = girl.AddComponent<PlayerController>();
            var storm = Child("Storm", Vector3.zero).AddComponent<Storm>();
            var route = RouteBuilder.BuildRoute(root.transform, player, storm, out _, out _);
            int renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            Scatter();
            Assert.AreEqual(renderers, root.GetComponentsInChildren<Renderer>(true).Length);
            int worstDraws = 0;
            long worstTriangles = 0;
            foreach (var clearing in route.GetComponent<WitchChapterTracker>().clearings)
            {
                float radius = RouteBuilder.ClearingSize * 0.5f + 8f;
                int draws = 0;
                long triangles = 0;
                foreach (var batch in Litter.data.batches)
                {
                    var position = Litter.transform.TransformPoint(batch.matrices[0].MultiplyPoint3x4(Vector3.zero));
                    var chunk = GroundLitter.Chunk(position);
                    float x = chunk.x * GroundLitter.ChunkSize, z = chunk.y * GroundLitter.ChunkSize;
                    if (x > clearing.position.x + radius || x + GroundLitter.ChunkSize < clearing.position.x - radius
                        || z > clearing.position.z + radius || z + GroundLitter.ChunkSize < clearing.position.z - radius) continue;
                    draws += (batch.matrices.Length + ForestInstances.MaxInstancesPerDraw - 1) / ForestInstances.MaxInstancesPerDraw;
                    triangles += (long)batch.mesh.GetIndexCount(0) / 3 * batch.matrices.Length;
                }
                TestContext.WriteLine($"{clearing.name}: {draws} llamadas, {triangles} triángulos.");
                worstDraws = Math.Max(worstDraws, draws);
                worstTriangles = Math.Max(worstTriangles, triangles);
                Assert.That(draws, Is.InRange(1, 40), clearing.name);
                Assert.That(triangles, Is.InRange(1L, 60000L), clearing.name);
            }
            TestContext.WriteLine($"Suelo, máximos por claro: {worstDraws} llamadas, {worstTriangles} triángulos.");
        }

        [Test]
        public void EveryGroundMaterialStrengthensNormalsAndCavities()
        {
            foreach (GroundType ground in Enum.GetValues(typeof(GroundType)))
            {
                var material = GroundMaterials.For(ground);
                Assert.Greater(material.GetFloat("_NormalStrength"), 1.9f);
                Assert.Greater(material.GetFloat("_Cavity"), 0.32f);
                Assert.AreEqual(0f, material.GetFloat("_Patch"));
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            }
        }

        void Scatter() => GroundLitterBuilder.Scatter(root.transform);

        GameObject Child(string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        GameObject Floor(Vector3 top, Vector2 size, GroundType ground = GroundType.Leaves)
        {
            var floor = Child("Ground", top - Vector3.up * 0.25f);
            floor.AddComponent<BoxCollider>().size = new Vector3(size.x, 0.5f, size.y);
            floor.AddComponent<GroundSurface>().type = ground;
            return floor;
        }

        Matrix4x4[] Snapshot()
        {
            var matrices = new List<Matrix4x4>();
            foreach (var batch in Litter.data.batches) matrices.AddRange(batch.matrices);
            return matrices.ToArray();
        }
    }
}
