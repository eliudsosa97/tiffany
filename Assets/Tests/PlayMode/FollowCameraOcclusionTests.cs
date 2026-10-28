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

        [SetUp]
        public void SetUp()
        {
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
    }
}
