using System.IO;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public class ModelLibraryTests
    {
        const string TempFolder = "Assets/ModelLibraryTestTemp";
        GameObject parent;

        [SetUp]
        public void SetUp()
        {
            ModelLibrary.materialFolder = TempFolder + "/Materials";
            parent = new GameObject("ModelLibraryTests");
            parent.transform.SetPositionAndRotation(new Vector3(4f, 3f, -2f), Quaternion.Euler(10f, 30f, 5f));
            parent.transform.localScale = new Vector3(2f, 3f, 4f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
            AssetDatabase.DeleteAsset(TempFolder);
            ModelLibrary.materialFolder = ModelLibrary.MaterialFolder;
        }

        [Test]
        public void WitchFbxHasBothPosesWithinBudget()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelLibrary.ModelFolder + "/witch.fbx");
            Assert.IsNotNull(model);
            foreach (string pose in new[] { "WitchStand", "WitchCrouch" })
            {
                var mesh = model.transform.Find(pose).GetComponent<MeshFilter>().sharedMesh;
                long indices = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
                Assert.That(indices / 3, Is.InRange(1000, 6000), pose);
            }
        }

        [Test]
        public void WitchStandsOnHerFeetFacingForward()
        {
            var root = ModelLibrary.Instantiate("witch", parent.transform);
            Assert.AreEqual("witch", root.name);
            Assert.AreSame(parent.transform, root.transform.parent);
            AssertNeutral(root.transform);

            var stand = LocalBounds(root.transform, "WitchStand");
            Assert.That(stand.size.y, Is.InRange(2.25f, 2.4f));
            Assert.AreEqual(0f, stand.min.y, 0.01f);
            Assert.Less(new Vector2(stand.center.x, stand.center.z).magnitude, 0.3f);

            var crouch = LocalBounds(root.transform, "WitchCrouch");
            Assert.Less(crouch.size.y, 1.8f);
            Assert.AreEqual(0f, crouch.min.y, 0.01f);
            // Las manos y la cabeza van por delante; al revés este margen se invierte.
            Assert.Greater(crouch.max.z, 2f * -crouch.min.z);
            Assert.Greater(stand.max.z, 2f * -stand.min.z);
        }

        [Test]
        public void WitchUsesSyncedOpaqueMaterialsAndNoColliders()
        {
            var root = ModelLibrary.Instantiate("witch", parent.transform);
            Assert.IsEmpty(root.GetComponentsInChildren<Collider>(true));
            var spec = ModelLibrary.LoadSpec("witch");
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.AreEqual(2, renderers.Length);
            foreach (var renderer in renderers)
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name);
                    Assert.AreEqual($"{ModelLibrary.materialFolder}/{material.name}.mat", AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
                    Assert.IsEmpty(material.shaderKeywords, material.name);
                    Assert.AreEqual(0f, material.GetFloat("_Surface"));
                    Assert.AreEqual(0f, material.GetFloat("_AlphaClip"));
                    Assert.IsNull(material.GetTexture("_BaseMap"));
                    Assert.IsNull(material.GetTexture("_EmissionMap"));
                    var entry = System.Array.Find(spec.materials, m => m.name == material.name);
                    Assert.IsNotNull(entry, material.name);
                    ColorUtility.TryParseHtmlString(entry.color, out var color);
                    var baseColor = material.GetColor("_BaseColor");
                    for (int c = 0; c < 4; c++) Assert.AreEqual(color[c], baseColor[c], 1e-4f, material.name);
                    Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 1e-4f);
                }
            }
        }

        [Test]
        public void SyncMaterialsIsStable()
        {
            ModelLibrary.SyncMaterials("witch");
            string path = $"{ModelLibrary.materialFolder}/WitchSkin.mat";
            string guid = AssetDatabase.AssetPathToGUID(path);
            string text = File.ReadAllText(path);
            ModelLibrary.SyncMaterials("witch");
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(text, File.ReadAllText(path));
        }

        [Test]
        public void MissingSourceFailsWithPrefix()
        {
            var error = Assert.Throws<FileNotFoundException>(() => ModelLibrary.SyncMaterials("no-model"));
            StringAssert.StartsWith(GameInfo.LogPrefix, error.Message);
        }

        static void AssertNeutral(Transform t)
        {
            Assert.AreEqual(Vector3.zero, t.localPosition, t.name);
            Assert.AreEqual(Quaternion.identity, t.localRotation, t.name);
            Assert.AreEqual(Vector3.one, t.localScale, t.name);
        }

        static Bounds LocalBounds(Transform root, string pose)
        {
            var child = root.Find(pose);
            Assert.IsNotNull(child, pose);
            Assert.AreSame(root, child.parent);
            AssertNeutral(child);
            return child.GetComponent<MeshFilter>().sharedMesh.bounds;
        }
    }
}
