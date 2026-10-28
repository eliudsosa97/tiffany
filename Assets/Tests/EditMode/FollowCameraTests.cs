using NUnit.Framework;
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
        public void SnapWithoutTargetDoesNotThrow()
        {
            Assert.DoesNotThrow(() => followCamera.Snap());
        }
    }
}
