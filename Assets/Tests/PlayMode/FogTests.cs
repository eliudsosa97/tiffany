using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class FogTests : TiffanyLogTests
    {
        GroundFog fog;
        WitchChapterTracker tracker;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.RunOverride = GameInput.SlowOverride = GameInput.InteractOverride = GameInput.LightHeldOverride = false;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<GameInterface>().StartGame();
            fog = Object.FindAnyObjectByType<GroundFog>();
            tracker = Object.FindAnyObjectByType<WitchChapterTracker>();
            Assert.IsNotNull(fog, "Regenera Main para incluir World/Ground Fog.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.CreateScene(nameof(FogTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        [UnityTest]
        public IEnumerator SceneLoadsSerializedLayersWithSharedMaterialAndInstanceDensities()
        {
            Assert.AreEqual("World/" + GroundFog.RootName, fog.transform.parent.name + "/" + fog.name);
            Assert.IsFalse(fog.transform.IsChildOf(tracker.transform));
            Assert.AreSame(tracker.player, fog.player);
            Assert.AreEqual(100, fog.layers.Length, "16 de claros, 54 del sendero, 8 del bosque embrujado y 22 de agua.");
            Assert.AreEqual(22, System.Array.FindAll(fog.layers, layer => layer.water).Length);
            Assert.AreEqual(fog.layers.Length, fog.GetComponentsInChildren<Renderer>().Length);
            var material = fog.layers[0].renderer.sharedMaterial;
            Assert.AreEqual("Universal Render Pipeline/Tiffany Ground Fog", material.shader.name);
            var properties = new MaterialPropertyBlock();
            foreach (var layer in fog.layers)
            {
                Assert.IsTrue(layer.renderer.transform.IsChildOf(fog.transform));
                Assert.AreSame(material, layer.renderer.sharedMaterial);
                Assert.AreEqual(ShadowCastingMode.Off, layer.renderer.shadowCastingMode);
                Assert.IsFalse(layer.renderer.receiveShadows);
                Assert.That(layer.density, Is.InRange(0.01f, 1f));
                layer.renderer.GetPropertyBlock(properties);
                Assert.AreEqual(layer.density, properties.GetFloat("_Density"));
                Assert.AreEqual((Vector4)layer.pathCorner, properties.GetVector("_PathCorner"));
            }
            Assert.IsEmpty(fog.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(fog.GetComponentsInChildren<Light>());
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(FogMode.Linear, RenderSettings.fogMode);
            Assert.Greater(RenderSettings.fogStartDistance, Camera.main.GetComponent<FollowCamera>().distance);
            Assert.Less(RenderSettings.fogEndDistance, Camera.main.farClipPlane);
            Assert.Greater(RenderSettings.fogColor.b, RenderSettings.fogColor.r);
            yield return null;
            Vector4 position = Shader.GetGlobalVector("_GroundFogPlayer");
            Assert.Less(Vector3.Distance(tracker.player.position, position), 0.01f);
            Assert.AreEqual(1f, position.w);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CaptureStagesRestoreFogAndPreserveDisabledLayersForEveryInitialState()
        {
            var stages = Object.FindObjectsByType<FogCaptureStage>(FindObjectsSortMode.None);
            Assert.AreEqual(6, stages.Length);
            Color color = RenderSettings.fogColor;
            FogMode mode = RenderSettings.fogMode;
            float start = RenderSettings.fogStartDistance;
            float end = RenderSettings.fogEndDistance;
            var renderer = fog.layers[0].renderer;
            var material = renderer.sharedMaterial;
            renderer.enabled = false;
            foreach (var stage in stages)
                foreach (bool active in new[] { false, true })
                    foreach (bool distant in new[] { false, true })
                    {
                        fog.gameObject.SetActive(active);
                        RenderSettings.fog = distant;
                        Assert.IsTrue(stage.Apply());
                        Assert.IsTrue(stage.Apply());
                        Assert.AreEqual(stage.fogEnabled, fog.gameObject.activeSelf);
                        Assert.AreEqual(stage.fogEnabled, RenderSettings.fog);
                        Assert.IsFalse(renderer.enabled);
                        Assert.AreSame(material, renderer.sharedMaterial);
                        stage.Clear();
                        stage.Clear();
                        Assert.AreEqual(active, fog.gameObject.activeSelf);
                        Assert.AreEqual(distant, RenderSettings.fog);
                        Assert.IsTrue(stage.Apply());
                        stage.enabled = false;
                        Assert.AreEqual(active, fog.gameObject.activeSelf);
                        Assert.AreEqual(distant, RenderSettings.fog);
                        stage.enabled = true;
                    }
            Assert.AreEqual(color, RenderSettings.fogColor);
            Assert.AreEqual(mode, RenderSettings.fogMode);
            Assert.AreEqual(start, RenderSettings.fogStartDistance);
            Assert.AreEqual(end, RenderSettings.fogEndDistance);
            renderer.enabled = true;
            fog.gameObject.SetActive(true);
            RenderSettings.fog = true;
            yield return null;
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(fog.layers[0].density, properties.GetFloat("_Density"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DemoWalksFromTheLastClearingWithCameraRelativeInputAndCleansUp()
        {
            fog.gameObject.SetActive(false);
            RenderSettings.fog = false;
            Vector3 originalPosition = tracker.player.position;
            var demo = new GameObject("Fog Demo Test").AddComponent<FogDemo>();
            demo.clearingNumber = tracker.clearings.Length;
            try
            {
                yield return null;
                yield return null;
                yield return null;
                Assert.IsTrue(fog.gameObject.activeSelf);
                Assert.IsTrue(RenderSettings.fog);
                Assert.IsTrue(tracker.player.GetComponent<PlayerLight>().State.IsOn);
                Assert.IsTrue(tracker.player.GetComponent<PlayerLight>().spot.enabled);
                Assert.IsTrue(GameInput.SlowOverride.Value);
                Assert.IsFalse(GameInput.RunOverride.Value);
                Vector3 before = tracker.player.position;
                Vector3 center = tracker.clearings[tracker.clearings.Length - 1].position;
                Assert.Less(Vector3.Distance(center, before), 18f);
                Vector3 direction = (tracker.clearings[tracker.clearings.Length - 2].position - center).normalized;
                yield return new WaitForSeconds(1.5f);
                Vector3 movement = tracker.player.position - before;
                movement.y = 0f;
                Assert.Greater(Vector3.Dot(movement, direction), 0.15f);
                Assert.Less(Vector3.Cross(movement, direction).magnitude, 0.1f);
                demo.enabled = false;
                Assert.IsFalse(fog.gameObject.activeSelf);
                Assert.IsFalse(RenderSettings.fog);
                Assert.Less(Vector3.Distance(originalPosition, tracker.player.position), 0.05f);
                Assert.AreEqual(Vector2.zero, GameInput.MoveOverride.Value);
                Assert.AreEqual(Vector2.zero, GameInput.AimOverride.Value);
                Assert.IsFalse(GameInput.SlowOverride.Value);
                Assert.IsFalse(GameInput.RunOverride.Value);
                Assert.IsFalse(tracker.player.GetComponent<PlayerLight>().State.IsOn);
            }
            finally
            {
                Object.Destroy(demo.gameObject);
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
