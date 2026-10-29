using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class FollowCameraOcclusionTests
    {
        GameObject player;
        GameObject cameraObject;
        FollowCamera follow;
        float previousDisabled;
        float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousDisabled = Shader.GetGlobalFloat("_TiffanyOcclusionDisabled");
            previousTimeScale = Time.timeScale;
            Shader.SetGlobalFloat("_TiffanyOcclusionDisabled", 0f);
            player = new GameObject("Tiffany");
            cameraObject = new GameObject("Camera");
            follow = cameraObject.AddComponent<FollowCamera>();
            follow.target = player.transform;
            follow.lookAhead = 8f;
            follow.Snap();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(cameraObject);
            Object.Destroy(player);
            yield return null;
            Shader.SetGlobalFloat("_TiffanyOcclusionDisabled", previousDisabled);
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator PublishesTheMovingChestInsteadOfTheSmoothedLookAhead()
        {
            player.transform.position = new Vector3(9f, 3f, -7f);
            yield return null;
            yield return null;
            var focus = Shader.GetGlobalVector("_TiffanyFocusWS");
            Assert.Less(Vector3.Distance(player.transform.position + Vector3.up * FollowCamera.ChestHeight, focus), 1e-4f);
            Assert.AreEqual(FollowCamera.OcclusionRadius, focus.w);
            Assert.Less(Vector3.Distance(cameraObject.transform.position, Shader.GetGlobalVector("_TiffanyCameraWS")), 1e-4f);
            Assert.AreEqual(new Vector4(FollowCamera.OcclusionSoftness, FollowCamera.OcclusionFocusMargin,
                FollowCamera.OcclusionMinimumKeep, FollowCamera.OcclusionNearMargin), Shader.GetGlobalVector("_TiffanyOcclusionParams"));
        }

        [UnityTest]
        public IEnumerator LosingTheTargetClearsThePreviousCutout()
        {
            follow.target = null;
            yield return null;
            yield return null;
            Assert.AreEqual(Vector4.zero, Shader.GetGlobalVector("_TiffanyFocusWS"));
        }

        [Test]
        public void FixedCaptureCameraPublishesItsActualPose()
        {
            follow.enabled = false;
            cameraObject.transform.position = new Vector3(3f, 6f, -8f);
            player.transform.position = new Vector3(2f, 1f, 4f);
            follow.PublishOcclusionFocus();
            Assert.Less(Vector3.Distance(cameraObject.transform.position, Shader.GetGlobalVector("_TiffanyCameraWS")), 1e-4f);
            Assert.Less(Vector3.Distance(player.transform.position + Vector3.up * FollowCamera.ChestHeight,
                Shader.GetGlobalVector("_TiffanyFocusWS")), 1e-4f);
        }

        [UnityTest]
        public IEnumerator CaptureOverrideSurvivesFocusPublishingAndRestoresOnClearOrDisable()
        {
            var stage = cameraObject.AddComponent<OcclusionCaptureStage>();
            Assert.AreEqual(0f, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"));
            Shader.SetGlobalFloat("_TiffanyOcclusionDisabled", 0.25f);
            Time.timeScale = 0.75f;
            follow.enabled = false;
            stage.Apply();
            stage.Apply();
            follow.PublishOcclusionFocus();
            yield return null;
            Assert.AreEqual(1f, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"));
            Assert.AreEqual(FollowCamera.OcclusionRadius, Shader.GetGlobalVector("_TiffanyFocusWS").w);
            Assert.AreEqual(0f, Time.timeScale);
            stage.Clear();
            stage.Clear();
            Assert.AreEqual(0.25f, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"));
            Assert.AreEqual(0.75f, Time.timeScale);
            stage.after = true;
            stage.Apply();
            Assert.AreEqual(0f, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"));
            stage.enabled = false;
            Assert.AreEqual(0.25f, Shader.GetGlobalFloat("_TiffanyOcclusionDisabled"));
            Assert.AreEqual(0.75f, Time.timeScale);
        }
    }
}
