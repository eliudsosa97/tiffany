using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using LogAssert = TiffanyGame.Tests.TiffanyLogs;

namespace TiffanyGame.Tests
{
    public sealed class WitchControllerTests : TiffanyLogTests
    {
        private GameObject world;
        private GameObject ground;
        private NavMeshSurface surface;
        private GameObject player;
        private GameObject witch;
        private NavMeshAgent agent;
        private WitchController controller;
        private Light fireLight;
        internal WitchController Controller => controller;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            world = new GameObject("WitchTestWorld");
            ground = new GameObject("Ground");
            ground.transform.SetParent(world.transform);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            ground.AddComponent<BoxCollider>();
            surface = world.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();

            player = new GameObject("Player") { tag = "Player" };
            player.transform.SetParent(world.transform);
            player.transform.position = new Vector3(-30f, 0f, -30f);
            var capsule = new GameObject("PlayerBody");
            capsule.transform.SetParent(player.transform, false);
            var collider = capsule.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.4f;
            collider.center = Vector3.up;

            CreateWitch();
        }

        private void CreateWitch()
        {
            witch = new GameObject("Witch");
            witch.SetActive(false);
            witch.transform.SetParent(world.transform);
            witch.transform.position = new Vector3(0f, 14f, 0f);
            agent = witch.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            controller = witch.AddComponent<WitchController>();
            controller.player = player.transform;
            controller.skyRoute = new[] { new Vector3(30f, 14f, 0f), new Vector3(30f, 14f, 30f) };
#if UNITY_EDITOR
            var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/witch.fbx");
            controller.figure = Object.Instantiate(model, witch.transform, false);
            controller.figure.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            controller.figure.transform.localScale = Vector3.one;
            var animator = controller.figure.GetComponent<Animator>();
            if (animator == null) animator = controller.figure.AddComponent<Animator>();
            animator.runtimeAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Art/Models/Witch.controller");
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
#endif
            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(witch.transform, false);
            fireLight = lightObject.AddComponent<Light>();
            fireLight.enabled = false;
            controller.fireLight = fireLight;
            witch.SetActive(true);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeZones.Clear();
            if (surface != null)
            {
                surface.RemoveData();
                Object.Destroy(surface.navMeshData);
            }
            Object.Destroy(world);
            yield return null;
        }

#if UNITY_EDITOR
        [TestCase(WitchState.Stalking)]
        [TestCase(WitchState.Suspicious)]
        [TestCase(WitchState.WindUp)]
        [TestCase(WitchState.Sprinting)]
        [TestCase(WitchState.Recovering)]
        [TestCase(WitchState.Stunned)]
        [TestCase(WitchState.Caught)]
        public void ShowPoseSelectsClipWithoutTransformingFigure(WitchState state)
        {
            controller.enabled = false;
            controller.ShowPose(WitchState.Flying, true);
            Assert.IsFalse(controller.figure.activeSelf);
            controller.ShowPose(state, false);
            var animator = controller.figure.GetComponent<Animator>();
            animator.Update(0f);

            string expectedState = state == WitchState.Suspicious ? nameof(WitchState.Stalking) : state.ToString();
            Assert.AreEqual(Animator.StringToHash(expectedState), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
            if (state == WitchState.Suspicious) Assert.AreEqual(0f, animator.GetFloat("Speed"));
            Assert.IsTrue(controller.figure.activeSelf);
            Assert.AreEqual(Vector3.zero, controller.figure.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, controller.figure.transform.localRotation);
            Assert.AreEqual(Vector3.one, controller.figure.transform.localScale);
        }

        [UnityTest]
        public IEnumerator CaptureAdvancesSprintClipWithControllerDisabled()
        {
            var stage = world.AddComponent<WitchCaptureStage>();
            stage.state = WitchState.Sprinting;
            stage.witchOffset = Vector3.forward * 3f;
            stage.lightWitch = false;
            stage.Apply(player.transform);
            var animator = controller.figure.GetComponent<Animator>();
            animator.Update(0f);
            float start = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

            yield return new WaitForSeconds(0.2f);

            Assert.IsFalse(controller.enabled);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Sprinting"));
            Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, start);
        }

