using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class ForestModelTests
    {
        const string TempFolder = "Assets/ForestModelTestTemp";
        readonly List<string> createdMaterials = new List<string>();
        GameObject parent;
        string previousMaterialFolder;
        string previousKenneyMaterialRoot;

        [SetUp]
        public void SetUp()
        {
            previousMaterialFolder = ModelLibrary.materialFolder;
            previousKenneyMaterialRoot = ModelLibrary.kenneyMaterialRoot;
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
            ModelLibrary.kenneyMaterialRoot = TempFolder;
            createdMaterials.Clear();
            foreach (string id in new[] { "oak", "bougainvillea" })
            foreach (var material in ModelLibrary.LoadSpec(id).materials)
            {
                string path = $"{ModelLibrary.MaterialFolder}/{material.name}.mat";
                if (!File.Exists(path) && !createdMaterials.Contains(path)) createdMaterials.Add(path);
            }
            parent = new GameObject("ForestModelTests");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            foreach (string path in createdMaterials) AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = previousMaterialFolder;
            ModelLibrary.kenneyMaterialRoot = previousKenneyMaterialRoot;
        }

        [TestCase("oak", "Trunk,Canopy", 1500, 6f, 8f)]
        [TestCase("bougainvillea", "Bush", 800, 1.7f, 2.2f)]
        public void ImportedModelMeetsGeometryAndMaterialContract(string id, string children, int budget,
            float minHeight, float maxHeight)
        {
            string path = $"{ModelLibrary.ModelFolder}/{id}.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(asset, $"Genera {id}.fbx con Art/Blender/{id}.py antes de correr los tests.");
            Assert.IsEmpty(asset.GetComponentsInChildren<Collider>(true));
            foreach (var transform in asset.GetComponentsInChildren<Transform>(true)) AssertNeutral(transform);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(importer.preserveHierarchy);
            Assert.IsFalse(importer.addCollider);

            var root = ModelLibrary.Instantiate(id, parent.transform);
            AssertNeutral(root.transform);
            Assert.AreSame(parent.transform, root.transform.parent);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
            var expectedNames = children.Split(',');
            Assert.AreEqual(expectedNames.Length, root.transform.childCount);
            Assert.AreEqual(expectedNames.Length, root.GetComponentsInChildren<MeshFilter>().Length);
            long triangles = 0;
            Bounds bounds = default;
            for (int i = 0; i < expectedNames.Length; i++)
            {
                var child = root.transform.Find(expectedNames[i]);
                Assert.IsNotNull(child, expectedNames[i]);
                Assert.AreSame(root.transform, child.parent);
                AssertNeutral(child);
                var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Assert.AreEqual(MeshTopology.Triangles, mesh.GetTopology(submesh));
                    triangles += mesh.GetIndexCount(submesh) / 3;
                }
                if (i == 0) bounds = mesh.bounds;
                else bounds.Encapsulate(mesh.bounds);
            }
            Assert.That(triangles, Is.InRange(1, budget), id);
            Assert.AreEqual(0f, bounds.min.y, 0.01f, id);
            Assert.AreEqual(0f, bounds.center.x, 0.01f, id);
            Assert.AreEqual(0f, bounds.center.z, 0.01f, id);
            Assert.That(bounds.size.y, Is.InRange(minHeight, maxHeight), id);
            if (id == "bougainvillea") Assert.That(bounds.size.x, Is.InRange(2.3f, 2.8f));
            foreach (var placement in ForestLayout.Placements)
                if (placement.id == id)
                    Assert.LessOrEqual(new Vector2(bounds.extents.x, bounds.extents.z).magnitude * placement.scale,
                        placement.Radius, id);
            AssertMaterials(root, id);
        }

        [TestCase("tree_pineTallA_detailed")]
        [TestCase("tree_pineTallC_detailed")]
        [TestCase("tree_pineRoundC")]
        [TestCase("rock_largeA")]
        [TestCase("stone_tallA")]
        [TestCase("rock_smallC")]
        [TestCase("log")]
        [TestCase("stump_oldTall")]
        public void KenneyFitsHeightBaseAndFootprintWithSavedOpaqueMaterials(string id)
        {
            var placement = new ForestLayout.Placement();
            foreach (var candidate in ForestLayout.Placements)
                if (candidate.id == "Nature/" + id) placement = candidate;
            Assert.IsTrue(placement.IsKenney, id);
            var importer = (ModelImporter)AssetImporter.GetAtPath($"{ModelLibrary.KenneyFolder}/Nature/Models/{id}.fbx");
            Assert.IsNotNull(importer);
            Assert.IsFalse(importer.addCollider || importer.importAnimation || importer.importCameras ||
                importer.importLights || importer.importBlendShapes);
            Assert.AreEqual(ModelImporterAnimationType.None, importer.animationType);
            Assert.AreEqual(ModelImporterMaterialImportMode.ImportViaMaterialDescription, importer.materialImportMode);
            var root = ModelLibrary.InstantiateKenney(placement.id, parent.transform, placement.height);
            AssertNeutral(root.transform);
            Assert.AreSame(parent.transform, root.transform.parent);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
            var renderers = root.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.AreEqual(placement.height, bounds.size.y, 1e-4f);
            Assert.AreEqual(0f, bounds.min.y, 1e-4f);
            Assert.AreEqual(0f, bounds.center.x, 1e-4f);
            Assert.AreEqual(0f, bounds.center.z, 1e-4f);
            Assert.LessOrEqual(new Vector2(bounds.extents.x, bounds.extents.z).magnitude * placement.scale, placement.Radius);
            string palettePath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Art/Kenney/nature.json");
            var palette = JsonUtility.FromJson<ModelLibrary.ModelSpec>(File.ReadAllText(palettePath));
            foreach (var renderer in renderers)
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
                Assert.IsTrue(renderer.receiveShadows);
                foreach (var material in renderer.sharedMaterials)
                {
                    string path = $"{ModelLibrary.kenneyMaterialRoot}/Nature/Materials/{material.name}.mat";
                    Assert.AreEqual(path, AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.AreEqual(0f, material.GetFloat("_Surface"));
                    Assert.AreEqual(0f, material.GetFloat("_AlphaClip"));
                    Assert.IsEmpty(material.shaderKeywords);
                    foreach (string property in material.GetTexturePropertyNames()) Assert.IsNull(material.GetTexture(property));
                    var entry = System.Array.Find(palette.materials, candidate => "KenneyNature" + candidate.name == material.name);
                    Assert.IsNotNull(entry, material.name);
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.color, out var expected));
                    for (int channel = 0; channel < 4; channel++)
                        Assert.AreEqual(expected[channel], material.GetColor("_BaseColor")[channel], 1e-4f);
                }
            }
            parent.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            parent.transform.localScale = new Vector3(2f, 3f, 4f);
            var second = ModelLibrary.InstantiateKenney(placement.id, parent.transform, placement.height);
            AssertNeutral(second.transform);
            Assert.Less(Vector3.Distance(bounds.size, ModelLibrary.LocalBounds(second.transform).size), 1e-4f);
            Assert.Less(Vector3.Distance(bounds.center, ModelLibrary.LocalBounds(second.transform).center), 1e-4f);
            var secondRenderers = second.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
                CollectionAssert.AreEqual(renderers[i].sharedMaterials, secondRenderers[i].sharedMaterials);
        }

        static void AssertMaterials(GameObject root, string id)
        {
            var spec = ModelLibrary.LoadSpec(id);
            var usedNames = new HashSet<string>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
                Assert.IsTrue(renderer.receiveShadows);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material);
                    usedNames.Add(material.name);
                    Assert.AreEqual($"{ModelLibrary.MaterialFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                    var entry = System.Array.Find(spec.materials, candidate => candidate.name == material.name);
                    Assert.IsNotNull(entry, material.name);
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.color, out var expected));
                    var actual = material.GetColor("_BaseColor");
                    for (int channel = 0; channel < 4; channel++)
                        Assert.AreEqual(expected[channel], actual[channel], 1e-4f, material.name);
                    Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 1e-4f);
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.AreEqual(0f, material.GetFloat("_Surface"));
                    Assert.IsNull(material.GetTexture("_BaseMap"));
                }
            }
            CollectionAssert.AreEquivalent(System.Array.ConvertAll(spec.materials, entry => entry.name), usedNames);
        }

        static void AssertNeutral(Transform transform)
        {
            Assert.AreEqual(Vector3.zero, transform.localPosition, transform.name);
            Assert.Less(Quaternion.Angle(Quaternion.identity, transform.localRotation), 0.01f, transform.name);
            Assert.AreEqual(Vector3.one, transform.localScale, transform.name);
        }
    }
}
