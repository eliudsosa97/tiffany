using System.Collections;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class PlayerLightTests
    {
        private static readonly float[] SlopeAngles = { 8f, -8f };
        private static readonly bool[] CaptureFlashCases = { false, true };
        private static readonly bool[] LightStates = { false, true };
        private static readonly (float, bool, bool)[] GroundedFlashCases =
        {
            (5f, true, true),
            (8f, false, true),
            (13f, false, false)
        };

        private GameObject world;
        private GameObject ground;
        private GameObject player;
        private PlayerSoul soul;
        private PlayerLight playerLight;
        private Light spot;
        private NavMeshSurface surface;
        private WitchController controller;
        private NavMeshAgent agent;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            Time.timeScale = 1f;
            GameInput.InteractOverride = false;
            GameInput.LightHeldOverride = false;
            world = new GameObject("PlayerLightTestWorld");
            ground = AddBox("Ground", Vector3.down * 0.5f, new Vector3(80f, 1f, 80f)).gameObject;
            CreatePlayer();
        }

        private void CreatePlayer(bool canHide = false, bool withBody = false, bool unlocked = true)
        {
            player = new GameObject("Player") { tag = "Player" };
            player.SetActive(false);
            player.transform.SetParent(world.transform, false);
            soul = player.AddComponent<PlayerSoul>();
            soul.startingPowers = unlocked ? new[] { Power.Light } : System.Array.Empty<Power>();
            if (canHide)
            {
                player.AddComponent<PlayerController>().enabled = false;
                player.AddComponent<PlayerHiding>();
            }
            spot = new GameObject("Soul Light").AddComponent<Light>();
            spot.transform.SetParent(player.transform, false);
            spot.transform.localPosition = new Vector3(0f, 2.4f, -0.3f);
            spot.type = LightType.Spot;
            spot.spotAngle = 70f;
            spot.innerSpotAngle = 40f;
            spot.range = 11f;
            spot.intensity = 18f;
            playerLight = player.AddComponent<PlayerLight>();
            playerLight.spot = spot;
            playerLight.baseGlow = new GameObject("Soul Glow").AddComponent<Light>();
            playerLight.baseGlow.transform.SetParent(player.transform, false);
            playerLight.burstLight = new GameObject("Flash Burst").AddComponent<Light>();
            playerLight.burstLight.transform.SetParent(player.transform, false);
            playerLight.burstLight.type = LightType.Point;
            playerLight.burstLight.intensity = 70f;
            playerLight.burstLight.range = FlashCharge.BurstRadius;
            playerLight.burstLight.enabled = false;
            if (withBody)
            {
                var body = new GameObject("Body", typeof(MeshRenderer));
                body.transform.SetParent(player.transform, false);
                var renderer = body.GetComponent<Renderer>();
                renderer.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Models/Materials/TiffanySkin.mat");
                playerLight.bodyRenderers = new[] { renderer };
                playerLight.visual = body.transform;
                playerLight.bodyGlowBoost = 1.2f;
            }
            playerLight.startOn = true;
            player.SetActive(true);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SafeZones.Clear();
            Time.timeScale = 1f;
            GameInput.ClearOverrides();
            if (surface != null)
            {
                surface.RemoveData();
                Object.Destroy(surface.navMeshData);
            }
            Object.Destroy(world);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartingLightDrainsEnergyAndIlluminatesGroundAhead()
        {
            yield return null;
            float initialEnergy = soul.Energy.Current;
            yield return new WaitForSeconds(1f);
            Assert.That(initialEnergy - soul.Energy.Current, Is.InRange(0.8f, 1.5f));
            Assert.IsTrue(spot.enabled);
            Assert.AreEqual(0f, playerLight.LitPoint.y, 0.01f);
            Vector3 offset = playerLight.LitPoint - player.transform.position;
            offset.y = 0f;
            Assert.AreEqual(2.4f / Mathf.Tan(playerLight.pitch * Mathf.Deg2Rad) - 0.3f, offset.magnitude, 0.2f);
            Assert.Greater(Vector3.Dot(offset, player.transform.forward), 0f);
        }

        [UnityTest]
        public IEnumerator BodyGlowSmoothsToBoostAndBackWithoutChangingSharedMaterial()
        {
            player.SetActive(false);
            CreatePlayer(withBody: true);
            var renderer = playerLight.bodyRenderers[0];
            var material = renderer.sharedMaterial;
            var properties = new MaterialPropertyBlock();
            yield return new WaitForSeconds(0.05f);
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_RimIntensity"), Is.GreaterThan(1.5f).And.LessThan(3.3f));
            yield return new WaitForSeconds(0.8f);
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(3.3f, properties.GetFloat("_RimIntensity"), 0.02f);
            Time.timeScale = 0f;
            yield return null;
            Time.timeScale = 1f;
            playerLight.Toggle();
            yield return new WaitForSeconds(0.8f);
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(1.5f, properties.GetFloat("_RimIntensity"), 0.02f);
            Assert.AreSame(material, renderer.sharedMaterial);
            Assert.AreEqual(1.5f, material.GetFloat("_RimIntensity"));
        }

        [UnityTest]
        public IEnumerator EmptyEnergyTurnsOffSpotAndKeepsBaseGlow()
        {
            yield return null;
            soul.Energy.ResetTo(0.5f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, soul.Energy.Current);
            Assert.IsFalse(spot.enabled);
            Assert.IsTrue(playerLight.baseGlow.enabled);
        }

        [UnityTest]
        public IEnumerator BaseGlowNeedsNeitherPowerNorEnergy()
        {
            player.SetActive(false);
            CreatePlayer(unlocked: false);
            soul.Energy.ResetTo(0f);
            yield return null;
            Assert.IsFalse(soul.Powers.Has(Power.Light) || spot.enabled);
            Assert.IsTrue(playerLight.baseGlow.enabled);
            soul.Energy.ResetTo(SoulEnergy.Max);
            yield return null;
            Assert.IsFalse(spot.enabled);
            Assert.IsTrue(playerLight.baseGlow.enabled);
        }

        [UnityTest]
        public IEnumerator MotionDemoUnlocksLightBeforeItsFirstToggle()
        {
            yield return null;
            playerLight.Toggle();
            soul.Powers.Clear();
            world.AddComponent<MotionDemo>();
            yield return null;
            yield return null;
            Assert.IsTrue(soul.Powers.Has(Power.Light) && playerLight.State.IsOn);
            yield return null;
            Assert.IsTrue(playerLight.State.IsOn);
        }

        [UnityTest]
        public IEnumerator GroundRaysFollowSlopesAndIgnorePlayerTriggersAndExcludedLayers(
            [ValueSource(nameof(SlopeAngles))] float angle)
        {
            ground.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            ground.transform.position = -ground.transform.up * 0.5f;
            ground.layer = 8;
            playerLight.groundMask = 1 << 8;
            var ownBox = AddBox("PlayerCollider", new Vector3(0f, 0.3f, 1f));
            ownBox.transform.SetParent(player.transform, true);
            ownBox.gameObject.layer = 8;
            var trigger = AddBox("Trigger", new Vector3(0f, 0.6f, 1f));
            trigger.gameObject.layer = 8;
            trigger.isTrigger = true;
            AddBox("Excluded", new Vector3(0f, 0.8f, 1f));
            Physics.SyncTransforms();
            yield return null;
            Vector3 tangent = Vector3.ProjectOnPlane(spot.transform.forward, ground.transform.up);
            Assert.AreEqual(playerLight.pitch, Vector3.Angle(tangent, spot.transform.forward), 2f);
            Assert.Less(Vector3.Dot(spot.transform.forward, ground.transform.up), 0f);
            Assert.AreEqual(0f, Vector3.Dot(playerLight.LitPoint, ground.transform.up), 0.01f);
        }

        [UnityTest]
        public IEnumerator MissingGroundUsesLevelDirectionAndHalfRangeFallback()
        {
            ground.SetActive(false);
            Physics.SyncTransforms();
            yield return null;
            Assert.AreEqual(playerLight.pitch, 90f - Vector3.Angle(spot.transform.forward, Vector3.down), 0.01f);
            Assert.AreEqual(player.transform.position + player.transform.forward * 5.5f, playerLight.LitPoint);
        }

        [UnityTest]
        public IEnumerator LowEnergyShrinksSpotAndRechargeRestoresBaseValues()
        {
            yield return null;
            soul.Energy.ResetTo(8f);
            yield return null;
            Assert.Less(spot.range, 11f);
            Assert.Less(spot.spotAngle, 70f);
            Assert.That(spot.intensity, Is.InRange(18f * SoulLight.MinFlicker, 18f));
            soul.Energy.ResetTo(SoulEnergy.Max);
            yield return null;
            Assert.AreEqual(11f, spot.range);
            Assert.AreEqual(70f, spot.spotAngle);
            Assert.AreEqual(18f, spot.intensity);
        }

        [UnityTest]
        public IEnumerator FlashWorksWithoutWitchAndBoostsSpotWhileSwitchIsOff()
        {
            yield return null;
            if (playerLight.State.IsOn) playerLight.Toggle();
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: estallido");
            Assert.IsTrue(playerLight.TryBurst());
            yield return null;
            Assert.IsFalse(playerLight.State.IsOn);
            Assert.IsTrue(spot.enabled);
            Assert.AreEqual(18f * playerLight.flashBoost, spot.intensity);
            yield return new WaitForSeconds(SoulLight.FlashDuration);
            Assert.IsFalse(spot.enabled);
        }

        [UnityTest]
        public IEnumerator BurstDoesNotReportStunForFlyingWitch()
        {
            CreateWitch(Chapter.Cemetery);
            yield return null;
            Assert.AreEqual(WitchForm.Flying, controller.Decision.Form);
            Assert.IsTrue(controller.playerLightOn);
            Assert.AreEqual(playerLight.LitPoint, controller.playerLightPosition);
            if (playerLight.State.IsOn) playerLight.Toggle();
            yield return null;
            Assert.IsFalse(controller.playerLightOn);
            Assert.AreEqual(playerLight.LitPoint, controller.playerLightPosition);
            PlacePlayerNearWitch(5f, true);
            Assert.IsTrue(SoulLight.InCone(player.transform.position, player.transform.forward,
                controller.transform.position, 11f, 35f));
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: estallido");
            float initialEnergy = soul.Energy.Current;
            Assert.IsTrue(playerLight.TryBurst());
            Assert.AreEqual(initialEnergy - SoulCosts.Flash, soul.Energy.Current, 1e-4f);
            yield return null;
            Assert.AreEqual(WitchState.Flying, controller.Decision.State);
            Assert.IsTrue(controller.playerLightOn);
        }

        [UnityTest]
        public IEnumerator BurstStunsGroundedWitchWithinRadiusInAnyDirection(
            [ValueSource(nameof(GroundedFlashCases))] (float distance, bool facingWitch, bool stuns) testCase)
        {
            var (distance, facingWitch, stuns) = testCase;
            player.transform.position = new Vector3(20f, 0f, 0f);
            CreateWitch();
            bool sawLanding = false;
            float deadline = Time.time + 6f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Stalking)
            {
                sawLanding |= controller.Decision.State == WitchState.Landing;
                yield return null;
            }
            Assert.IsTrue(sawLanding);
            Assert.AreEqual(WitchState.Stalking, controller.Decision.State);
            Assert.AreEqual(WitchForm.Grounded, controller.Decision.Form);
            Assert.IsTrue(agent.enabled && agent.isOnNavMesh);
            PlacePlayerNearWitch(distance, facingWitch);
            if (!facingWitch) Assert.IsFalse(SoulLight.InCone(player.transform.position, player.transform.forward,
                controller.transform.position, spot.range, spot.spotAngle * 0.5f));
            controller.enabled = false;
            agent.isStopped = true;
            playerLight.Toggle();
            float energy = soul.Energy.Current;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: carga");
            LogAssert.Expect(LogType.Log, stuns
                ? $"{GameInfo.LogPrefix} Destello: estallido, la bruja queda aturdida" : $"{GameInfo.LogPrefix} Destello: estallido");
            GameInput.LightHeldOverride = true;
            deadline = Time.time + FlashCharge.ChargeSeconds + 1f;
            while (!playerLight.State.IsFlashing && Time.time < deadline) yield return null;
            Assert.IsTrue(playerLight.State.IsFlashing);
            Assert.AreEqual(energy - SoulCosts.Flash, soul.Energy.Current, 1e-4f);
            GameInput.LightHeldOverride = false;
            controller.enabled = true;
            deadline = Time.time + 0.5f;
            while (Time.time < deadline)
            {
                if (stuns && controller.Decision.State == WitchState.Stunned) break;
                if (!stuns) Assert.AreNotEqual(WitchState.Stunned, controller.Decision.State);
                yield return null;
            }
            Assert.AreEqual(stuns, controller.Decision.State == WitchState.Stunned);
        }

        [UnityTest]
        public IEnumerator ReleasingChargeAtOneAndAHalfSecondsCostsNothingAndLowersOnlyVisual()
        {
            player.SetActive(false);
            CreatePlayer(canHide: true, withBody: true);
            yield return null;
            playerLight.Toggle();
            float energy = soul.Energy.Current;
            Vector3 rootPosition = player.transform.position;
            Vector3 visualPosition = playerLight.visual.localPosition;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: carga");
            GameInput.LightHeldOverride = true;
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(playerLight.Charge.Charging);
            Assert.AreEqual(FlashCharge.ChargeMoveScale, player.GetComponent<PlayerController>().speedScale);
            Assert.Greater(playerLight.visual.localPosition.y, visualPosition.y);
            Assert.AreEqual(rootPosition, player.transform.position);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: cancelado");
            GameInput.LightHeldOverride = false;
            yield return null;
            Assert.IsFalse(playerLight.State.IsFlashing || playerLight.State.IsOn);
            Assert.AreEqual(energy, soul.Energy.Current);
            Assert.AreEqual(1f, player.GetComponent<PlayerController>().speedScale);
            yield return new WaitForSeconds(FlashCharge.PulseFade + 0.1f);
            Assert.AreEqual(visualPosition, playerLight.visual.localPosition);
        }

        [UnityTest]
        public IEnumerator BurstPulseRisesAndFadesWithoutRepeatingWhileHeld()
        {
            yield return null;
            playerLight.Toggle();
            float energy = soul.Energy.Current;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: carga");
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: estallido");
            GameInput.LightHeldOverride = true;
            float deadline = Time.time + FlashCharge.ChargeSeconds + 1f;
            while (!playerLight.State.IsFlashing && Time.time < deadline) yield return null;
            Assert.IsTrue(playerLight.State.IsFlashing);
            yield return new WaitForSeconds(FlashCharge.PulseRise);
            Assert.IsTrue(playerLight.burstLight.enabled);
            Assert.Greater(playerLight.burstLight.range, FlashCharge.BurstRadius * 0.8f);
            yield return new WaitForSeconds(FlashCharge.PulseFade + FlashCharge.ChargeSeconds);
            Assert.IsFalse(playerLight.burstLight.enabled || playerLight.State.IsFlashing);
            Assert.AreEqual(energy - SoulCosts.Flash, soul.Energy.Current, 1e-4f);
            GameInput.LightHeldOverride = false;
            yield return null;
            Assert.IsFalse(playerLight.State.IsOn);
        }

        [UnityTest]
        public IEnumerator CaptureRestoresStateAndHandlesScheduledFlash(
            [ValueSource(nameof(CaptureFlashCases))] bool clearBeforeFlash)
        {
            CreateWitch(Chapter.Cemetery);
            yield return null;
            playerLight.Toggle();
            soul.Powers.Clear();
            var stop = new GameObject("Light Capture");
            stop.transform.SetParent(world.transform);
            stop.AddComponent<CaptureStop>().wait = 0.4f;
            var stage = stop.AddComponent<LightCaptureStage>();
            stage.energy = 8f;
            stage.flash = true;
            if (!clearBeforeFlash) LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: estallido");
            stage.Apply(player.transform);
            Assert.IsTrue(soul.Powers.Has(Power.Light));
            Assert.AreEqual(8f, soul.Energy.Current);
            Assert.IsTrue(playerLight.State.IsOn);
            Assert.IsFalse(controller.gameObject.activeSelf);
            if (!clearBeforeFlash)
            {
                float deadline = Time.time + 1f;
                while (!playerLight.State.IsFlashing && Time.time < deadline) yield return null;
                Assert.IsTrue(playerLight.State.IsFlashing);
                Assert.That(soul.Energy.Current, Is.InRange(49f, 50f));
            }
            stage.Clear();
            Assert.AreEqual(SoulEnergy.Max, soul.Energy.Current);
            Assert.IsFalse(playerLight.State.IsOn);
            Assert.AreEqual(clearBeforeFlash, controller.gameObject.activeSelf);
            if (clearBeforeFlash) Assert.IsFalse(controller.playerLightOn);
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(controller.gameObject.activeSelf);
            Assert.IsFalse(controller.playerLightOn);
            Assert.Greater(soul.Energy.Current, 98f);
            Assert.IsFalse(playerLight.State.IsFlashing);
        }

        [UnityTest]
        public IEnumerator FlashRequiresClearChestRayOnSlope([ValueSource(nameof(SlopeAngles))] float angle)
        {
            ground.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
            ground.transform.position = -ground.transform.up * 0.5f;
            player.transform.position = new Vector3(20f, 0f, 0f);
            CreateWitch();
            float deadline = Time.time + 6f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Stalking) yield return null;
            Assert.AreEqual(WitchForm.Grounded, controller.Decision.Form);
            PlacePlayerNearWitch(5f, true);
            var position = player.transform.position;
            position.y = -position.z * Mathf.Tan(angle * Mathf.Deg2Rad);
            player.transform.position = position;
            var middle = (position + controller.transform.position) * 0.5f + Vector3.up * 0.75f;
            var size = new Vector3(1.5f, 1.5f, 0.5f);
            var blocker = AddBox("Flash Blocker", middle, size);
            Physics.SyncTransforms();
            float energy = soul.Energy.Current;
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: estallido");
            Assert.IsTrue(playerLight.TryBurst());
            Assert.AreEqual(energy - SoulCosts.Flash, soul.Energy.Current, 1e-4f);
            Assert.IsTrue(playerLight.State.IsFlashing);
            yield return null;
            Assert.IsTrue(spot.enabled);
            Assert.AreNotEqual(WitchState.Stunned, controller.Decision.State);
            blocker.enabled = false;
            AddBox("Own Collider", middle, size).transform.SetParent(player.transform, true);
            AddBox("Witch Collider", middle, size).transform.SetParent(controller.transform, true);
            AddBox("Trigger", middle, size).isTrigger = true;
            AddBox("Excluded", middle, size).gameObject.layer = 8;
            playerLight.occluderMask = ~(1 << 8);
            Physics.SyncTransforms();
            soul.Energy.ResetTo(SoulEnergy.Max);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: estallido, la bruja queda aturdida");
            Assert.IsTrue(playerLight.TryBurst());
            deadline = Time.time + 0.5f;
            while (Time.time < deadline && controller.Decision.State != WitchState.Stunned) yield return null;
            Assert.AreEqual(WitchState.Stunned, controller.Decision.State);
        }

        [UnityTest]
        public IEnumerator HidingExtinguishesLightAndBlocksToggleAndFlash()
        {
            player.SetActive(false);
            CreatePlayer(true, withBody: true);
            CreateWitch(Chapter.Cemetery);
            yield return null;
            Assert.IsTrue(playerLight.State.IsOn && controller.playerLightOn);
            var volume = AddBox("Hide Volume", Vector3.up * 0.75f, Vector3.one * 1.5f)
                .gameObject.AddComponent<HideVolume>();
            Physics.SyncTransforms();
            var hiding = player.GetComponent<PlayerHiding>();
            hiding.Hide(volume);
            yield return null;
            Assert.IsFalse(controller.playerLightOn || playerLight.State.IsOn || spot.enabled);
            Assert.IsFalse(playerLight.baseGlow.enabled);
            var properties = new MaterialPropertyBlock();
            playerLight.bodyRenderers[0].GetPropertyBlock(properties);
            Assert.AreEqual(0f, properties.GetFloat("_RimIntensity"));
            float energy = soul.Energy.Current;
            PressLightInput();
            Assert.IsFalse(playerLight.State.Visible || controller.playerLightOn);
            playerLight.Toggle();
            Assert.IsFalse(playerLight.State.IsOn);
            Assert.IsFalse(playerLight.TryBurst());
            Assert.IsFalse(playerLight.State.Visible || controller.playerLightOn);
            Assert.AreEqual(energy, soul.Energy.Current);
            hiding.Leave();
            yield return null;
            Assert.IsFalse(playerLight.State.IsOn || controller.playerLightOn);
            Assert.IsTrue(playerLight.baseGlow.enabled);
            playerLight.bodyRenderers[0].GetPropertyBlock(properties);
            Assert.AreEqual(1.5f, properties.GetFloat("_RimIntensity"));
        }

        [UnityTest]
        public IEnumerator PauseFreezesPartialChargeAndDefersReleaseUntilResumed()
        {
            yield return null;
            playerLight.Toggle();
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: carga");
            GameInput.LightHeldOverride = true;
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(playerLight.Charge.Charging);
            Time.timeScale = 0f;
            float progress = playerLight.Charge.Progress;
            float energy = soul.Energy.Current;
            GameInput.LightHeldOverride = false;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(progress, playerLight.Charge.Progress);
            Assert.AreEqual(energy, soul.Energy.Current);
            Assert.IsFalse(playerLight.State.IsFlashing);
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Destello: cancelado");
            Time.timeScale = 1f;
            yield return null;
            Assert.IsFalse(playerLight.Charge.Charging || playerLight.State.IsFlashing);
            Assert.AreEqual(energy, soul.Energy.Current);
        }

        [UnityTest]
        public IEnumerator PausedLightIgnoresToggleAndFlash([ValueSource(nameof(LightStates))] bool on)
        {
            yield return null;
            if (!on) playerLight.Toggle();
            Time.timeScale = 0f;
            yield return null;
            float energy = soul.Energy.Current;
            PressLightInput();
            yield return null;
            Assert.AreEqual(on, playerLight.State.IsOn);
            Assert.IsFalse(playerLight.State.IsFlashing);
            Assert.AreEqual(energy, soul.Energy.Current);
        }

        private void PressLightInput()
        {
            GameInput.LightHeldOverride = true;
            Assert.IsTrue(GameInput.LightHeld);
            playerLight.SendMessage("Update");
            GameInput.ClearOverrides();
        }

        private BoxCollider AddBox(string name, Vector3 position, Vector3? size = null)
        {
            var box = new GameObject(name);
            box.transform.SetParent(world.transform);
            box.transform.position = position;
            var collider = box.AddComponent<BoxCollider>();
            collider.size = size ?? new Vector3(3f, 0.1f, 6f);
            return collider;
        }

        private void CreateWitch(Chapter chapter = Chapter.Stable)
        {
            surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            var witch = new GameObject("Witch");
            witch.SetActive(false);
            witch.transform.SetParent(world.transform);
            witch.transform.position = new Vector3(0f, 14f, 0f);
            agent = witch.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            controller = witch.AddComponent<WitchController>();
            controller.player = player.transform;
            controller.chapter = chapter;
            controller.skyRoute = new[] { new Vector3(30f, 14f, 0f), new Vector3(30f, 14f, 30f) };
            playerLight.witch = controller;
            witch.SetActive(true);
        }

        private void PlacePlayerNearWitch(float distance, bool facingWitch)
        {
            Vector3 position = controller.transform.position + Vector3.back * distance;
            position.y = 0f;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facingWitch ? 0f : 180f, 0f));
            Physics.SyncTransforms();
        }
    }
}
