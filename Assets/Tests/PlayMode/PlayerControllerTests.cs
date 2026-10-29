using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public class PlayerControllerTests
    {
        GameObject ground;
        GameObject cameraObject;
        GameObject player;
        List<StepNoise> steps;

        [SetUp]
        public void SetUp()
        {
            ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            ground.AddComponent<BoxCollider>();
            cameraObject = new GameObject("Camera");
            cameraObject.transform.position = new Vector3(0f, 20f, -20f);
            cameraObject.transform.LookAt(Vector3.zero);
            var viewCamera = cameraObject.AddComponent<Camera>();
            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0.05f, 0f);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.2f;
            controller.radius = 0.25f;
            controller.center = new Vector3(0f, 0.6f, 0f);
            player.AddComponent<PlayerController>().viewCamera = viewCamera;

            steps = new List<StepNoise>();
            NoiseBus.Published += RecordStep;
            GameInput.MoveOverride = Vector2.up;
            GameInput.SlowOverride = false;
            GameInput.RunOverride = false;
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= RecordStep;
            GameInput.ClearOverrides();
            Object.Destroy(player);
            Object.Destroy(cameraObject);
            Object.Destroy(ground);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingOnGravelPublishesStepsAndMovesForward()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;

            yield return new WaitForSeconds(2f);

            AssertSteps(5, 7, 8f);
            Assert.That(player.transform.position.z, Is.InRange(3.5f, 4.6f));
        }

        [UnityTest]
        public IEnumerator FootstepsUseTheHighestSurfaceWhenMoreThanFourCollidersOverlap()
        {
            for (int i = 12; i >= 1; i--)
            {
                var layer = new GameObject($"Ground Layer {i}");
                layer.transform.SetParent(ground.transform, true);
                layer.transform.position = Vector3.up * (i * 0.02f - 0.005f);
                layer.AddComponent<BoxCollider>().size = new Vector3(60f, 0.01f, 60f);
                layer.AddComponent<GroundSurface>().type = i == 12 ? GroundType.Gravel : GroundType.Dirt;
            }
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = Vector3.up * 0.24f;
            body.enabled = true;
            Physics.SyncTransforms();
            Assert.Greater(Physics.RaycastAll(player.transform.position + Vector3.up * 0.5f, Vector3.down,
                1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Length, 4);

            yield return new WaitForSeconds(2f);

            AssertSteps(5, 7, NoiseModel.GravelRadius);
        }

        [UnityTest]
        public IEnumerator WalkingBackwardsKeepsAimAndRunningFacesMovement()
        {
            cameraObject.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
            float yaw = cameraObject.transform.eulerAngles.y;
            var aimDirection = PlayerMotion.CameraRelative(Vector2.up, yaw);
            var moveDirection = PlayerMotion.CameraRelative(Vector2.down, yaw);
            var body = player.GetComponent<CharacterController>();
            GameInput.AimOverride = Vector2.up;
            GameInput.MoveOverride = Vector2.down;

            yield return new WaitForSeconds(2f);

            Assert.That(Vector3.Dot(player.transform.forward, aimDirection), Is.GreaterThan(0.95f));
            Assert.That(Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude,
                Is.EqualTo(0.7f * 2.2f).Within(0.15f));
            GameInput.RunOverride = true;

            yield return new WaitForSeconds(1f);

            Assert.That(Vector3.Dot(player.transform.forward, moveDirection), Is.GreaterThan(0.95f));
            Assert.That(Vector3.ProjectOnPlane(body.velocity, Vector3.up).magnitude,
                Is.EqualTo(4f).Within(0.15f));
        }

        [UnityTest]
        public IEnumerator HalfStickOnGravelUsesSlowStepsAndMovesForward()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;
            GameInput.MoveOverride = new Vector2(0f, 0.5f);

            yield return new WaitForSeconds(2f);

            AssertSteps(3, 5, 4f);
            Assert.That(player.transform.position.z, Is.InRange(1.7f, 2.4f));
        }

        [UnityTest]
        public IEnumerator NearFullStickOnGravelUsesWalkingSteps()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;
            GameInput.MoveOverride = new Vector2(0f, 0.9f);

            yield return new WaitForSeconds(2f);

            AssertSteps(5, 7, 8f);
        }

        [UnityTest]
        public IEnumerator ReleasingStickPreservesLastGait()
        {
            GameInput.MoveOverride = new Vector2(0f, 0.9f);
            yield return null;

            var controller = player.GetComponent<PlayerController>();
            Assert.AreEqual(Gait.Walk, controller.CurrentGait);
            GameInput.MoveOverride = Vector2.zero;

            yield return null;

            Assert.AreEqual(Gait.Walk, controller.CurrentGait);
        }

        [UnityTest]
        public IEnumerator RunningOnGravelDoublesNoiseRadius()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;
            GameInput.RunOverride = true;

            yield return new WaitForSeconds(1.5f);

            AssertSteps(4, 6, 16f);
        }

        [UnityTest]
        public IEnumerator SlowMovementWithoutSurfaceUsesDirtNoise()
        {
            GameInput.SlowOverride = true;

            yield return new WaitForSeconds(2f);

            AssertSteps(3, 5, 1f);
        }

        [UnityTest]
        public IEnumerator StandingStillDoesNotPublishSteps()
        {
            GameInput.MoveOverride = Vector2.zero;

            yield return new WaitForSeconds(1f);

            Assert.AreEqual(0, steps.Count);
        }

        [UnityTest]
        public IEnumerator TappingForwardOnBranchesStillMakesNoise()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Branches;

            for (int tap = 0; tap < 10; tap++)
            {
                GameInput.MoveOverride = Vector2.up;
                yield return new WaitForSeconds(0.12f);
                GameInput.MoveOverride = Vector2.zero;
                yield return new WaitForSeconds(0.15f);
            }

            AssertSteps(1, int.MaxValue, 20f);
        }

        [UnityTest]
        public IEnumerator TeleportDoesNotCountAsSteps()
        {
            GameInput.MoveOverride = Vector2.zero;
            yield return null;

            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position += Vector3.right * 10f;
            controller.enabled = true;
            Physics.SyncTransforms();

            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(0, steps.Count);
        }

        void RecordStep(StepNoise noise)
        {
            steps.Add(noise);
        }

        void AssertSteps(int minimum, int maximum, float radius)
        {
            Assert.That(steps.Count, Is.InRange(minimum, maximum));
            foreach (var step in steps) Assert.AreEqual(radius, step.Radius, 1e-4f);
        }
    }
}
