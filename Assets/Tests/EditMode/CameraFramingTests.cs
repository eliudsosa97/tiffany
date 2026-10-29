using NUnit.Framework;
using TiffanyGame.EditorTools;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class CameraFramingTests
    {
        GameObject cameraObject;
        FollowCamera followCamera;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Camera");
            followCamera = cameraObject.AddComponent<FollowCamera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void TopEdgeAboveFocusIsBetweenNineAndTenMeters()
        {
            Assert.That(VisibleHeight(0f), Is.InRange(9f, 10f));
        }

        [Test]
        public void TopEdgeFallsBeyondFocus()
        {
            Assert.Less(VisibleHeight(2f), VisibleHeight(0f));
        }

        [Test]
        public void SkyDecoyIsHighAboveFocusAndInsideUpperHalfOfFrame()
        {
            Vector2 offset = CameraFraming.DecoyOffset(followCamera.distance, followCamera.pitch, followCamera.fieldOfView);
            Quaternion rotation = Quaternion.Euler(followCamera.pitch, 0f, 0f);
            Vector3 ray = rotation * Vector3.forward * followCamera.distance + new Vector3(0f, offset.y, offset.x);
            Vector3 local = Quaternion.Inverse(rotation) * ray;
            float angle = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            Assert.GreaterOrEqual(offset.y, 4f);
            Assert.That(angle, Is.GreaterThan(0f).And.LessThan(followCamera.fieldOfView * 0.5f));
        }

        [Test]
        public void TopEdgeRisesWithPitch()
        {
            float original = VisibleHeight(0f);
            followCamera.pitch += 10f;
            Assert.Greater(VisibleHeight(0f), original);
        }

        [TestCase(-2f, 7.928203f)]
        [TestCase(0f, 6.773503f)]
        [TestCase(2f, 5.618802f)]
        public void TopEdgeMatchesExactGeometry(float offset, float expected)
        {
            Assert.AreEqual(expected, CameraFraming.MaxVisibleHeight(10f, 60f, 60f, 1f, offset), 1e-4f);
        }

        const float FireBallRadius = 0.45f;

        [Test]
        public void WholeFireBallShowsRightAboveTiffanyWhenSheFacesAway()
        {
            AssertFireBallInFrame(-followCamera.lookAhead);
        }

        [Test]
        public void WholeFireBallShowsWithinThreeAndAHalfMetersWhenSheFacesTheCamera()
        {
            AssertFireBallInFrame(followCamera.lookAhead - 3.5f);
        }

        void AssertFireBallInFrame(float offsetAlongView)
        {
            float top = RouteBuilder.SkyHeight + SceneBuilder.FireBallHeight + FireBallRadius;
            Assert.LessOrEqual(top, VisibleHeight(offsetAlongView));
        }

        float VisibleHeight(float offsetAlongView) => CameraFraming.MaxVisibleHeight(followCamera.distance,
            followCamera.pitch, followCamera.fieldOfView, followCamera.focusHeight, offsetAlongView);
    }
}
