using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TiffanyGame.Tests
{
    public sealed class PlayerRespawnTests
    {
        private static readonly bool[] CandleCases = { true, false };
        private GameObject world;
        private PlayerSoul soul;
        private PlayerController movement;
        private PlayerHiding hiding;
        private CharacterController character;
        private PlayerRespawn respawn;
        private PlayerLight light;
        private CandleSpot candle;
        private Collider glass;

        [SetUp]
        public void SetUp()
        {
            SafeZones.Clear();
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = false;
            GameInput.InteractOverride = false;
            world = new GameObject("RespawnTestWorld");
            world.SetActive(false);
            var ground = new GameObject("Ground");
            ground.transform.SetParent(world.transform);
            ground.transform.position = Vector3.down * 0.5f;
            ground.AddComponent<BoxCollider>().size = new Vector3(80f, 1f, 80f);
            var player = new GameObject("Tiffany") { tag = "Player" };
            player.transform.SetParent(world.transform);
            player.transform.position = Vector3.left * 12f;
            soul = player.AddComponent<PlayerSoul>();
            soul.startingPowers = new[] { Power.Light };
            character = player.AddComponent<CharacterController>();
            character.height = 1.2f;
            character.radius = 0.25f;
            character.center = Vector3.up * 0.6f;
            movement = player.AddComponent<PlayerController>();
            var body = new GameObject("Body");
            body.transform.SetParent(player.transform, false);
            body.AddComponent<MeshRenderer>();
            hiding = player.AddComponent<PlayerHiding>();
            hiding.body = PlayerHiding.BodyRenderers(player);
            light = player.AddComponent<PlayerLight>();
            light.spot = new GameObject("Soul Light").AddComponent<Light>();
            light.spot.transform.SetParent(player.transform, false);
            light.startOn = true;
            var witch = new GameObject("Witch");
            witch.transform.SetParent(world.transform);
            witch.transform.position = Vector3.up * 14f;
            witch.AddComponent<NavMeshAgent>().enabled = false;
            var controller = witch.AddComponent<WitchController>();
            controller.player = player.transform;
            controller.playerVanished = true;
            controller.skyRoute = new[] { new Vector3(-30f, 14f, -30f), new Vector3(30f, 14f, 30f) };
            light.witch = controller;
            respawn = player.AddComponent<PlayerRespawn>();
            respawn.witch = controller;
            respawn.overlay = new GameObject("Caught Fade", typeof(RectTransform)).AddComponent<Image>();
            respawn.overlay.transform.SetParent(world.transform);
            var candleObject = new GameObject("Family Candle 1");
            candleObject.transform.SetParent(world.transform);
            candleObject.transform.position = Vector3.right * 6f;
            candle = candleObject.AddComponent<CandleSpot>();
            candle.player = soul;
            var vessel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vessel.transform.SetParent(candle.transform, false);
            vessel.transform.localPosition = Vector3.up * 0.11f;
            vessel.transform.localScale = new Vector3(0.14f, 0.11f, 0.14f);
            glass = vessel.GetComponent<CapsuleCollider>();
            world.SetActive(true);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            Object.Destroy(world);
            yield return null;
            SafeZones.Clear();
        }

        [UnityTest]
        public IEnumerator RespawnKeepsSpawnWhenPlayerMovesBeforeFirstAccess()
        {
            var player = new GameObject("Spawn Test");
            player.transform.SetParent(world.transform);
            Vector3 spawn = new Vector3(3f, 2f, 4f);
            player.transform.position = spawn;
            var playerSoul = player.AddComponent<PlayerSoul>();
            player.transform.position = new Vector3(15f, 0f, 8f);

            Assert.AreEqual(spawn, playerSoul.Respawn.Position);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CaughtReturnsToCandleOrSpawnWithHalfEnergyAndUnlocksMovement(
            [ValueSource(nameof(CandleCases))] bool touchCandle)
        {
            yield return null;
            Vector3 destination = soul.Respawn.Position;
            if (touchCandle)
            {
                PlacePlayer(candle.transform.position + Vector3.back);
                yield return null;
                yield return null;
                destination = candle.transform.position;
                Assert.AreEqual(destination, soul.Respawn.Position);
            }
            PlacePlayer(new Vector3(15f, 0f, 8f));
            yield return null;
            soul.Energy.ResetTo(80f);
            Vector3 caughtPosition = soul.transform.position;
            GameInput.MoveOverride = Vector2.right;
            respawn.Trigger();
            Assert.IsFalse(movement.enabled);
            yield return null;
            Assert.AreEqual(caughtPosition, soul.transform.position);
            Assert.IsTrue(respawn.overlay.gameObject.activeSelf);
            float deadline = Time.time + 4f;
            while (respawn.Active && Time.time < deadline)
            {
                Assert.IsFalse(movement.enabled);
                yield return null;
            }
            Assert.IsFalse(respawn.Active);
            Assert.IsTrue(movement.enabled && character.enabled);
            Vector3 offset = soul.transform.position - destination;
            offset.y = 0f;
            Assert.Less(offset.magnitude, 1.5f);
            Assert.AreEqual(Vector3.back * respawn.sideOffset, offset);
            Assert.AreEqual(0f, soul.transform.position.y, 0.01f);
            if (touchCandle) Assert.IsTrue(SafeZones.Contains(soul.transform.position));
            Assert.That(soul.Energy.Current, Is.InRange(50f,
                50f + SoulCosts.RechargePerSecond * CaughtSequence.FadeInDuration + 1f));
            Assert.IsFalse(light.State.IsOn);
            Assert.AreNotEqual(WitchState.Caught, respawn.witch.Decision.State);
            Assert.IsFalse(respawn.overlay.gameObject.activeSelf);
            Vector3 center = soul.transform.TransformPoint(character.center);
            Vector3 half = Vector3.up * (character.height * 0.5f - character.radius);
            CollectionAssert.DoesNotContain(Physics.OverlapCapsule(center - half, center + half, character.radius), glass);
            GameInput.MoveOverride = Vector2.zero;
            yield return null;
            if (touchCandle) Assert.Greater(soul.Energy.Current, 50f);
        }

        [UnityTest]
        public IEnumerator CaughtWhileHidingReturnsVisibleToCandleAndUnlocksControls()
        {
            yield return null;
            PlacePlayer(candle.transform.position + Vector3.back);
            yield return null;
            yield return null;
            Assert.AreEqual(candle.transform.position, soul.Respawn.Position);
            PlacePlayer(new Vector3(15f, 0f, 8f));
            var bush = new GameObject("Hide Test");
            bush.transform.SetParent(world.transform);
            bush.transform.position = soul.transform.position + Vector3.forward;
            bush.layer = LayerMask.NameToLayer("Ignore Raycast");
            var collider = bush.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.6f, 1.8f, 1.6f);
            collider.center = Vector3.up * 0.9f;
            var volume = bush.AddComponent<HideVolume>();
            Physics.SyncTransforms();

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Escondida");
            hiding.Hide(volume);
            Assert.IsTrue(hiding.Hidden);
            foreach (var renderer in hiding.body) Assert.IsFalse(renderer.enabled);

            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Fuera del escondite");
            respawn.Trigger();
            Assert.IsFalse(movement.enabled);
            Assert.IsFalse(hiding.enabled);
            float deadline = Time.time + 4f;
            while (respawn.Active && Time.time < deadline)
            {
                Assert.IsFalse(movement.enabled);
                Assert.IsFalse(hiding.enabled);
                yield return null;
            }
            Assert.IsFalse(respawn.Active);
            Assert.IsFalse(hiding.Hidden);
            foreach (var renderer in hiding.body) Assert.IsTrue(renderer.enabled);
            Assert.IsTrue(hiding.enabled && movement.enabled && character.enabled);
            Vector3 destination = candle.transform.position + Vector3.back * respawn.sideOffset;
            Assert.Less(Vector3.Distance(destination, soul.transform.position), 0.01f);
        }

        private void PlacePlayer(Vector3 position)
        {
            character.enabled = false;
            soul.transform.position = position;
            Physics.SyncTransforms();
            character.enabled = true;
        }

        [UnityTest]
        public IEnumerator ReturningToTheCheckpointEndsVanish()
        {
            var vanish = soul.gameObject.AddComponent<VanishPower>();
            vanish.witch = respawn.witch;
            soul.Powers.Unlock(Power.Vanish);
            yield return null;
            Assert.IsTrue(vanish.TryStart());
            respawn.ReturnToCheckpoint();
            yield return null;
            Assert.IsFalse(vanish.State.IsActive);
            Assert.IsFalse(respawn.witch.playerVanished);
        }
    }
}
