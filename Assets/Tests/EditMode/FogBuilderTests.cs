using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiffanyGame.Tests
{
    public sealed class FogBuilderTests
    {
        const string TempFolder = "Assets/FogBuilderTestTemp";
        GameObject world;
        string previousFolder;

        [SetUp]
        public void SetUp()
        {
            world = new GameObject(nameof(FogBuilderTests));
            previousFolder = GeneratedMaterials.folder;
            GeneratedMaterials.folder = TempFolder + "/Materials";
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(world);
            AssetDatabase.DeleteAsset(TempFolder);
            GeneratedMaterials.folder = previousFolder;
        }

        [TestCase("_ADDITIONAL_LIGHTS")]
        [TestCase("_ADDITIONAL_LIGHTS_VERTEX")]
        [TestCase("_CLUSTER_LIGHT_LOOP")]
        public void GroundFogShaderCompilesWithoutErrors(string lightingKeyword)
        {
            var material = GeneratedMaterials.GetGroundFog();
            material.EnableKeyword(lightingKeyword);
            material.EnableKeyword("FOG_LINEAR");
            ShaderUtil.CompilePass(material, 0, true);
            var messages = System.Array.ConvertAll(ShaderUtil.GetShaderMessages(material.shader),
                message => $"{message.file}({message.line}) [{message.platform}]: {message.severity}: {message.message}\n{message.messageDetails}");
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), string.Join("\n", messages));
        }

        [Test]
        public void RegeneratingMaterialReplacesLegacyFadesAndDrawsFogAfterWater()
        {
            var material = GeneratedMaterials.GetGroundFog();
            material.SetColor("_BaseColor", new Color(0.08f, 0.11f, 0.16f, 0.85f));
            material.SetFloat("_NoiseScale", 0.24f);
            material.SetFloat("_LightStrength", 1.5f);
            material.SetFloat("_EdgeFade", 0.12f);
            material.SetFloat("_PlayerFadeEnd", 6f);
            material.SetFloat("_CameraFadeEnd", 12f);
            material.renderQueue = (int)RenderQueue.Transparent;
            Assert.AreSame(material, GeneratedMaterials.GetGroundFog());
            Color color = material.GetColor("_BaseColor").linear;
            float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
            Assert.That(luminance, Is.InRange(0.1f, 0.18f));
            Assert.AreEqual(0.45f, color.a);
            Assert.AreEqual(0.65f, material.GetFloat("_NoiseScale"));
            Assert.AreEqual(2.5f, material.GetFloat("_NoiseStretch"));
            Assert.AreEqual(0.35f, material.GetFloat("_LightStrength"));
            Assert.AreEqual(0.2f, material.GetFloat("_EdgeFade"));
            Assert.AreEqual(0.35f, material.GetFloat("_HeightFadeStart"));
            Assert.AreEqual(1.1f, material.GetFloat("_HeightFadeEnd"));
            Assert.AreEqual(2f, material.GetFloat("_PlayerFadeEnd"));
            Assert.AreEqual(4f, material.GetFloat("_CameraFadeEnd"));
            Assert.AreEqual(4.5f, material.GetFloat("_HaloRadius"));
            Assert.AreEqual((int)RenderQueue.Transparent + 10, material.renderQueue);
            Assert.Greater(material.renderQueue, GeneratedMaterials.GetWater().renderQueue);
        }

        [TestCase("Mobile")]
        [TestCase("PC")]
        public void RenderProfilesDoNotRequireDepthForGroundFog(string profile)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>($"Assets/Settings/{profile}_RPAsset.asset");
            Assert.IsNotNull(asset);
            using var serialized = new SerializedObject(asset);
            Assert.IsFalse(serialized.FindProperty("m_RequireDepthTexture").boolValue);
        }

        [Test]
        public void ClearingLayersReachTreeBandsOnBothSidesOfBothEntrances()
        {
            var clearings = Clearings(1);
            var fog = FogBuilder.BuildLayers(world.transform, clearings);
            Vector3 forward = clearings[0].forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            foreach (float across in new[] { -11f, 11f })
                foreach (float along in new[] { -17f, 17f })
                {
                    Vector3 treeBase = clearings[0].position + side * across + forward * along;
                    Assert.IsTrue(System.Array.Exists(fog.layers, layer =>
                    {
                        Vector3 local = layer.renderer.transform.InverseTransformPoint(treeBase);
                        return Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.y) < 0.5f;
                    }), $"Sin neblina entre los árboles del borde en {treeBase}.");
                }
        }

        [Test]
        public void EightClearingsInACircleHaveLayersAtEveryClearingAndConsecutiveSegment()
        {
            var clearings = Clearings(8);
            var fog = FogBuilder.BuildLayers(world.transform, clearings);
            Assert.AreEqual(23, fog.layers.Length, "Ocho claros por dos tarjetas y siete tramos por una.");
            Assert.AreEqual(FogBuilder.MaxCards(clearings.Length), fog.layers.Length);
            for (int i = 0; i < clearings.Length; i++)
            {
                var clearingLayers = System.Array.FindAll(fog.layers, layer => layer.renderer.name.StartsWith($"Clearing {i + 1} Fog "));
                Assert.AreEqual(FogBuilder.CardsPerClearing, clearingLayers.Length);
                foreach (var layer in clearingLayers)
                {
                    Vector3 offset = layer.renderer.transform.position - clearings[i].position;
                    Assert.That(offset.y, Is.InRange(FogBuilder.MinHeight - 0.001f, FogBuilder.MaxHeight + 0.001f));
                    offset.y = 0f;
                    Assert.LessOrEqual(offset.magnitude, 10.01f);
                }
                if (i + 1 >= clearings.Length) continue;
                var segmentLayers = System.Array.FindAll(fog.layers, layer => layer.renderer.name.StartsWith($"Segment {i + 1} Fog "));
                Assert.AreEqual(FogBuilder.CardsPerSegment, segmentLayers.Length);
                foreach (var layer in segmentLayers)
                {
                    Assert.AreEqual(FogDensity.Ambient(false, false), layer.density);
                    Assert.Less(FogDensity.DistanceToSegment(layer.renderer.transform.position,
                        clearings[i].position, clearings[i + 1].position), 0.001f);
                    Assert.AreEqual(clearings[i].position, layer.pathStart);
                    Assert.AreEqual(clearings[i + 1].position, layer.pathEnd);
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(8)]
        public void CardBudgetUsesSharedSavedMaterialTwoTrianglesAndNoCollidersOrShadows(int count)
        {
            var fog = FogBuilder.BuildLayers(world.transform, Clearings(count));
            var material = GeneratedMaterials.GetGroundFog();
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material));
            Assert.IsTrue(AssetDatabase.Contains(material));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(GeneratedMaterials.GetGroundFog())));
            Assert.AreEqual("Universal Render Pipeline/Tiffany Ground Fog", material.shader.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            Assert.AreEqual(FogBuilder.MaxCards(count), fog.GetComponentsInChildren<Renderer>().Length);
            Assert.IsEmpty(fog.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(fog.GetComponentsInChildren<Light>());
            foreach (var layer in fog.layers)
            {
                Assert.AreSame(material, layer.renderer.sharedMaterial);
                Assert.AreEqual(6, layer.renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length);
                Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(layer.renderer.gameObject, StaticEditorFlags.BatchingStatic));
                Assert.AreEqual(ShadowCastingMode.Off, layer.renderer.shadowCastingMode);
                Assert.IsFalse(layer.renderer.receiveShadows);
                var properties = new MaterialPropertyBlock();
                layer.renderer.GetPropertyBlock(properties);
                Assert.AreEqual(layer.density, properties.GetFloat("_Density"));
                Assert.AreEqual(GroundFog.WindSpeed, properties.GetFloat("_WindSpeed"));
                Assert.AreEqual(FogDensity.PathMultiplier, properties.GetFloat("_PathMultiplier"));
                Assert.IsEmpty(layer.renderer.GetComponents<MonoBehaviour>());
            }
        }

        [Test]
        public void ClosedLoopAlsoCoversTheLastToFirstSegmentWithinItsBudget()
        {
            var clearings = Clearings(8);
            var fog = FogBuilder.BuildLayers(world.transform, clearings, closedLoop: true);
            Assert.AreEqual(24, fog.layers.Length, "Ocho claros por dos tarjetas y ocho tramos por una.");
            Assert.AreEqual(FogBuilder.MaxCards(8, true), fog.layers.Length);
            var closing = System.Array.FindAll(fog.layers, layer => layer.renderer.name.StartsWith("Segment 8 Fog "));
            Assert.AreEqual(FogBuilder.CardsPerSegment, closing.Length);
            foreach (var layer in closing)
            {
                Assert.AreEqual(clearings[7].position, layer.pathStart);
                Assert.AreEqual(clearings[0].position, layer.pathEnd);
                Assert.Less(FogDensity.DistanceToSegment(layer.renderer.transform.position,
                    clearings[7].position, clearings[0].position), 0.001f);
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void FindsWaterAndNestedTreeGroupsAtArbitraryPositions(bool water, bool trees)
        {
            var clearings = Clearings(1);
            var dry = FogBuilder.BuildLayers(world.transform, clearings);
            float baseline = dry.layers[0].density;
            Object.DestroyImmediate(dry.gameObject);
            if (water) Feature("Water", clearings[0], new Vector3(9f, -0.15f, 3f), new Vector3(12f, 0.1f, 22f));
            if (trees)
            {
                var group = new GameObject("Trees").transform;
                group.SetParent(clearings[0], false);
                Feature("Visual", group, new Vector3(-16f, 3f, 4f), new Vector3(2f, 6f, 2f));
            }
            var dense = FogBuilder.BuildLayers(world.transform, clearings);
            Assert.Greater(dense.layers[0].density, baseline);
        }

        [Test]
        public void LongWaterOutsideClearingsHasOverlappingCardsAcrossItsWholeSurface()
        {
            var clearings = Clearings(1);
            var landscape = new GameObject("Landscape").transform;
            landscape.SetParent(world.transform, false);
            var water = Feature("Water", landscape, new Vector3(80f, -0.15f, 30f), new Vector3(4f, 0.1f, 120f));
            water.transform.localRotation = Quaternion.Euler(0f, 57f, 0f);
            var fog = FogBuilder.BuildLayers(world.transform, clearings);
            var layers = System.Array.FindAll(fog.layers, layer => layer.water);
            Assert.AreEqual(4, layers.Length);
            Assert.AreEqual(6, fog.layers.Length);
            for (int sample = 0; sample <= 60; sample++)
            {
                Vector3 point = water.transform.TransformPoint(new Vector3(0f, 0f, sample / 60f - 0.5f));
                Assert.IsTrue(System.Array.Exists(layers, layer =>
                {
                    point.y = layer.renderer.transform.position.y;
                    Vector3 local = layer.renderer.transform.InverseTransformPoint(point);
                    return Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.y) < 0.5f;
                }), point.ToString());
            }
        }

        [Test]
        public void LayersUseGroundSurfaceHeightInsteadOfTheWorldOrigin()
        {
            var clearings = Clearings(1);
            var floor = Feature("Raised Ground", clearings[0], Vector3.up * 2f, new Vector3(60f, 1f, 60f));
            floor.AddComponent<BoxCollider>();
            floor.AddComponent<GroundSurface>();
            var fog = FogBuilder.BuildLayers(world.transform, clearings);
            float ground = clearings[0].position.y + 2.5f;
            foreach (var layer in fog.layers)
            {
                Assert.AreEqual(ground, layer.groundHeight, 0.001f);
                var properties = new MaterialPropertyBlock();
                layer.renderer.GetPropertyBlock(properties);
                Assert.AreEqual(ground, properties.GetFloat("_GroundHeight"), 0.001f);
                Assert.That(layer.renderer.transform.position.y - ground,
                    Is.InRange(FogBuilder.MinHeight - 0.001f, FogBuilder.MaxHeight + 0.001f));
                Assert.AreEqual(0.3f, layer.renderer.bounds.size.y, 0.001f);
                Assert.Greater(layer.renderer.bounds.min.y, ground);
            }
        }

        [TestCase(22f, 0f)]
        [TestCase(4f, 57f)]
        [TestCase(4f, 137f)]
        public void WaterBankFollowsSurfaceHeightAndOrientationAndReplacesLandFog(float length, float yaw)
        {
            var clearings = Clearings(1);
            var water = Feature("Water", clearings[0], new Vector3(9f, -1.8f, 3f), new Vector3(12f, 0.1f, length));
            water.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var surface = water.GetComponent<Renderer>();
            var fog = FogBuilder.BuildLayers(world.transform, clearings);
            Assert.AreEqual(3, fog.layers.Length);
            Assert.AreEqual(FogBuilder.MaxCards(1, waterCount: 1), fog.layers.Length);
            var waterLayers = System.Array.FindAll(fog.layers, layer => layer.water);
            Assert.AreEqual(1, waterLayers.Length);
            var bank = waterLayers[0];
            Assert.AreEqual(surface.bounds.max.y, bank.groundHeight, 0.001f);
            Assert.AreEqual(surface.bounds.max.y + FogBuilder.WaterHeight, bank.renderer.transform.position.y, 0.001f);
            Assert.AreEqual(0.96f, bank.density, 0.0001f);
            Assert.AreEqual(6f, bank.waterFootprint.z, 0.001f);
            Assert.AreEqual(length * 0.5f, bank.waterFootprint.w, 0.001f);
            foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float z in new[] { -0.5f, 0.5f })
                {
                    Vector3 local = bank.renderer.transform.InverseTransformPoint(water.transform.TransformPoint(new Vector3(x, 0f, z)));
                    Assert.Less(Mathf.Abs(local.x), 0.5f);
                    Assert.Less(Mathf.Abs(local.y), 0.5f);
                }
            var properties = new MaterialPropertyBlock();
            foreach (var layer in fog.layers)
            {
                layer.renderer.GetPropertyBlock(properties);
                Assert.AreEqual(bank.waterFootprint, properties.GetVector("_WaterFootprint"));
                Assert.AreEqual(new Vector4(bank.waterAxis.x, bank.waterAxis.y, 0f, 0f), properties.GetVector("_WaterAxis"));
                Assert.AreEqual(layer.water ? 1f : 0f, properties.GetFloat("_WaterLayer"));
                Assert.AreEqual(layer.water ? 1f : FogDensity.PathMultiplier, properties.GetFloat("_PathMultiplier"));
            }
            bank.renderer.GetPropertyBlock(properties);
            Assert.AreEqual(0.2f, properties.GetFloat("_WindSpeed"));
            Assert.Less(GroundFog.WaterWindSpeed, GroundFog.WindSpeed);
        }

        Transform[] Clearings(int count)
        {
            var clearings = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                var clearing = new GameObject($"Test Clearing {i + 1}").transform;
                clearing.SetParent(world.transform, false);
                clearing.position = new Vector3(137f + Mathf.Cos(angle) * 55f, 4f, -219f + Mathf.Sin(angle) * 55f);
                clearing.rotation = Quaternion.Euler(0f, 57f, 0f);
                clearings[i] = clearing;
            }
            return clearings;
        }

        static GameObject Feature(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var feature = GameObject.CreatePrimitive(PrimitiveType.Cube);
            feature.name = name;
            feature.transform.SetParent(parent, false);
            feature.transform.localPosition = position;
            feature.transform.localScale = size;
            Object.DestroyImmediate(feature.GetComponent<Collider>());
            return feature;
        }
    }
}
