using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiffanyGame.Tests
{
    public class RiverPiecesTests
    {
        static readonly string[] ModelIds = { "river-rock-1", "river-rock-2", "river-rock-3", "river-rock-4",
            "river-step-1", "river-step-2", "river-log", "river-bed-straight", "river-bed-curve", "plant-tussock" };
        GameObject parent;
        string tempFolder;
        string previousMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            tempFolder = "Assets/RiverPiecesTestTemp_" + Guid.NewGuid().ToString("N");
            previousMaterialFolder = ModelLibrary.materialFolder;
            ModelLibrary.materialFolder = tempFolder + "/Materials";
            parent = new GameObject("RiverPiecesTests");
            parent.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            parent.transform.localScale = new Vector3(2f, 3f, 4f);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Object.DestroyImmediate(parent);
                if (AssetDatabase.IsValidFolder(tempFolder)) AssetDatabase.DeleteAsset(tempFolder);
            }
            finally
            {
                ModelLibrary.materialFolder = previousMaterialFolder;
            }
        }

        [TestCaseSource(nameof(ModelIds))]
        public void FbxHasExpectedPivotAndTriangleBudget(string id)
        {
            var model = Model(id);
            int budget = id.StartsWith("river-bed-", StringComparison.Ordinal) ? 1500
                : id == "river-log" ? 500 : id == "plant-tussock" ? 600 : 300;
            int minimum = id.StartsWith("river-bed-", StringComparison.Ordinal) ? 1400
                : id == "river-log" ? 450 : id == "plant-tussock" ? 580
                : id.StartsWith("river-step-", StringComparison.Ordinal) ? 180 : 200;
            Assert.That(ModelLibraryTests.TriangleCount(model.transform), Is.InRange(minimum, budget), id);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) ModelLibraryTests.AssertNeutral(node);
            var bounds = ModelLibrary.LocalBounds(model.transform);
            if (id.StartsWith("river-bed-", StringComparison.Ordinal))
                Assert.That(bounds.min.y, Is.InRange(-1.49f, -1.46f), id);
            else
            {
                bool buried = id.StartsWith("river-rock-", StringComparison.Ordinal) || id == "plant-tussock";
                Assert.That(bounds.min.y, buried ? Is.InRange(-0.04f, -0.02f) : Is.InRange(-0.02f, 0.02f), id);
                Assert.AreEqual(0f, bounds.center.x, 0.02f, id);
                Assert.AreEqual(0f, bounds.center.z, 0.02f, id);
            }
            if (id == "plant-tussock") Assert.That(bounds.size.y, Is.InRange(0.5f, 0.8f));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RockHasBankScaleAndAnObstacleCollider(int index)
        {
            var rock = RiverPieces.Rock(index, parent.transform);
            var size = ModelLibrary.LocalBounds(rock.transform).size;
            Assert.That(size.x, Is.InRange(0.5f, 1.2f));
            if (index == 1) Assert.Greater(size.x / size.z, 1.6f);
            if (index == 3) Assert.Greater(size.x / size.y, 2.5f);
            if (index == 4) Assert.Greater(size.y / size.x, 0.8f);
            AssertStoneCollider(rock);
            Assert.IsEmpty(rock.GetComponentsInChildren<GroundSurface>(true));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void StepHasAWideNearlyLevelTopAndGravelOnItsCollider(int index)
        {
            var step = RiverPieces.Step(index, parent.transform);
            var bounds = ModelLibrary.LocalBounds(step.transform);
            Assert.That(bounds.size.x, Is.InRange(0.7f, 0.9f));
            Assert.That(bounds.size.y, Is.InRange(0.25f, 0.35f));
            if (index == 1) Assert.Greater(bounds.size.x / bounds.size.z, 1.25f);
            else Assert.That(bounds.size.x / bounds.size.z, Is.InRange(0.95f, 1.15f));
            AssertStoneCollider(step);
            var collider = step.GetComponentInChildren<MeshCollider>(true);
            Assert.IsNotNull(collider.GetComponent<GroundSurface>());
            Assert.AreEqual(GroundType.Gravel, GroundSurface.TypeOf(collider));
            float area = 0f;
            float lowerHalfWidth = 0f, upperHalfWidth = 0f;
            int topFaces = 0;
            bool NearCenter(Vector3 point)
            {
                var offset = point - bounds.center;
                float x = offset.x / bounds.extents.x, z = offset.z / bounds.extents.z;
                return x * x + z * z < 0.25f;
            }
            foreach (var filter in step.GetComponentsInChildren<MeshFilter>(true))
            {
                var vertices = LocalVertices(filter, step.transform);
                foreach (var point in vertices)
                {
                    float halfWidth = Mathf.Abs(point.x - bounds.center.x);
                    if (point.y < bounds.min.y + 0.02f) lowerHalfWidth = Mathf.Max(lowerHalfWidth, halfWidth);
                    if (point.y > bounds.max.y - 0.05f) upperHalfWidth = Mathf.Max(upperHalfWidth, halfWidth);
                }
                var indices = filter.sharedMesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < bounds.max.y - 0.06f) continue;
                    if (!NearCenter(a) && !NearCenter(b) && !NearCenter(c)) continue;
                    var cross = Vector3.Cross(b - a, c - a);
                    Assert.Greater(cross.sqrMagnitude, 1e-10f);
                    Assert.GreaterOrEqual(cross.normalized.y, Mathf.Cos(6f * Mathf.Deg2Rad));
                    area += cross.y * 0.5f;
                    topFaces++;
                }
            }
            Assert.GreaterOrEqual(topFaces, 24);
            Assert.Greater(area, 0.25f);
            Assert.Greater(lowerHalfWidth, upperHalfWidth * 1.04f);
        }

        [Test]
        public void LogSpansTheCrossingWithALongitudinalCapsuleAndBranches()
        {
            var log = RiverPieces.Log(parent.transform);
            var bounds = ModelLibrary.LocalBounds(log.transform);
            Assert.That(bounds.size.z, Is.InRange(4.3f, 4.7f));
            Assert.AreEqual(0.45f, bounds.size.y, 0.02f);
            float tipLow = float.PositiveInfinity, tipHigh = float.NegativeInfinity;
            float cutLow = float.PositiveInfinity, cutHigh = float.NegativeInfinity;
            foreach (var filter in log.GetComponentsInChildren<MeshFilter>(true))
                foreach (var point in LocalVertices(filter, log.transform))
                {
                    if (point.z < bounds.min.z + 0.25f)
                    {
                        tipLow = Mathf.Min(tipLow, point.y);
                        tipHigh = Mathf.Max(tipHigh, point.y);
                    }
                    if (point.z > bounds.max.z - 0.01f)
                    {
                        cutLow = Mathf.Min(cutLow, point.y);
                        cutHigh = Mathf.Max(cutHigh, point.y);
                    }
                }
            Assert.AreEqual(0.38f, tipHigh - tipLow, 0.025f);
            Assert.AreEqual(0.45f, cutHigh - cutLow, 0.025f);
            Assert.Greater(cutHigh - cutLow - (tipHigh - tipLow), 0.04f);
            var colliders = log.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length);
            Assert.IsInstanceOf<CapsuleCollider>(colliders[0]);
            var capsule = (CapsuleCollider)colliders[0];
            Assert.AreSame(log, capsule.gameObject);
            Assert.AreEqual(2, capsule.direction);
            Assert.AreEqual(new Vector3(0f, 0.225f, 0f), capsule.center);
            Assert.AreEqual(0.225f, capsule.radius, 1e-4f);
            Assert.AreEqual(4.5f, capsule.height, 1e-4f);
            Assert.IsFalse(capsule.isTrigger);
            Assert.AreEqual(GroundType.Branches, GroundSurface.TypeOf(capsule));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BankProfileHasTheCorrectStartEndArcAndTerrainClearance(bool curve)
        {
            Assert.AreEqual(10f, RiverPieces.BankLength);
            Assert.AreEqual(18f, RiverPieces.BankCurveRadius);
            var model = Model(curve ? "river-bed-curve" : "river-bed-straight");
            var filter = Array.Find(model.GetComponentsInChildren<MeshFilter>(true), f => f.name == "BankProfile");
            Assert.IsNotNull(filter);
            var vertices = LocalVertices(filter, model.transform);
            float maxAlong = 0f, minInset = float.PositiveInfinity, maxInset = float.NegativeInfinity;
            float minDryHeight = float.PositiveInfinity, maxDryHeight = float.NegativeInfinity;
            Vector3 start = default, end = default;
            bool hasStart = false, hasEnd = false;
            var terrain = new RouteTerrain();
            foreach (var point in vertices)
            {
                var local = BankCoordinates(point, curve);
                Assert.That(local.y, Is.InRange(-0.001f, RiverPieces.BankLength + 0.001f));
                maxAlong = Mathf.Max(maxAlong, local.y);
                minInset = Mathf.Min(minInset, local.x);
                maxInset = Mathf.Max(maxInset, local.x);
                AssertClearance(point, curve);
                if (local.x < 0f)
                {
                    minDryHeight = Mathf.Min(minDryHeight, point.y);
                    maxDryHeight = Mathf.Max(maxDryHeight, point.y);
                }
                if (Mathf.Abs(local.x) < 0.001f)
                {
                    if (local.y < 0.001f) { start = point; hasStart = true; }
                    if (local.y > RiverPieces.BankLength - 0.001f) { end = point; hasEnd = true; }
                }
                if (!curve && point.z < 0.001f)
                {
                    float ground = terrain.GroundHeight(terrain.Crossing.x,
                        terrain.Crossing.z + RouteTerrain.RiverWidth * 0.5f - point.x);
                    Assert.That(point.y + terrain.Crossing.y - ground, Is.InRange(0.019f, 0.041f));
                }
            }
            Assert.IsTrue(hasStart && hasEnd);
            Assert.Less(Vector3.Distance(new Vector3(0f, 0.03f, 0f), start), 0.001f);
            Assert.AreEqual(10f, maxAlong, 0.2f);
            Assert.AreEqual(-1.2f, minInset, 0.001f);
            Assert.AreEqual(0.9f, maxInset, 0.001f);
            float radius = RiverPieces.BankCurveRadius, angle = RiverPieces.BankLength / radius;
            var expected = curve ? new Vector3(radius * (1f - Mathf.Cos(angle)), 0.03f, radius * Mathf.Sin(angle))
                : new Vector3(0f, 0.03f, 10f);
            Assert.Less(Vector3.Distance(expected, end), 0.001f);
            float chord = curve ? 2f * radius * Mathf.Sin(angle * 0.5f) : 10f;
            Assert.AreEqual(chord, Vector3.Distance(start, end), 0.001f);
            Assert.Greater(maxDryHeight - minDryHeight, 0.03f);
            float weightedClearance = 0f, area = 0f;
            var indices = filter.sharedMesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                var cross = Vector3.Cross(b - a, c - a);
                Assert.Greater(cross.y, 0f);
                weightedClearance += AssertClearance((a + b + c) / 3f, curve) * cross.magnitude;
                area += cross.magnitude;
                AssertClearance((a + b) / 2f, curve);
                AssertClearance((b + c) / 2f, curve);
                AssertClearance((c + a) / 2f, curve);
            }
            Assert.That(weightedClearance / area, Is.InRange(0.02f, 0.04f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BankHasAnUndercutAboveTerrainAndRootsReachingTheWater(bool curve)
        {
            var model = Model(curve ? "river-bed-curve" : "river-bed-straight");
            var filters = model.GetComponentsInChildren<MeshFilter>(true);
            var erosion = Array.Find(filters, filter => filter.name == "BankErosion");
            Assert.IsNotNull(erosion);
            var vertices = LocalVertices(erosion, model.transform);
            foreach (var point in vertices) AssertClearance(point, curve);
            var triangles = erosion.sharedMesh.triangles;
            bool hasUnderside = false;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                if (Vector3.Cross(b - a, c - a).normalized.y < -0.2f) hasUnderside = true;
            }
            Assert.IsTrue(hasUnderside);
            var roots = Array.Find(filters, filter => filter.name == "BankRoots");
            Assert.IsNotNull(roots);
            float lowestRoot = float.PositiveInfinity;
            foreach (var point in LocalVertices(roots, model.transform)) lowestRoot = Mathf.Min(lowestRoot, point.y);
            Assert.Less(lowestRoot, -0.35f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BankMirrorsOnlyAcrossXAndHasNoCollider(bool curve)
        {
            var original = RiverPieces.Bank(curve, parent.transform);
            var mirrored = RiverPieces.Bank(curve, parent.transform, true);
            Assert.AreEqual(new Vector3(-1f, 1f, 1f), mirrored.transform.localScale);
            Assert.IsEmpty(original.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(mirrored.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(mirrored.GetComponentsInChildren<GroundSurface>(true));
            var filters = original.GetComponentsInChildren<MeshFilter>(true);
            var reflectedFilters = mirrored.GetComponentsInChildren<MeshFilter>(true);
            Assert.AreEqual(filters.Length, reflectedFilters.Length);
            for (int i = 0; i < filters.Length; i++)
            {
                Assert.AreSame(filters[i].sharedMesh, reflectedFilters[i].sharedMesh);
                var points = LocalVertices(filters[i], parent.transform);
                var reflected = LocalVertices(reflectedFilters[i], parent.transform);
                for (int j = 0; j < points.Length; j++)
                    Assert.Less(Vector3.Distance(new Vector3(-points[j].x, points[j].y, points[j].z), reflected[j]), 1e-4f);
            }
        }

        [TestCaseSource(nameof(ModelIds))]
        public void ApiUsesJsonMaterialsStaticBatchingAndOnlyLargeRocksCastShadows(string id)
        {
            var root = Create(id);
            Assert.AreEqual(id, root.name);
            Assert.AreSame(parent.transform, root.transform.parent);
            ModelLibraryTests.AssertNeutral(root.transform);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                Assert.IsTrue((GameObjectUtility.GetStaticEditorFlags(node.gameObject) & StaticEditorFlags.BatchingStatic) != 0);
            var spec = ModelLibrary.LoadSpec(id);
            var used = new HashSet<string>();
            bool largeRock = id.StartsWith("river-rock-", StringComparison.Ordinal) && ModelLibrary.LocalBounds(root.transform).size.x >= 1f;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Assert.AreEqual(largeRock ? ShadowCastingMode.On : ShadowCastingMode.Off, renderer.shadowCastingMode);
                Assert.IsTrue(renderer.receiveShadows);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material);
                    used.Add(material.name);
                    Assert.AreEqual($"{ModelLibrary.materialFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                    var entry = Array.Find(spec.materials, candidate => candidate.name == material.name);
                    Assert.IsNotNull(entry, material.name);
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.color, out var expected));
                    for (int channel = 0; channel < 4; channel++)
                        Assert.AreEqual(expected[channel], material.GetColor("_BaseColor")[channel], 1e-4f, material.name);
                    Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 1e-4f);
                    Assert.AreEqual(entry.metallic, material.GetFloat("_Metallic"), 1e-4f);
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.AreEqual(0f, material.GetFloat("_Surface"));
                    Assert.AreEqual(0f, material.GetFloat("_AlphaClip"));
                    Assert.IsEmpty(material.shaderKeywords);
                    foreach (string property in material.GetTexturePropertyNames()) Assert.IsNull(material.GetTexture(property));
                }
            }
            CollectionAssert.AreEquivalent(Array.ConvertAll(spec.materials, entry => entry.name), used);
            if (id.StartsWith("river-rock-", StringComparison.Ordinal) || id.StartsWith("river-step-", StringComparison.Ordinal))
            {
                var dry = Array.Find(spec.materials, entry => entry.name == "RiverGraniteDry");
                var wet = Array.Find(spec.materials, entry => entry.name == "RiverGraniteWet");
                Assert.AreEqual(2, spec.materials.Length);
                Assert.Greater(wet.smoothness, dry.smoothness);
                ColorUtility.TryParseHtmlString(dry.color, out var dryColor);
                ColorUtility.TryParseHtmlString(wet.color, out var wetColor);
                Assert.Less(wetColor.grayscale, dryColor.grayscale);
            }
            if (id == "plant-tussock" || id.StartsWith("river-bed-", StringComparison.Ordinal))
            {
                Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
                Assert.IsEmpty(root.GetComponentsInChildren<GroundSurface>(true));
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(5)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidRockIndexThrowsBeforeCreatingAnything(int index)
        {
            Assert.AreEqual("index", Assert.Throws<ArgumentOutOfRangeException>(() => RiverPieces.Rock(index, parent.transform)).ParamName);
            Assert.AreEqual(0, parent.transform.childCount);
            Assert.IsFalse(AssetDatabase.IsValidFolder(tempFolder));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(int.MinValue)]
        [TestCase(int.MaxValue)]
        public void InvalidStepIndexThrowsBeforeCreatingAnything(int index)
        {
            Assert.AreEqual("index", Assert.Throws<ArgumentOutOfRangeException>(() => RiverPieces.Step(index, parent.transform)).ParamName);
            Assert.AreEqual(0, parent.transform.childCount);
            Assert.IsFalse(AssetDatabase.IsValidFolder(tempFolder));
        }

        static GameObject Model(string id)
        {
            string path = $"{ModelLibrary.ModelFolder}/{id}.fbx";
            Assert.IsTrue(File.Exists(path), $"Genera {path} con Art/Blender/{id}.py antes de correr los tests.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(model, path);
            return model;
        }

        GameObject Create(string id)
        {
            if (id.StartsWith("river-rock-", StringComparison.Ordinal)) return RiverPieces.Rock(id[id.Length - 1] - '0', parent.transform);
            if (id.StartsWith("river-step-", StringComparison.Ordinal)) return RiverPieces.Step(id[id.Length - 1] - '0', parent.transform);
            if (id == "river-log") return RiverPieces.Log(parent.transform);
            if (id == "plant-tussock") return RiverPieces.Plant(parent.transform);
            return RiverPieces.Bank(id == "river-bed-curve", parent.transform);
        }

        static void AssertStoneCollider(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length);
            Assert.IsInstanceOf<MeshCollider>(colliders[0]);
            var collider = (MeshCollider)colliders[0];
            Assert.IsTrue(collider.convex);
            Assert.IsFalse(collider.isTrigger);
            Assert.AreSame(collider.GetComponent<MeshFilter>().sharedMesh, collider.sharedMesh);
        }

        static Vector3[] LocalVertices(MeshFilter filter, Transform root)
        {
            var vertices = filter.sharedMesh.vertices;
            var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            return vertices;
        }

        static Vector2 BankCoordinates(Vector3 point, bool curve)
        {
            if (!curve) return new Vector2(point.x, point.z);
            float radius = RiverPieces.BankCurveRadius;
            var radial = new Vector2(radius - point.x, point.z);
            return new Vector2(radius - radial.magnitude, Mathf.Atan2(radial.y, radial.x) * radius);
        }

        static float AssertClearance(Vector3 point, bool curve)
        {
            float inset = BankCoordinates(point, curve).x;
            float ground = -1.5f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inset / 0.8f));
            float clearance = point.y - ground;
            Assert.That(clearance, Is.InRange(0.0099f, 0.1201f), point.ToString("F4"));
            return clearance;
        }
    }
}
