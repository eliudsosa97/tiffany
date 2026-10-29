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
    public class StreetLampsTests
    {
        GameObject parent;
        string tempFolder;
        string previousMaterialFolder;

        [SetUp]
        public void SetUp()
        {
            tempFolder = "Assets/StreetLampsTestTemp_" + Guid.NewGuid().ToString("N");
            previousMaterialFolder = ModelLibrary.materialFolder;
            ModelLibrary.materialFolder = tempFolder + "/Materials";
            parent = new GameObject("StreetLampsTests");
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
            finally { ModelLibrary.materialFolder = previousMaterialFolder; }
        }

        [Test]
        public void FbxHasABasePivotSevenMeterPoleAndAtMostSixHundredTriangles()
        {
            string path = ModelLibrary.ModelFolder + "/street-lamp.fbx";
            Assert.IsTrue(File.Exists(path), "Genera street-lamp.fbx con Art/Blender/street-lamp.py antes de correr los tests.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(model, path);
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) ModelLibraryTests.AssertNeutral(node);
            Assert.IsEmpty(model.GetComponentsInChildren<Collider>(true));
            Assert.That(ModelLibraryTests.TriangleCount(model.transform), Is.InRange(1, 600));
            var bounds = ModelLibrary.LocalBounds(model.transform);
            Assert.AreEqual(0f, bounds.min.y, 0.001f);
            Assert.AreEqual(7f, bounds.size.y, 0.001f);
            var pole = model.transform.Find("Pole");
            Assert.IsNotNull(pole);
            var poleBounds = pole.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Less(new Vector2(poleBounds.center.x, poleBounds.center.z).magnitude, 0.02f);
            var bulb = model.transform.Find("Bulb");
            Assert.IsNotNull(bulb);
            var bulbBounds = bulb.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Less(Vector3.Distance(ModelLibrary.LoadSpec("street-lamp").lightOffset, bulbBounds.center), 0.001f);
            Assert.Greater(bulbBounds.min.z, 0.8f);
            Assert.AreEqual(0.21f, bulbBounds.size.x, 0.001f);
            Assert.AreEqual(0.012f, bulbBounds.size.y, 0.001f);
            Assert.AreEqual(0.56f, bulbBounds.size.z, 0.001f);
        }

        [Test]
        public void PlaceWiresOneWarmShadowlessPointLightAtTheJsonOffset()
        {
            var position = new Vector3(7f, 2f, -8f);
            var root = StreetLamps.Place(parent.transform, position, 65f, 127);
            Assert.AreSame(parent.transform, root.transform.parent);
            Assert.AreEqual(position, root.transform.localPosition);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0f, 65f, 0f), root.transform.localRotation), 0.001f);
            Assert.AreEqual(Vector3.one, root.transform.localScale);
            var lights = root.GetComponentsInChildren<Light>(true);
            Assert.AreEqual(1, lights.Length);
            var lamp = lights[0];
            Assert.AreEqual(LightType.Point, lamp.type);
            Assert.AreEqual(LightShadows.None, lamp.shadows);
            Assert.AreEqual(7f, lamp.range);
            Assert.LessOrEqual(lamp.range, 8f);
            Assert.That(lamp.intensity, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.AreEqual(new Color(1f, 196f / 255f, 107f / 255f), lamp.color);
            var offset = ModelLibrary.LoadSpec("street-lamp").lightOffset;
            Assert.AreEqual(offset, lamp.transform.localPosition);
            Assert.Less(Vector3.Distance(root.transform.TransformPoint(offset), lamp.transform.position), 0.001f);
            var component = root.GetComponent<StreetLamp>();
            Assert.IsNotNull(component);
            Assert.AreSame(lamp, component.lamp);
            Assert.AreEqual(127, component.seed);
            Assert.AreEqual(component.baseIntensity, lamp.intensity);
            Assert.IsNotNull(component.bulb);
            Assert.AreEqual("StreetLampBulb", component.bulb.sharedMaterial.name);
            Assert.AreEqual(1, component.bulb.sharedMaterials.Length);
            Assert.IsTrue(component.bulb.transform.IsChildOf(root.transform));
            Assert.AreEqual(22f, StreetLamps.Spacing);
            Assert.Greater(StreetLamps.Spacing, 2f * lamp.range);
        }

        [Test]
        public void FlickerUpdatesTheLightAndBulbWithoutChangingTheSharedMaterial()
        {
            var root = StreetLamps.Place(parent.transform, Vector3.zero, 0f, 127);
            var component = root.GetComponent<StreetLamp>();
            var material = component.bulb.sharedMaterial;
            int colorId = Shader.PropertyToID("_BaseColor");
            Color original = material.GetColor(colorId);
            component.SendMessage("Start");
            for (int i = 0; i < 100; i++) component.Apply(0.016f);
            float value = component.lamp.intensity / component.baseIntensity;
            Assert.That(value, Is.InRange(LampFlicker.MinIntensity, 1f));
            var properties = new MaterialPropertyBlock();
            component.bulb.GetPropertyBlock(properties);
            Color actual = properties.GetColor(colorId);
            for (int i = 0; i < 3; i++) Assert.AreEqual(original[i] * value, actual[i], 1e-5f);
            Assert.AreEqual(original.a, actual.a);
            Assert.AreSame(material, component.bulb.sharedMaterial);
            Assert.AreEqual(original, material.GetColor(colorId));
        }

        [Test]
        public void PoleHasOneVerticalColliderWithoutAGroundSurface()
        {
            var root = StreetLamps.Place(parent.transform, Vector3.zero, 0f, 1);
            var colliders = root.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length);
            Assert.IsInstanceOf<CapsuleCollider>(colliders[0]);
            var capsule = (CapsuleCollider)colliders[0];
            Assert.AreSame(root, capsule.gameObject);
            Assert.AreEqual(1, capsule.direction);
            Assert.AreEqual(new Vector3(0f, 3.5f, 0f), capsule.center);
            Assert.AreEqual(0.15f, capsule.radius);
            Assert.AreEqual(7f, capsule.height);
            Assert.IsFalse(capsule.isTrigger);
            Assert.IsEmpty(root.GetComponentsInChildren<GroundSurface>(true));
        }

        [Test]
        public void ModelUsesTheFourJsonMaterialsAndStaticBatchingWithoutShadows()
        {
            var root = StreetLamps.Place(parent.transform, Vector3.zero, 0f, 1);
            var spec = ModelLibrary.LoadSpec("street-lamp");
            CollectionAssert.AreEquivalent(new[] { "StreetLampConcrete", "StreetLampMetal", "StreetLampHousing", "StreetLampBulb" },
                Array.ConvertAll(spec.materials, entry => entry.name));
            var used = new HashSet<string>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Assert.IsTrue((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0);
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
                foreach (var material in renderer.sharedMaterials)
                {
                    used.Add(material.name);
                    var entry = Array.Find(spec.materials, candidate => candidate.name == material.name);
                    Assert.IsNotNull(entry, material.name);
                    Assert.AreEqual(material.name == "StreetLampBulb", entry.unlit);
                    Assert.AreEqual("Universal Render Pipeline/" + (entry.unlit ? "Unlit" : "Lit"), material.shader.name);
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(entry.color, out var expected));
                    var actual = material.GetColor("_BaseColor");
                    for (int i = 0; i < 4; i++) Assert.AreEqual(expected[i], actual[i], 1e-4f);
                    Assert.AreEqual($"{ModelLibrary.materialFolder}/{entry.name}.mat", AssetDatabase.GetAssetPath(material));
                    Assert.AreEqual(!entry.unlit, renderer.receiveShadows);
                    if (!entry.unlit)
                    {
                        Assert.AreEqual(entry.smoothness, material.GetFloat("_Smoothness"), 1e-4f);
                        Assert.AreEqual(entry.metallic, material.GetFloat("_Metallic"), 1e-4f);
                    }
                }
            }
            CollectionAssert.AreEquivalent(Array.ConvertAll(spec.materials, entry => entry.name), used);
        }
    }
}