        [UnityTest]
        public IEnumerator MovingStalkingFinishesWindUpBlendWithinThreeTenthsOfASecond()
        {
            controller.raining = true;
            yield return WaitForStalking();
            LightPlayerAt(witch.transform.position + Vector3.right * 14f);
            var animator = controller.figure.GetComponent<Animator>();
            int speedHash = Animator.StringToHash("Speed");

            float deadline = Time.time + 2f;
            while (Time.time < deadline && (animator.IsInTransition(0)
                || !animator.GetCurrentAnimatorStateInfo(0).IsName("Stalking")
                || animator.GetCurrentAnimatorStateInfo(0).length >= 2.5f
                || animator.GetFloat(speedHash) < 1f)) yield return null;

            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.IsFalse(animator.IsInTransition(0));
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Stalking"));
            Assert.Less(animator.GetCurrentAnimatorStateInfo(0).length, 2.5f);
            Assert.GreaterOrEqual(animator.GetFloat(speedHash), 1f);
            Assert.Greater(agent.velocity.sqrMagnitude, 1f);
            LightPlayerAt(witch.transform.position + Vector3.right * 8f);
            yield return WaitForState(WitchState.WindUp, 1f);

            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(WitchState.WindUp, controller.Decision.State);
            Assert.AreEqual(0f, animator.GetFloat(speedHash), 0.01f);
            Assert.IsFalse(animator.IsInTransition(0));
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("WindUp"));
        }

        [UnityTest]
        public IEnumerator SprintingAnimatesAtMeasuredSpeedWithoutRestartingClip()
        {
            witch.transform.position = Vector3.zero;
            LightPlayerAt(Vector3.right * 8f);
            yield return WaitForStalking();
            yield return WaitForState(WitchState.Sprinting, 2f);
            LightPlayerAt(Vector3.back * 25f);
            yield return new WaitForSeconds(0.15f);
            var animator = controller.figure.GetComponent<Animator>();
            int speedHash = Animator.StringToHash("Speed");
            float start = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

            for (int i = 0; i < 3; i++)
            {
                Vector3 previous = witch.transform.position;
                yield return null;
                Vector3 displacement = witch.transform.position - previous;
                displacement.y = 0f;
                Assert.AreEqual(WitchState.Sprinting, controller.Decision.State);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Sprinting"));
                Assert.Greater(animator.GetFloat(speedHash), 5f);
                Assert.AreEqual(displacement.magnitude / Time.deltaTime, animator.GetFloat(speedHash), 0.01f);
            }
            Assert.Greater(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, start);
        }
