using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class EndingCameraTests
    {
        [TestCase(0f)]
        [TestCase(FollowCamera.DefaultYaw)]
        [TestCase(225f)]
        public void ClosePoseFramesMidpointAtSevenMetersAndThirtyDegreesWithGameYaw(float yaw)
        {
            var tiffany = new Vector3(2f, 1f, 4f);
            var family = new Vector3(6f, 3f, 8f);
            var focus = new Vector3(4f, 2.7f, 6f);
            EndingCamera.ClosePose(tiffany, family, yaw, EndingCamera.DefaultDistance, EndingCamera.DefaultPitch,
                out var position, out var rotation);
            Assert.AreEqual(7f, Vector3.Distance(position, focus), 0.001f);
            Assert.Less(Vector3.Distance(focus, position + rotation * Vector3.forward * 7f), 0.001f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(yaw, rotation.eulerAngles.y), 0.001f);
            Assert.AreEqual(30f, rotation.eulerAngles.x, 0.001f);
            Assert.AreEqual(3.5f, position.y - focus.y, 0.001f);
        }

        [Test]
        public void ClosePoseHonorsCustomDistanceAndPitch()
        {
            EndingCamera.ClosePose(Vector3.zero, Vector3.zero, 90f, 6f, 45f,
                out var position, out var rotation);
            Assert.AreEqual(6f, Vector3.Distance(position, Vector3.up * 0.7f), 0.001f);
            Assert.AreEqual(45f, rotation.eulerAngles.x, 0.001f);
        }

        [TestCase(-1f, 0f, 42f)]
        [TestCase(0f, 0f, 42f)]
        [TestCase(0.25f, 0.15625f, 40.125f)]
        [TestCase(0.5f, 0.5f, 36f)]
        [TestCase(1f, 1f, 30f)]
        [TestCase(2f, 1f, 30f)]
        public void BlendClampsProgressAndInterpolatesPositionAndRotation(float progress, float fraction, float pitch)
        {
            EndingCamera.Blend(Vector3.zero, Quaternion.Euler(42f, 45f, 0f), Vector3.right * 10f,
                Quaternion.Euler(30f, 45f, 0f), progress, out var position, out var rotation);
            Assert.Less(Vector3.Distance(Vector3.right * (10f * fraction), position), 0.001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(pitch, 45f, 0f), rotation), 0.05f);
        }

        [TestCase(0f, 0.001f)]
        [TestCase(1f, 0.999f)]
        public void BlendHasZeroVelocityAtBothEnds(float end, float near)
        {
            var target = Quaternion.Euler(0f, 90f, 0f);
            EndingCamera.Blend(Vector3.zero, Quaternion.identity, Vector3.right, target, end,
                out var position, out var rotation);
            EndingCamera.Blend(Vector3.zero, Quaternion.identity, Vector3.right, target, near,
                out var nextPosition, out var nextRotation);
            float step = Mathf.Abs(near - end);
            Assert.Less(Vector3.Distance(position, nextPosition) / step, 0.01f);
            var difference = new Vector4(rotation.x - nextRotation.x, rotation.y - nextRotation.y,
                rotation.z - nextRotation.z, rotation.w - nextRotation.w);
            Assert.Less(difference.magnitude / step, 0.01f);
        }
    }
}
