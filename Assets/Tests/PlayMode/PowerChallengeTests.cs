using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
        public IEnumerator EveryRechargeCandleHasACompletePathFromThePreviousClearing()
        {
            yield return new WaitForSeconds(0.6f);
            var path = new NavMeshPath();
            foreach (var challenge in challenges)
            {
                Vector3 origin = challenge.transform.parent.TransformPoint(new Vector3(0f, 0f, -13f));
                Assert.IsTrue(NavMesh.SamplePosition(origin, out var from, 0.5f, NavMesh.AllAreas), challenge.name);
                Assert.IsTrue(NavMesh.SamplePosition(challenge.RechargePoint, out var to,
                    challenge.checkpoint.radius + 0.5f, NavMesh.AllAreas), challenge.name);
                Assert.IsTrue(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path), challenge.name);
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, challenge.name);
                Assert.IsFalse(Physics.Linecast(to.position + Vector3.up * 0.6f,
                    challenge.RechargePoint + Vector3.up * 0.6f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore), challenge.name);
                float firstBarrier = challenge.darkness.Length > 0 ? challenge.darkness[0].transform.localPosition.z : 0f;
                Assert.Less(challenge.checkpoint.transform.localPosition.z + challenge.checkpoint.radius,
                    firstBarrier, challenge.name);
            }
            Assert.IsTrue(new PlaythroughNavigation().Calculate(challenges[2].Exit, challenges[3].RechargePoint),
                "La recarga de la choza debe ser alcanzable al salir del reto del establo.");
        }

        [UnityTest]
        public IEnumerator EnteringAnyUnresolvedChallengeStopsTrailEffectsBeforeWatchStarts()
        {
            var tension = Object.FindAnyObjectByType<TrailTension>();
            Assert.IsNotNull(tension);
            CollectionAssert.AreEquivalent(challenges, tension.challenges);
            foreach (var challenge in challenges)
            {
                float progress = challenge.checkpoint.transform.localPosition.z - 4.9f;
                Teleport(challenge.transform.TransformPoint(new Vector3(10.1f, 0f, progress)), challenge.transform.forward);
                yield return null;
                yield return null;
                Assert.IsFalse(challenge.Underway, challenge.name);
                tension.skyDecoy.gameObject.SetActive(true);
                tension.oneShot.PlayOneShot(tension.skyFire);
                Teleport(challenge.transform.TransformPoint(Vector3.forward * progress), challenge.transform.forward);
                yield return null;
                Assert.IsTrue(challenge.Underway, challenge.name);
                Assert.IsFalse(tension.skyDecoy.gameObject.activeSelf, challenge.name);
                Assert.IsFalse(tension.oneShot.isPlaying, challenge.name);
            }
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
        public IEnumerator WatchSprintReachesStationaryPlayerThroughBuiltPassagesDespiteItsBody()
        {
            foreach (var challenge in new[] { challenges[1], challenges[3] })
            {
                yield return Prepare(challenge);
                if (challenge.Rules.NeedsCandles)
                {
                    soul.Powers.Unlock(Power.Candles);
                    yield return Kindle(challenge);
                }
                var witch = challenge.Witch;
                Assert.IsTrue(witch.GetComponent<CapsuleCollider>().enabled);
                Vector3 post = witch.transform.position;
                Teleport(challenge.transform.TransformPoint(Vector3.forward * 0.2f), challenge.transform.forward);
                bool warned = false, sprinted = false;
                float deadline = Time.time + 3f;
                while (!respawn.Active && Time.time < deadline)
                {
                    if (witch.Decision.State == WitchState.WindUp)
                    {
                        warned = true;
                        Assert.Less(Horizontal(witch.transform.position, post), 0.01f);
                    }
                    if (witch.Decision.State == WitchState.Sprinting) sprinted = true;
                    yield return null;
                }
                Assert.IsTrue(warned && sprinted, "La vigilancia debe avisar inmóvil y después embestir.");
                Assert.IsTrue(respawn.Active, "La embestida debe alcanzar a Tiffany dentro del pasillo.");
                Assert.Greater(Horizontal(witch.transform.position, post), 0.5f,
                    "La bruja debe avanzar sin atascarse en su collider ni en las paredes.");
                Assert.IsFalse(challenge.Rules.Completed);
                yield return Respawn(challenge);
            }
        }

        [UnityTest]
        public IEnumerator LeavingTracksRestoresPatrolAndReturningRestartsWatch() => LeaveAndReturn(challenges[1]);

        [UnityTest]
        public IEnumerator LeavingStableRestoresPatrolAndReturningRestartsWatch() => LeaveAndReturn(challenges[2]);

        [UnityTest]
        public IEnumerator LeavingHutRestoresPatrolAndReturningRestartsWatch() => LeaveAndReturn(challenges[3]);

        [UnityTest]
        public IEnumerator WatchOutranksMainBehaviorsUntilEndWatchRestoresRoaming()
        {
            var challenge = challenges[1];
            var witch = challenge.Witch;
            yield return Prepare(challenge);
            var eager = witch.Profile.Copy();
            eager.SearchEnabled = eager.LastSeenEnabled = eager.AmbushEnabled = eager.CandleWatchEnabled = eager.BarkLureEnabled = true;
            eager.BarkLureMinDelay = eager.BarkLureMaxDelay = eager.AmbushMinDelay = eager.AmbushMaxDelay = 0f;
            eager.RoamMinDelay = eager.RoamMaxDelay = 0f;
            witch.SetProfile(eager);
            witch.NextClearingOverride = challenge.tracker.clearings[3].position;
            Vector3 post = witch.transform.position;
            NoiseBus.Publish(new StepNoise(player.transform.position, 30f));
            yield return HoldPost(witch, challenge, post, 1f);

            var senses = witch.senses;
            float dark = senses.DarkSightDistance, lit = senses.LitSightDistance, smell = senses.SmellDistance;
            senses.DarkSightDistance = senses.LitSightDistance = senses.SmellDistance = 0f;
            Teleport(challenge.transform.TransformPoint(new Vector3(0f, 0f, -2f)), challenge.transform.forward);
            yield return HoldPost(witch, challenge, post, 1.5f);
            (senses.DarkSightDistance, senses.LitSightDistance, senses.SmellDistance) = (dark, lit, smell);

            Teleport(challenge.transform.parent.TransformPoint(new Vector3(0f, 0f, -13f)), -challenge.transform.forward);
            yield return null;
            yield return null;
            Assert.IsNull(witch.WatchPassage);
            Assert.AreEqual(WitchState.Flying, witch.Decision.State);
            var roam = witch.Profile.Copy();
            roam.AmbushEnabled = false;
            roam.RoamMinDelay = roam.RoamMaxDelay = 0f;
            roam.RoamMinDistance = roam.RoamMaxDistance = 10f;
            roam.FarDistance = 0f;
            witch.SetProfile(roam);
            yield return null;
            roam.FarDistance = 1e4f;
            yield return null;
            roam.FarDistance = 0f;
            float deadline = Time.time + 1f;
            while (Mathf.Abs(Horizontal(witch.Decision.MoveTarget, player.transform.position) - 10f) > 0.01f && Time.time < deadline)
                yield return null;
            Assert.AreEqual(WitchState.Flying, witch.Decision.State);
            Assert.AreEqual(10f, Horizontal(witch.Decision.MoveTarget, player.transform.position), 0.01f,
                "Al terminar la vigilancia vuelve a deambular cerca de Tiffany como en main.");
        }

        IEnumerator HoldPost(WitchController witch, PowerChallenge challenge, Vector3 post, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                yield return null;
                Assert.AreSame(challenge.transform, witch.WatchPassage);
                Assert.AreEqual(WitchState.Stalking, witch.Decision.State);
                Assert.AreEqual(WitchIntent.None, witch.Decision.Intent);
                Assert.IsFalse(witch.Decision.VoiceLure);
                Assert.Less(Horizontal(witch.transform.position, post), 0.3f, "La bruja no deja el puesto mientras vigila el reto.");
            }
        }

        static float Horizontal(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

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

        IEnumerator LeaveAndReturn(PowerChallenge challenge)
        {
            var witch = challenge.Witch;
            var animator = witch.figure.GetComponent<Animator>();
            Assert.IsNotNull(animator);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            yield return Prepare(challenge);
            yield return new WaitForSeconds(0.35f);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Stalking"));
            Assert.AreEqual(0f, animator.GetFloat("Speed"), 0.01f);
            if (challenge.Rules.NeedsCandles)
            {
                soul.Powers.Unlock(Power.Candles);
                yield return Kindle(challenge);
            }
            if (challenge.lure != null)
            {
                soul.Powers.Unlock(Power.Poltergeist);
                yield return Distract(challenge);
                Assert.IsTrue(challenge.lure.Used && witch.WatchInvestigating);
            }
            if (challenge.Rules.NeedsVanish)
            {
                soul.Powers.Unlock(Power.Vanish);
                Assert.IsTrue(vanish.TryStart());
            }
            Vector3 previousClearing = challenge.transform.parent.TransformPoint(new Vector3(0f, 0f, -13f));
            Teleport(previousClearing, -challenge.transform.forward);
            yield return null;
            yield return null;

            Assert.IsNull(witch.WatchPassage);
            Assert.AreEqual(WitchState.Flying, witch.Decision.State);
            Assert.AreEqual(WitchForm.Flying, witch.Decision.Form);
            Assert.IsTrue(witch.Decision.FireLit && witch.fireLight.enabled);
            Assert.IsFalse(witch.GetComponent<CapsuleCollider>().enabled);
            Assert.IsFalse(witch.WatchInvestigating || witch.WatchPassageClear || respawn.Active);
            Assert.IsFalse(challenge.Rules.Completed || challenge.Rules.Crossing || vanish.State.IsActive);
            Assert.AreEqual(0, challenge.Rules.LitMask);
            foreach (var candle in challenge.candles) Assert.IsFalse(candle.Candle.IsLit);
            foreach (var gate in challenge.darkness) Assert.IsTrue(gate.activeSelf);
            if (challenge.lure != null) Assert.IsFalse(challenge.lure.Used);
            Vector3 patrolPosition = witch.transform.position;
            yield return new WaitForSeconds(0.1f);
            Assert.Greater(Vector3.Distance(patrolPosition, witch.transform.position), 0.1f);
            Assert.IsNull(witch.WatchPassage);

            yield return Prepare(challenge);
            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(WitchState.Stalking, witch.Decision.State);
            Assert.IsTrue(witch.figure.activeInHierarchy);
            Assert.IsFalse(witch.fireLight.enabled);
            Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("Stalking"));
            Assert.AreEqual(0f, animator.GetFloat("Speed"), 0.01f);
            Assert.AreEqual(challenge.Rules.NeedsDistraction, witch.GetComponent<CapsuleCollider>().enabled);
            Assert.IsFalse(challenge.Rules.Completed);
        }

        IEnumerator Prepare(PowerChallenge challenge)
        {
            Teleport(challenge.RechargePoint, challenge.transform.forward);
            yield return null;
            yield return null;
            if (challenge.kind != ChallengeKind.Candles) Assert.AreSame(challenge.transform, challenge.Witch.WatchPassage);
            Assert.IsTrue(challenge.Underway);
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
            bool warned = false, sprinted = false;
            float deadline = Time.time + 3f;
            while (!respawn.Active && Time.time < deadline)
            {
                warned |= challenge.Witch.Decision.State == WitchState.WindUp;
                sprinted |= challenge.Witch.Decision.State == WitchState.Sprinting;
                yield return null;
            }
            GameInput.MoveOverride = Vector2.zero;
            Assert.IsTrue(warned, "La bruja debe pasar por el aviso antes de atrapar a Tiffany.");
            Assert.IsTrue(sprinted, "La vigilancia solo atrapa durante la embestida.");
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
            Assert.IsFalse(challenge.Underway, "El reto deja de estar en curso en el cuadro en que se resuelve.");
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
            float ground = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider.TryGetComponent<GroundSurface>(out _)) ground = Mathf.Max(ground, hit.point.y);
            Assert.IsFalse(float.IsNegativeInfinity(ground), "El destino del test debe tener suelo.");
            position.y = ground + 0.02f;
            player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up)));
            character.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