#endif

        [TestCase(WitchState.Flying, 1f)]
        [TestCase(WitchState.Flying, 5f)]
        [TestCase(WitchState.Flying, 14f)]
        [TestCase(WitchState.TakingOff, 5f)]
        public void ShowPoseKeepsGroundIlluminationAtCruiseLevel(WitchState state, float height)
        {
            controller.enabled = false;
            float baseIntensity = fireLight.intensity;
            witch.transform.position = Vector3.up * (height - 1.5f);
            var ball = new GameObject("FireBall");
            ball.transform.SetParent(witch.transform, false);
            ball.transform.localPosition = Vector3.up * 1.5f;
            controller.fireBall = ball;
            fireLight.transform.SetParent(ball.transform, false);

            controller.ShowPose(state, true);

            Assert.IsTrue(fireLight.enabled);
            Assert.AreEqual(baseIntensity / (14f * 14f), fireLight.intensity / (height * height), 1e-5f);
        }

        [Test]
        public void ShowPoseUsesGroundMaskAndIgnoresTriggers()
        {
            controller.enabled = false;
            float baseIntensity = fireLight.intensity;
            witch.transform.position = Vector3.up * 5f;
            var canopy = AddBox("Canopy", Vector3.up * 3f, Vector3.one);
            canopy.gameObject.layer = 8;
            var trigger = AddBox("Trigger", Vector3.up * 2f, Vector3.one);
            trigger.isTrigger = true;
            controller.groundMask = 1 << ground.layer;
            Physics.SyncTransforms();

            controller.ShowPose(WitchState.Flying, true);

            Assert.AreEqual(baseIntensity * 25f / 196f, fireLight.intensity, 1e-5f);
        }

        [Test]
        public void ShowPoseMeasuresFromLightAndIgnoresCharacterColliders()
        {
            controller.enabled = false;
            float baseIntensity = fireLight.intensity;
            witch.transform.position = Vector3.up * 5f;
            fireLight.transform.localPosition = Vector3.up * 1.5f;
            var ball = new GameObject("FireBall");
            ball.transform.SetParent(witch.transform, false);
            controller.fireBall = ball;
            var body = AddBox("WitchBody", Vector3.up * 4f, Vector3.one);
            body.transform.SetParent(witch.transform, true);
            player.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            controller.ShowPose(WitchState.Flying, true);

            Assert.AreEqual(WitchPoses.FireIntensity(baseIntensity, 6.5f, controller.fireFullHeight),
                fireLight.intensity, 1e-5f);
        }

        [Test]
        public void ShowPoseRestoresBaseIntensityWithoutGround()
        {
            controller.enabled = false;
            float baseIntensity = fireLight.intensity;
            witch.transform.position = Vector3.up * 5f;
            controller.ShowPose(WitchState.Flying, true);
            Assert.Less(fireLight.intensity, baseIntensity);
            ground.GetComponent<BoxCollider>().enabled = false;
            Physics.SyncTransforms();

            controller.ShowPose(WitchState.Flying, true);

            Assert.AreEqual(baseIntensity, fireLight.intensity, 1e-5f);
        }

        [UnityTest]
        public IEnumerator FlyingFollowsSkyRouteWithFireAndWithoutAgent()
        {
            float deadline = Time.time + 1f;
            while (Time.time < deadline) yield return null;

            Assert.That(witch.transform.position.x, Is.InRange(4f, 7f));
            Assert.AreEqual(14f, witch.transform.position.y, 1e-4f);
            Assert.AreEqual(WitchState.Flying, controller.Decision.State);
            Assert.IsFalse(agent.enabled);
            Assert.IsTrue(fireLight.enabled);
        }

        [UnityTest]
        public IEnumerator TakingOffKeepsFireDimNearGround()
        {
            float baseIntensity = fireLight.intensity;
            fireLight.transform.localPosition = Vector3.up * 1.5f;
            controller.raining = true;
            yield return WaitForStalking();
            controller.raining = false;
            controller.playerVanished = true;
            yield return WaitForState(WitchState.TakingOff, 10f);

            float groundHeight = ground.GetComponent<BoxCollider>().bounds.max.y;
            int nearGroundFrames = 0;
            while (controller.Decision.State == WitchState.TakingOff)
            {
                float height = fireLight.transform.position.y - groundHeight;
                if (height >= 4f) break;
                Assert.IsTrue(fireLight.enabled);
                Assert.Less(fireLight.intensity, baseIntensity * 0.1f);
                Assert.AreEqual(WitchPoses.FireIntensity(baseIntensity, height, controller.fireFullHeight),
                    fireLight.intensity, baseIntensity * 1e-5f);
                nearGroundFrames++;
                yield return null;
            }

            Assert.Greater(nearGroundFrames, 1);
        }

        [UnityTest]
        public IEnumerator FlyingUsesLightHeightAboveFlatGround()
        {
            float baseIntensity = fireLight.intensity;
            fireLight.transform.localPosition = Vector3.up * 1.5f;
            controller.fireFullHeight = 18f;
            float groundHeight = ground.GetComponent<BoxCollider>().bounds.max.y;

            for (int i = 0; i < 5; i++)
            {
                yield return null;
                Assert.AreEqual(WitchState.Flying, controller.Decision.State);
                Assert.AreEqual(controller.skyRoute[0].y, witch.transform.position.y, 1e-4f);
                float height = fireLight.transform.position.y - groundHeight;
                float expected = WitchPoses.FireIntensity(baseIntensity, height, controller.fireFullHeight);
                Assert.IsTrue(fireLight.enabled);
                Assert.AreEqual(expected, fireLight.intensity, expected * 0.1f);
            }
        }

        [UnityTest]
        public IEnumerator LiveCaptureKeepsControllerRunningThroughLanding()
        {
            var character = player.AddComponent<CharacterController>();
            character.height = 1.2f;
            character.center = Vector3.up * 0.6f;
            var stage = world.AddComponent<WitchCaptureStage>();
            stage.live = true;
            Vector3 witchPosition = witch.transform.position;

            stage.Apply(player.transform);

            Assert.IsTrue(controller.enabled);
            Assert.IsTrue(character.enabled);
            Assert.AreEqual(witchPosition, witch.transform.position);
            Assert.AreEqual(new Vector3(witchPosition.x, 0f, witchPosition.z), player.transform.position);
            Assert.IsNull(GameObject.Find("Witch Capture Light"));
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Landing");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Stalking");
            yield return WaitForStalking();
        }

        [Test]
        public void GroundCapturePlacesWitchOnSurfaceBelowOffset()
        {
            player.transform.position = Vector3.zero;
            var platform = AddBox("Platform", new Vector3(1.5f, 0.5f, 3f), new Vector3(4f, 1f, 4f));
            var stage = world.AddComponent<WitchCaptureStage>();
            stage.state = WitchState.Stalking;
            stage.witchOffset = new Vector3(1.5f, 0f, 3f);
            stage.lightWitch = false;

            stage.Apply(player.transform);

            Assert.IsFalse(controller.enabled);
            Assert.AreEqual(new Vector3(1.5f, platform.bounds.max.y, 3f), witch.transform.position);
        }

        [UnityTest]
        public IEnumerator PlayerLightLeadsToLandingAndGroundedStalking()
        {
            TurnOnPlayerLight();

            yield return WaitForStalking();
        }

        [UnityTest]
        public IEnumerator LandingPushesWitchOutsideSafeZone()
        {
            SafeZones.Add(new Vector3(8f, 0f, 0f), 5f);
            TurnOnPlayerLight();
            Assert.IsFalse(SafeZones.Contains(player.transform.position));

            yield return WaitForStalking();

            Assert.IsFalse(SafeZones.Contains(witch.transform.position));
        }

        [UnityTest]
        public IEnumerator WitchStalksAroundAFamilyCandleWithoutCrossingIt()
        {
            var candle = AddFamilyCandle();
            player.transform.position = new Vector3(0f, 0f, 6f);
            witch.transform.position = new Vector3(0f, 0f, -6f);
            controller.playerHidden = true;
            controller.raining = true;
            Physics.SyncTransforms();
            yield return WaitForStalking();
            float initialDistance = HorizontalDistanceToPlayer();
            NoiseBus.Publish(new StepNoise(player.transform.position, 20f));
            yield return WaitForState(WitchState.Suspicious, 1f);
            yield return WaitForState(WitchState.Stalking, controller.Profile.SuspicionDuration + 1f);

            float deadline = Time.time + 12f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
                Vector3 offset = witch.transform.position - candle.transform.position;
                offset.y = 0f;
                Assert.GreaterOrEqual(offset.magnitude, candle.radius - 0.05f);
            }

            Assert.Less(HorizontalDistanceToPlayer(), initialDistance - 4f);
            Assert.Greater(witch.transform.position.z, candle.radius);
        }

        [UnityTest]
        public IEnumerator LandingNearLightInsideFamilyCandleEndsOutsideRadius()
        {
            var candle = AddFamilyCandle();
            controller.enabled = false;
            LightPlayerAt(new Vector3(0f, 0f, 6f));
            controller.playerLightPosition = candle.transform.position;
            witch.transform.position = new Vector3(0.5f, 14f, 0f);
            yield return new WaitForSeconds(0.6f);
            controller.enabled = true;
            Assert.IsTrue(SafeZones.Contains(controller.playerLightPosition));

            yield return WaitForStalking();

            Vector3 offset = witch.transform.position - candle.transform.position;
            offset.y = 0f;
            Assert.GreaterOrEqual(offset.magnitude, candle.radius);
        }

        [UnityTest]
        public IEnumerator StalkingClosesHorizontalDistanceOnNavMesh()
        {
            TurnOnPlayerLight();
            yield return WaitForStalking();
            LightPlayerAt(witch.transform.position + Vector3.right * 14f);
            float initialDistance = HorizontalDistanceToPlayer();

            float deadline = Time.time + 1f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
                Assert.IsTrue(agent.isOnNavMesh);
#if UNITY_EDITOR
                Vector3 velocity = agent.velocity;
                velocity.y = 0f;
                Assert.AreEqual(velocity.magnitude, controller.figure.GetComponent<Animator>().GetFloat("Speed"), 0.01f);
#endif
            }

            Assert.LessOrEqual(HorizontalDistanceToPlayer(), initialDistance - 0.5f);
        }

        [UnityTest]
        public IEnumerator SuspicionStopsMovementFacesNoiseAndResumesNavMesh()
        {
            controller.raining = true;
            yield return WaitForStalking();
            controller.raining = false;
            Vector3 origin = witch.transform.position + Vector3.right * 15f;
            NoiseBus.Publish(new StepNoise(origin, 20f));
            yield return WaitForState(WitchState.Suspicious, 1f);
            Vector3 anchor = witch.transform.position;
            float deadline = Time.time + controller.Profile.SuspicionDuration - 0.2f;
            while (Time.time < deadline)
            {
                Assert.AreEqual(WitchState.Suspicious, controller.Decision.State);
                Assert.AreEqual(anchor, witch.transform.position);
                Assert.IsFalse(agent.enabled);
                Assert.Less(Vector3.Angle(Vector3.right, witch.transform.forward), 0.1f);
                yield return null;
            }
            yield return WaitForState(WitchState.Stalking, 1f);
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
            Assert.AreEqual(origin, controller.Decision.MoveTarget);
        }

        [UnityTest]
        public IEnumerator WindUpKeepsPositionWithoutTryingToSnapToNavMesh()
        {
            witch.transform.position = Vector3.zero;
            LightPlayerAt(Vector3.right * 6f);
            yield return WaitForStalking();
            yield return WaitForState(WitchState.WindUp, 1f);
            Vector3 anchor = witch.transform.position;
            surface.RemoveData();
            float started = Time.time;
            while (controller.Decision.State == WitchState.WindUp)
            {
                Assert.AreEqual(anchor, witch.transform.position);
                Assert.IsFalse(agent.enabled);
                yield return null;
            }
            Assert.GreaterOrEqual(Time.time - started, controller.Profile.WindUpDuration - Time.deltaTime * 2f);
            Assert.AreEqual(WitchState.Sprinting, controller.Decision.State);
        }

        [UnityTest]
        public IEnumerator CachedHideNearNoiseIsReachedInspectedAndLeft()
        {
            Object.Destroy(witch);
            yield return null;
            var nearWitch = AddBox("Hide Near Witch", Vector3.left, Vector3.one);
            nearWitch.gameObject.AddComponent<HideVolume>();
            var nearNoise = AddBox("Hide Near Noise", Vector3.right * 12f, Vector3.one);
            var hide = nearNoise.gameObject.AddComponent<HideVolume>();
            CreateWitch();
            controller.raining = true;
            yield return WaitForStalking();
            controller.raining = false;
            NoiseBus.Publish(new StepNoise(Vector3.right * 13f, 20f));
            yield return WaitForState(WitchState.Suspicious, 1f);
            yield return WaitForState(WitchState.Stalking, controller.Profile.SuspicionDuration + 1f);
            Assert.IsTrue(NavMesh.SamplePosition(hide.HidePoint, out var hidePoint, 1f, NavMesh.AllAreas));
            Assert.AreEqual(hidePoint.position, controller.Decision.MoveTarget);
            float deadline = Time.time + 15f;
            while (!controller.Decision.InspectingHide && Time.time < deadline)
            {
                Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
                Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
                yield return null;
            }
            Assert.IsTrue(controller.Decision.InspectingHide);
            Assert.AreEqual(0f, controller.Decision.Speed);
            Assert.IsTrue(agent.isStopped);
            yield return new WaitForSeconds(controller.Profile.HideInspectionDuration - 0.2f);
            Assert.IsTrue(controller.Decision.InspectingHide);
            yield return WaitForState(WitchState.TakingOff, 1f);
        }

        [UnityTest]
        public IEnumerator SprintCatchesPlayerOnceDespiteOwnSightCollider()
        {
            witch.transform.position = Vector3.zero;
            agent.updateRotation = false;
            LightPlayerAt(Vector3.right * 6f);
            var body = AddBox("WitchBody", new Vector3(1f, 1.4f, 0f), Vector3.one * 0.4f);
            body.transform.SetParent(witch.transform, true);
            int caughtCount = 0;
            controller.Caught += () => caughtCount++;

            yield return WaitForStalking();
            yield return WaitForState(WitchState.WindUp, 1f);
            yield return WaitForState(WitchState.Sprinting, 2f);
            yield return WaitForState(WitchState.Caught, 3f);

            float deadline = Time.time + 1f;
            while (Time.time < deadline)
            {
                Assert.AreEqual(1, caughtCount);
                Assert.AreEqual(WitchState.Caught, controller.Decision.State);
                yield return null;
            }
            Assert.AreEqual(1, caughtCount);
        }

        [UnityTest]
        public IEnumerator ResetBrainReturnsTheWitchToTheSky()
        {
            witch.transform.position = Vector3.zero;
            LightPlayerAt(Vector3.right * 6f);
            int caughtCount = 0;
            controller.Caught += () => caughtCount++;
            yield return WaitForState(WitchState.Caught, 8f);
            Assert.AreEqual(1, caughtCount);
            controller.playerLightOn = false;

            controller.ResetBrain();

            Assert.AreEqual(WitchState.Flying, controller.Decision.State);
            Assert.AreEqual(WitchForm.Flying, controller.Decision.Form);
            Assert.AreEqual(controller.skyRoute[1], witch.transform.position);
            Assert.IsFalse(agent.enabled);
            Assert.IsTrue(controller.Decision.FireLit && fireLight.enabled);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(WitchState.Flying, controller.Decision.State);
            Assert.AreEqual(1, caughtCount);
        }

        [UnityTest]
        public IEnumerator CemeteryRespawnKeepsTheHigherRouteWithoutAccumulatingHeight()
        {
            controller.chapter = Chapter.Cemetery;
            Vector3 expected = controller.skyRoute[1] + Vector3.up * WitchProfiles.For(Chapter.Cemetery).FlightHeightOffset;
            controller.ResetBrain();
            Assert.AreEqual(expected, witch.transform.position);
            controller.ResetBrain();
            Assert.AreEqual(expected, witch.transform.position);
            yield return null;
            Assert.AreEqual(expected.y, witch.transform.position.y, 0.01f);
            Assert.AreEqual(WitchState.Flying, controller.Decision.State);
        }

        [UnityTest]
        public IEnumerator ChapterUpgradeKeepsLandingPositionAndEnablesForestLureWithoutRestarting()
        {
            controller.chapter = Chapter.WaterPlant;
            controller.ResetBrain();
            controller.raining = true;
            yield return WaitForState(WitchState.Landing, 1f);
            Vector3 position = witch.transform.position;
            WitchDecision decision = controller.Decision;

            controller.SetChapter(Chapter.TrainTracks);

            Assert.AreEqual(Chapter.TrainTracks, controller.chapter);
            Assert.AreEqual(position, witch.transform.position);
            Assert.AreEqual(decision, controller.Decision);
            Assert.IsFalse(agent.enabled);
            yield return WaitForStalking();
            Assert.IsTrue(controller.Profile.BarkLureEnabled);
            Assert.IsFalse(controller.Decision.VoiceLure, "Las vías habilitan el ladrido del bosque, no la voz al aterrizar.");
            controller.SetChapter(Chapter.Cemetery);
            Assert.AreEqual(Chapter.TrainTracks, controller.chapter);
            yield return null;
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.IsFalse(controller.Decision.VoiceLure, "El ladrido permanece apagado fuera de su punto de llamada.");
        }

        [UnityTest]
        public IEnumerator ResetBrainKeepsReturnChaseOnGroundAwayFromPlayerAndSafeZones()
        {
            player.transform.position = Vector3.zero;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            controller.BeginReturn();
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: WindUp");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Sprinting");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Caught");
            yield return WaitForState(WitchState.Caught, 8f);
            var candle = AddFamilyCandle();
            player.transform.position = candle.transform.position + Vector3.back;
            SafeZones.Add(player.transform.position - player.transform.forward * 12f, 3f);
            controller.Flash();
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: retoma la persecución a pie");
            controller.ResetBrain();

            Assert.AreEqual(Chapter.Return, controller.chapter);
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.AreEqual(WitchForm.Grounded, controller.Decision.Form);
            Assert.IsFalse(controller.Decision.FireLit || fireLight.enabled);
            Assert.GreaterOrEqual(HorizontalDistanceToPlayer(), 10f);
            Assert.IsFalse(SafeZones.Contains(witch.transform.position));
            Assert.Less(Mathf.Abs(witch.transform.position.y), 0.3f);
            yield return null;
            yield return null;
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);

            player.transform.position = Vector3.right * 20f;
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.AreEqual(WitchForm.Grounded, controller.Decision.Form);
            Assert.AreEqual(2.5f, controller.Decision.Speed);
            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out var playerPoint, 6f, NavMesh.AllAreas));
            Assert.AreEqual(playerPoint.position, controller.Decision.MoveTarget);
            Assert.IsFalse(controller.Decision.FireLit || fireLight.enabled);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SprintStopsAtWallAndRecoversOnNavMesh()
        {
            witch.transform.position = Vector3.zero;
            LightPlayerAt(Vector3.right * 6f);
            var wall = AddBox("Wall", new Vector3(3f, 1f, 3.1f), new Vector3(0.5f, 2f, 6f));
            float wallPlane = wall.bounds.min.x;
            yield return WaitForStalking();
            yield return WaitForState(WitchState.WindUp, 1f);

            bool sawSprint = false;
            float deadline = Time.time + 4f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Recovering)
            {
                if (controller.Decision.State == WitchState.Sprinting)
                {
                    sawSprint = true;
                    Assert.LessOrEqual(witch.transform.position.x, wallPlane);
                }
                yield return null;
            }

            Assert.IsTrue(sawSprint);
            Assert.AreEqual(WitchState.Recovering, controller.Decision.State);
            Assert.That(witch.transform.position.x, Is.InRange(1f, wallPlane));
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
        }

        [UnityTest]
        public IEnumerator SprintStaysInsideNarrowNavMesh()
        {
            surface.RemoveData();
            Object.Destroy(surface.navMeshData);
            ground.transform.localScale = new Vector3(10f, 1f, 80f);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            witch.transform.position = Vector3.zero;
            LightPlayerAt(Vector3.right * 8f);
            yield return WaitForStalking();
            yield return WaitForState(WitchState.WindUp, 1f);

            bool sawSprint = false;
            float deadline = Time.time + 4f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Recovering)
            {
                if (controller.Decision.State == WitchState.Sprinting)
                {
                    sawSprint = true;
                    Assert.That(witch.transform.position.x, Is.InRange(-5.5f, 5.5f));
                    Assert.That(witch.transform.position.z, Is.InRange(-40.5f, 40.5f));
                }
                yield return null;
            }

            Assert.IsTrue(sawSprint);
            Assert.AreEqual(WitchState.Recovering, controller.Decision.State);
            Assert.That(witch.transform.position.x, Is.InRange(3f, 5.5f));
            Assert.IsTrue(agent.isOnNavMesh);
        }

        [UnityTest]
        public IEnumerator LandingWithoutNavMeshKeepsAgentDisabledAndRetries()
        {
            AddBox("UnbakedPlatform", new Vector3(0f, 29.5f, 0f), new Vector3(6f, 1f, 6f));
            witch.transform.position = new Vector3(0f, 44f, 0f);
            controller.raining = true;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Landing");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Bruja: Stalking");
            LogAssert.Expect(LogType.Warning,
                $"{GameInfo.LogPrefix} La bruja no encontró NavMesh cerca de {new Vector3(0f, 30f, 0f)}.");
            yield return WaitForState(WitchState.Stalking, 4f);

            int disconnectedFrames = 0;
            float deadline = Time.time + 1f;
            while (Time.time < deadline)
            {
                disconnectedFrames = agent.enabled && !agent.isOnNavMesh ? disconnectedFrames + 1 : 0;
                Assert.LessOrEqual(disconnectedFrames, 1);
                Assert.AreEqual(30f, witch.transform.position.y, 0.01f);
                yield return null;
            }
            Assert.IsFalse(agent.enabled);

            witch.transform.position = new Vector3(0f, 18f, 0f);
            deadline = Time.time + 1f;
            while (Time.time < deadline && !(agent.enabled && agent.isOnNavMesh)) yield return null;

            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
            Assert.Less(Mathf.Abs(witch.transform.position.y), 0.3f);

            agent.enabled = false;
            controller.enabled = false;
            witch.transform.position = new Vector3(0f, 30f, 0f);
            controller.enabled = true;
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(agent.enabled);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LandingFindsNavMeshBeyondSixMeters()
        {
            AddBox("UnbakedPlatform", new Vector3(0f, 17.5f, 0f), new Vector3(6f, 1f, 6f));
            witch.transform.position = new Vector3(0f, 32f, 0f);
            controller.raining = true;
            yield return WaitForState(WitchState.Landing, 1f);

            float deadline = Time.time + 1f;
            while (Time.time < deadline) yield return null;

            Assert.AreEqual(WitchState.Landing, controller.Decision.State);
            Assert.Less(witch.transform.position.y, 16f);
            yield return WaitForStalking();
        }

        [UnityTest]
        public IEnumerator LandingIgnoresOwnCoverAndUsesGroundMask()
        {
            var canopy = AddBox("Canopy", new Vector3(0f, 29.5f, 0f), new Vector3(6f, 1f, 6f));
            canopy.gameObject.layer = 8;
            witch.transform.position = new Vector3(0f, 36f, 0f);
            var body = AddBox("WitchBody", new Vector3(0f, 35f, 0f), Vector3.one);
            body.gameObject.layer = 9;
            body.transform.SetParent(witch.transform, true);
            controller.coverMask = 1 << 9;
            controller.groundMask = 1 << ground.layer;
            LightPlayerAt(Vector3.zero);

            yield return null;
            Assert.AreEqual(WitchState.Landing, controller.Decision.State);
            yield return WaitForStalking();
        }

        private CandleSpot AddFamilyCandle()
        {
            var candle = new GameObject("Family Candle");
            candle.SetActive(false);
            candle.transform.SetParent(world.transform, false);
            var obstacle = candle.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.radius = CandleSite.DefaultRadius;
            obstacle.height = 2f;
            obstacle.center = Vector3.up;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            var spot = candle.AddComponent<CandleSpot>();
            candle.SetActive(true);
            Assert.IsTrue(SafeZones.Contains(candle.transform.position));
            return spot;
        }

        private BoxCollider AddBox(string name, Vector3 position, Vector3 size)
        {
            var box = new GameObject(name);
            box.transform.SetParent(world.transform);
            box.transform.position = position;
            var collider = box.AddComponent<BoxCollider>();
            collider.size = size;
            Physics.SyncTransforms();
            return collider;
        }

        private float HorizontalDistanceToPlayer()
        {
            Vector3 offset = witch.transform.position - player.transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        private IEnumerator WaitForState(WitchState state, float timeout)
        {
            float deadline = Time.time + timeout;
            while (Time.time < deadline && controller.Decision.State != state) yield return null;
            Assert.AreEqual(state, controller.Decision.State);
        }

        private void TurnOnPlayerLight()
        {
            LightPlayerAt(new Vector3(20f, 0f, 0f));
        }

        private void LightPlayerAt(Vector3 position)
        {
            player.transform.position = position;
            controller.playerLightOn = true;
            controller.playerLightPosition = player.transform.position;
            Physics.SyncTransforms();
        }

        private IEnumerator WaitForStalking()
        {
            bool sawLanding = false;
            float deadline = Time.time + 6f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Stalking)
            {
                sawLanding |= controller.Decision.State == WitchState.Landing;
                yield return null;
            }

            Assert.IsTrue(sawLanding, "La bruja debe pasar por aterrizaje.");
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.IsTrue(agent.enabled);
            Assert.IsTrue(agent.isOnNavMesh);
            Assert.Less(Mathf.Abs(witch.transform.position.y), 0.3f);
        }
    }
}
