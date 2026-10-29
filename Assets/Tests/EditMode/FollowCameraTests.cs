using System.Reflection;
using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public class FollowCameraTests
    {
        GameObject target;
        GameObject cameraObject;
        FollowCamera followCamera;

        [SetUp]
        public void SetUp()
        {
            target = new GameObject("Target");
            target.transform.position = new Vector3(1f, 0f, 2f);
            cameraObject = new GameObject("Camera");
            cameraObject.AddComponent<Camera>();
            followCamera = cameraObject.AddComponent<FollowCamera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
        }

        [TestCase(0f, 0f, 1f, 4.5f)]
        [TestCase(0f, 90f, 3.5f, 2f)]
        [TestCase(-90f, 0f, 1f, 2f)]
        public void SnapAppliesPoseAndHorizontalLookAhead(float targetPitch, float targetYaw, float focusX, float focusZ)
        {
            target.transform.rotation = Quaternion.Euler(targetPitch, targetYaw, 0f);
            followCamera.target = target.transform;

            followCamera.Snap();

            Assert.Less(Quaternion.Angle(Quaternion.Euler(42f, 45f, 0f), cameraObject.transform.rotation), 0.01f);
            var focus = cameraObject.transform.position + cameraObject.transform.forward * followCamera.distance;
            Assert.AreEqual(focusX, focus.x, 1e-3f);
            Assert.AreEqual(0.8f, focus.y, 1e-3f);
            Assert.AreEqual(focusZ, focus.z, 1e-3f);
        }

        [Test]
        public void BuiltCameraStaysCenteredWhenTargetTurnsAndUsesGameNightView()
        {
            Object.DestroyImmediate(cameraObject);
            var player = target.AddComponent<PlayerController>();
            var build = typeof(SceneBuilder).GetMethod("CreateCamera", BindingFlags.Static | BindingFlags.NonPublic);
            var camera = (Camera)build.Invoke(null, new object[] { player });
            cameraObject = camera.gameObject;
            followCamera = camera.GetComponent<FollowCamera>();
            Assert.AreEqual(0f, followCamera.lookAhead);
            Assert.AreEqual(NightView.CameraDistance, followCamera.distance);
            Assert.AreEqual(42f, followCamera.pitch);
            Assert.AreEqual(24f, camera.fieldOfView);
            Assert.AreEqual(NightView.FarClipPlane, camera.farClipPlane);
            Assert.AreEqual(CameraClearFlags.SolidColor, camera.clearFlags);
            Assert.AreEqual(RenderSettings.fogColor, camera.backgroundColor);
            var position = camera.transform.position;
            var rotation = camera.transform.rotation;
            foreach (float yaw in new[] { 90f, 180f, 270f, 0f })
            {
                target.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                followCamera.Snap();
                Assert.Less(Vector3.Distance(position, camera.transform.position), 0.001f);
                Assert.Less(Quaternion.Angle(rotation, camera.transform.rotation), 0.001f);
                var focus = camera.transform.position + camera.transform.forward * followCamera.distance;
                Assert.Less(Vector3.Distance(target.transform.position + Vector3.up * followCamera.focusHeight, focus), 0.001f);
            }
        }

        [Test]
        public void SnapWithoutTargetDoesNotThrow()
        {
            Assert.DoesNotThrow(() => followCamera.Snap());
        }
    }
}
