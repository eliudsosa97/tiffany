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
        List<float> stepSpeeds;
        Animator animator;
        GameObject wall;
        int steppedCount;

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
            var movement = player.AddComponent<PlayerController>();
            movement.viewCamera = viewCamera;
            steppedCount = 0;
            movement.Stepped += (_, _) => steppedCount++;
#if UNITY_EDITOR
            var model = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Models/tiffany-provisional.fbx"), player.transform, false);
            animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Art/Models/TiffanyProvisional.controller");
            player.AddComponent<TiffanyAnimator>().animator = animator;
#endif

            steps = new List<StepNoise>();
            stepSpeeds = new List<float>();
            NoiseBus.Published += RecordStep;
            GameInput.MoveOverride = Vector2.up;
            GameInput.SlowOverride = false;
            GameInput.RunOverride = false;
            GameInput.AimOverride = Vector2.zero;
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
            if (wall != null) Object.Destroy(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingOnGravelPublishesStepsAndMovesForward()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;

            yield return new WaitForSeconds(2f);

            AssertSteps(7, 9, 8f);
            Assert.That(player.transform.position.z, Is.InRange(3.5f, 4.6f));
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
        public IEnumerator HalfStickOnGravelUsesWalkingStepsAndMovesForward()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;
            GameInput.MoveOverride = new Vector2(0f, 0.5f);

            yield return new WaitForSeconds(2f);

            AssertSteps(7, 9, 8f);
            Assert.That(player.transform.position.z, Is.InRange(3.5f, 4.6f));
        }

        [UnityTest]
        public IEnumerator NearFullStickOnGravelUsesWalkingSteps()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;
            GameInput.MoveOverride = new Vector2(0f, 0.9f);

            yield return new WaitForSeconds(2f);

            AssertSteps(7, 9, 8f);
        }

        [UnityTest]
        public IEnumerator ReleasingStickUsesBrakingSpeedThenStops()
        {
            GameInput.MoveOverride = new Vector2(0f, 0.9f);
            yield return new WaitForSeconds(0.5f);

            var controller = player.GetComponent<PlayerController>();
            Assert.AreEqual(Gait.Walk, controller.CurrentGait);
            GameInput.MoveOverride = Vector2.zero;

            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(Gait.Slow, controller.CurrentGait);
            Assert.Less(controller.HorizontalSpeed, PlayerMotion.SlowSpeed);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(0f, controller.HorizontalSpeed, 0.01f);
        }

        [UnityTest]
        public IEnumerator RunningOnGravelDoublesNoiseRadius()
        {
            ground.AddComponent<GroundSurface>().type = GroundType.Gravel;
            GameInput.RunOverride = true;

            yield return new WaitForSeconds(1.5f);

            AssertSteps(6, 8, 16f);
        }

        [UnityTest]
        public IEnumerator SlowMovementWithoutSurfaceUsesDirtNoise()
        {
            GameInput.SlowOverride = true;

            yield return new WaitForSeconds(2f);

            AssertSteps(5, 6, 1f);
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

            Assert.Greater(steps.Count, 0);
            for (int i = 0; i < steps.Count; i++)
                Assert.AreEqual(NoiseModel.StepRadius(GroundType.Branches, PlayerMotion.NoiseGait(stepSpeeds[i]), false),
                    steps[i].Radius, 1e-4f);
        }

        [UnityTest]
        public IEnumerator MovementWithoutAnimationContactsMakesNoSteps()
        {
            Assert.IsNotNull(animator);
            animator.speed = 0f;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(player.transform.position.z, 0.5f);
            Assert.AreEqual(0, steps.Count);
            animator.speed = 1f;
            yield return new WaitForSeconds(0.08f);
            Assert.AreEqual(0, steps.Count);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(steps.Count, 0);
        }

        [UnityTest]
        public IEnumerator WalkingIntoWallStopsAnimationAndNoise()
        {
            wall = new GameObject("Wall");
            wall.transform.position = new Vector3(0f, 1f, 1.5f);
            wall.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 0.2f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1f);
            var movement = player.GetComponent<PlayerController>();
            Assert.Less(movement.HorizontalSpeed, TiffanyAnimation.MoveSpeed);
            Assert.Less(animator.GetFloat(TiffanyAnimation.SpeedParameter), TiffanyAnimation.MoveSpeed);
            int before = steps.Count;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(before, steps.Count);
        }

        [UnityTest]
        public IEnumerator AirborneContactsDoNotPublishSteps()
        {
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position += Vector3.up * 4f;
            character.enabled = true;
            player.GetComponent<PlayerController>().gravity = 0f;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(0, steps.Count);
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
            stepSpeeds.Add(player.GetComponent<PlayerController>().HorizontalSpeed);
        }

        void AssertSteps(int minimum, int maximum, float radius)
        {
            Assert.That(steps.Count, Is.InRange(minimum, maximum));
            Assert.AreEqual(steps.Count, steppedCount);
            foreach (var step in steps) Assert.AreEqual(radius, step.Radius, 1e-4f);
        }
    }
}
