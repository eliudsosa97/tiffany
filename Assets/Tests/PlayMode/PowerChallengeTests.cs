using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class PowerChallengeTests
    {
        PowerChallenge[] challenges;
        PlayerController player;
        PlayerSoul soul;
        PlayerRespawn respawn;
        CandleKindler kindler;
        VanishPower vanish;
        PlayerLight light;
        CharacterController character;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            GameInput.ClearOverrides();
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<GameInterface>().StartGame();
            foreach (var heartbeat in Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None)) heartbeat.enabled = false;
            Object.FindAnyObjectByType<Storm>().raining = false;
            challenges = Object.FindObjectsByType<PowerChallenge>(FindObjectsSortMode.None);
            System.Array.Sort(challenges, (a, b) => a.kind.CompareTo(b.kind));
            Assert.AreEqual(4, challenges.Length, "Regenera Main con ChallengeBuilder antes de correr PlayMode.");
            player = Object.FindAnyObjectByType<PlayerController>();
            soul = player.GetComponent<PlayerSoul>();
            respawn = player.GetComponent<PlayerRespawn>();
            kindler = player.GetComponent<CandleKindler>();
            vanish = player.GetComponent<VanishPower>();
            light = player.GetComponent<PlayerLight>();
            character = player.GetComponent<CharacterController>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            SceneManager.CreateScene(nameof(PowerChallengeTests));
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Main"));
        }

        [UnityTest]
        public IEnumerator DarknessBlocksWithoutCandlesAndCaptureRestoresARechargeableAttempt()
        {
            var challenge = challenges[0];
            yield return Prepare(challenge);
            Teleport(challenge.transform.TransformPoint(new Vector3(0f, 0f, -5f)), challenge.transform.forward);
            Assert.IsFalse(kindler.TryKindle());
            character.Move(challenge.transform.forward * 3f);
            Assert.Less(challenge.transform.InverseTransformPoint(player.transform.position).z, -4f);
            Assert.IsTrue(challenge.darkness[0].activeSelf);
            Assert.IsFalse(player.GetComponent<MemoryCollector>().Collect(3));
            soul.Powers.Unlock(Power.Candles);
            yield return Kindle(challenge);
            Assert.IsFalse(challenge.darkness[0].activeSelf || challenge.darkness[1].activeSelf);
            respawn.Trigger();
            yield return Respawn(challenge);
            Assert.IsTrue(challenge.darkness[0].activeSelf && challenge.darkness[1].activeSelf);
            Assert.IsFalse(player.GetComponent<MemoryCollector>().Collect(5));
            foreach (var candle in challenge.candles) Assert.IsFalse(candle.Candle.IsLit);
            soul.Energy.ResetTo(0f);
            yield return Recharge(challenge);
            yield return Kindle(challenge);
            Teleport(challenge.Entry, challenge.transform.forward);
            yield return null;
            yield return Cross(challenge);
            Assert.IsTrue(challenge.Rules.Completed);
        }

        [UnityTest]
        public IEnumerator TracksDetectWithoutDistractionAndOpenAfterTheActualObjectNoise()
        {
            var challenge = challenges[1];
            yield return Prepare(challenge);
            Teleport(challenge.Entry, challenge.transform.forward);
            yield return WalkUntilCaught(challenge);
            yield return Respawn(challenge);
            soul.Powers.Unlock(Power.Poltergeist);
            yield return Recharge(challenge);
            yield return Distract(challenge);
            yield return Cross(challenge);
            Assert.IsTrue(challenge.Rules.Completed);
        }

        [UnityTest]
        public IEnumerator StableDetectsVisiblePlayerAndAllowsWalkingWithinThreeSecondsOfVanish()
        {
            var challenge = challenges[2];
            yield return Prepare(challenge);
            Teleport(challenge.Entry, challenge.transform.forward);
            yield return WalkUntilCaught(challenge);
            yield return Respawn(challenge);
            soul.Powers.Unlock(Power.Vanish);
            yield return Recharge(challenge);
            Teleport(challenge.Entry, challenge.transform.forward);
            yield return null;
            Assert.IsTrue(vanish.TryStart());
            float start = Time.time;
            yield return Cross(challenge);
            Assert.IsTrue(challenge.Rules.Completed);
            Assert.Less(Time.time - start, Vanish.Duration);
        }

        [UnityTest]
        public IEnumerator HutRequiresCandlesDistractionAndVanishAndCanBeRetried()
        {
            var challenge = challenges[3];
            yield return Prepare(challenge);
            soul.Powers.Unlock(Power.Candles);
            soul.Powers.Unlock(Power.Poltergeist);
            soul.Powers.Unlock(Power.Vanish);
            Assert.IsTrue(challenge.darkness[0].activeSelf && challenge.darkness[1].activeSelf);
            yield return Kindle(challenge);
            Teleport(challenge.Entry, challenge.transform.forward);
            yield return null;
            Assert.IsTrue(vanish.TryStart());
            Move(challenge);
            yield return new WaitForSeconds(2.5f);
            GameInput.MoveOverride = Vector2.zero;
            Assert.IsFalse(challenge.Rules.Completed, "Desvanecerse no atraviesa el cuerpo de la bruja en el puesto.");
            Assert.Less(challenge.transform.InverseTransformPoint(player.transform.position).z, ChallengeRules.PassageLength);
            respawn.Trigger();
            yield return Respawn(challenge);
            yield return Recharge(challenge);
            yield return Kindle(challenge);
            yield return Distract(challenge);
            Teleport(challenge.transform.TransformPoint(Vector3.forward * 0.2f), challenge.transform.forward);
            yield return null;
            yield return null;
            Assert.Less(Vector3.Distance(player.transform.position, challenge.Witch.Decision.MoveTarget), 0.1f,
                "En la choza todavía ve el paso después de ir a revisar el señuelo.");
            Assert.IsFalse(challenge.Rules.Completed);
            Teleport(challenge.Entry, challenge.transform.forward);
            yield return null;
            Assert.IsTrue(vanish.TryStart());
            yield return Cross(challenge);
            Assert.IsTrue(challenge.Rules.Completed);
        }

        [UnityTest]
        public IEnumerator AThunderMaskedLureIsReusableAtCheckpointAndCaptureRestoresItsPhysics()
        {
            var challenge = challenges[1];
            yield return Prepare(challenge);
            soul.Powers.Unlock(Power.Poltergeist);
            var storm = Object.FindAnyObjectByType<Storm>();
            storm.enabled = false;
            challenge.Witch.hearingMuted = true;
            Vector3 original = challenge.lure.transform.position;
            yield return MoveLure(challenge);
            Assert.IsFalse(challenge.Witch.WatchInvestigating);
            yield return Recharge(challenge);
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(challenge.lure.Used);
            Assert.Less(Vector3.Distance(original, challenge.lure.transform.position), 0.001f);
            challenge.Witch.hearingMuted = false;
            yield return Distract(challenge);
            respawn.Trigger();
            yield return Respawn(challenge);
            Assert.IsFalse(challenge.lure.Used);
            Assert.IsTrue(challenge.lure.GetComponent<Rigidbody>().isKinematic);
            Assert.Less(Vector3.Distance(original, challenge.lure.transform.position), 0.001f);
            yield return Recharge(challenge);
            yield return Distract(challenge);
            yield return Cross(challenge);
            Assert.IsTrue(challenge.Rules.Completed);
        }

        [UnityTest]
        public IEnumerator CapturePreviewRestoresRulesPropsEnergyPowersCheckpointAndWitch()
        {
            var challenge = challenges[3];
            yield return Prepare(challenge);
            var stage = System.Array.Find(challenge.GetComponentsInChildren<ChallengeCaptureStage>(), item => item.after);
            var witch = challenge.Witch;
            Vector3 witchPosition = witch.transform.position, propPosition = challenge.lure.transform.position;
            Vector3 checkpoint = soul.Respawn.Position;
            int powers = soul.Powers.Count;
            float energy = soul.Energy.Current;
            WitchState state = witch.Decision.State;
            witch.gameObject.SetActive(false);
            stage.Apply();
            yield return null;
            yield return null;
            Assert.IsFalse(challenge.darkness[0].activeSelf);
            Assert.IsTrue(witch.gameObject.activeSelf);
            stage.Clear();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, challenge.Rules.LitMask);
            Assert.IsFalse(challenge.Rules.Completed || challenge.lure.Used);
            Assert.IsTrue(challenge.darkness[0].activeSelf && challenge.darkness[1].activeSelf);
            Assert.IsTrue(challenge.enabled && witch.enabled);
            Assert.IsFalse(witch.gameObject.activeSelf);
            Assert.AreEqual(state, witch.Decision.State);
            Assert.Less(Vector3.Distance(witchPosition, witch.transform.position), 0.001f);
            Assert.Less(Vector3.Distance(propPosition, challenge.lure.transform.position), 0.001f);
            Assert.AreEqual(checkpoint, soul.Respawn.Position);
            Assert.AreEqual(powers, soul.Powers.Count);
            Assert.AreEqual(energy, soul.Energy.Current);
            foreach (var candle in challenge.candles) Assert.IsFalse(candle.Candle.IsLit || candle.flame.enabled);
        }

        IEnumerator Prepare(PowerChallenge challenge)
        {
            Teleport(challenge.RechargePoint, challenge.transform.forward);
            yield return null;
            yield return null;
            if (challenge.kind != ChallengeKind.Candles) Assert.AreSame(challenge.transform, challenge.Witch.WatchPassage);
            Assert.AreEqual(challenge.checkpoint.transform.position, soul.Respawn.Position);
        }

        IEnumerator Recharge(PowerChallenge challenge)
        {
            Teleport(challenge.RechargePoint, challenge.transform.forward);
            float deadline = Time.time + 6f;
            do { yield return null; } while (soul.Energy.Current < 99f && Time.time < deadline);
            Assert.GreaterOrEqual(soul.Energy.Current, 99f);
        }

        IEnumerator Kindle(PowerChallenge challenge)
        {
            for (int i = 0; i < challenge.candles.Length; i++)
            {
                Teleport(challenge.candles[i].transform.position + challenge.transform.right - challenge.transform.forward * 0.4f,
                    challenge.transform.forward);
                Assert.IsTrue(kindler.TryKindle());
                yield return null;
                Assert.IsTrue(challenge.Rules.GateOpen(i));
            }
        }

        IEnumerator MoveLure(PowerChallenge challenge)
        {
            Teleport(challenge.Entry, challenge.lure.transform.position - challenge.Entry);
            if (!light.State.IsOn) light.Toggle();
            yield return null;
            yield return null;
            Assert.IsTrue(player.GetComponent<Poltergeist>().TryMove());
            yield return null;
        }

        IEnumerator Distract(PowerChallenge challenge)
        {
            yield return MoveLure(challenge);
            float deadline = Time.time + 8f;
            while (!challenge.Distracted && Time.time < deadline) yield return null;
            Assert.IsTrue(challenge.Distracted, "La campana debe sacar a la bruja del paso por el NavMesh.");
            if (light.State.IsOn) light.Toggle();
            player.transform.rotation = challenge.transform.rotation;
        }

        IEnumerator Respawn(PowerChallenge challenge)
        {
            float deadline = Time.time + 6f;
            while (respawn.Active && Time.time < deadline) yield return null;
            Assert.IsFalse(respawn.Active);
            Assert.IsFalse(challenge.Rules.Completed || vanish.State.IsActive);
            Assert.Less(Vector3.Distance(player.transform.position, challenge.checkpoint.transform.position), 1.6f);
            yield return null;
        }

        IEnumerator WalkUntilCaught(PowerChallenge challenge)
        {
            Move(challenge);
            float deadline = Time.time + 3f;
            while (!respawn.Active && Time.time < deadline) yield return null;
            GameInput.MoveOverride = Vector2.zero;
            Assert.IsTrue(respawn.Active, "La bruja debe atrapar a Tiffany al intentar pasar visible por el puesto.");
            Assert.IsFalse(challenge.Rules.Completed);
        }

        IEnumerator Cross(PowerChallenge challenge)
        {
            Move(challenge);
            float deadline = Time.time + 3f;
            while (!challenge.Rules.Completed && !respawn.Active && Time.time < deadline) yield return null;
            GameInput.MoveOverride = Vector2.zero;
            Assert.IsFalse(respawn.Active);
        }

        void Move(PowerChallenge challenge)
        {
            player.transform.rotation = challenge.transform.rotation;
            GameInput.RunOverride = GameInput.SlowOverride = false;
            GameInput.MoveOverride = PlaythroughNavigation.CameraInput(challenge.transform.forward, player.viewCamera.transform.eulerAngles.y);
        }

        void Teleport(Vector3 position, Vector3 forward)
        {
            GameInput.MoveOverride = Vector2.zero;
            character.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up)));
            character.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
