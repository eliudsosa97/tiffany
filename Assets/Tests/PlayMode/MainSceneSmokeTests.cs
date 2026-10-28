using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public class MainSceneSmokeTests
    {
        Scene mainScene;
        List<StepNoise> steps;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            steps = new List<StepNoise>();
            GameInput.MoveOverride = Vector2.zero;
            GameInput.SlowOverride = false;
            GameInput.RunOverride = false;
            GameInput.AimOverride = Vector2.zero;

            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            mainScene = SceneManager.GetActiveScene();
            NoiseBus.Published += RecordStep;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoiseBus.Published -= RecordStep;
            GameInput.ClearOverrides();
            SceneManager.CreateScene(nameof(MainSceneSmokeTests));
            yield return SceneManager.UnloadSceneAsync(mainScene);
        }

        [UnityTest]
        public IEnumerator MainSceneHasPlayerCameraAndSixGrounds()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.GetComponent<PlayerController>());
            Assert.IsNotNull(player.GetComponent<CharacterController>());

            var viewCamera = Camera.main;
            Assert.IsNotNull(viewCamera);
            var follow = viewCamera.GetComponent<FollowCamera>();
            Assert.IsNotNull(follow);
            Assert.AreSame(player.transform, follow.target);

            var surfaces = Object.FindObjectsByType<GroundSurface>(FindObjectsSortMode.None);
            foreach (GroundType type in System.Enum.GetValues(typeof(GroundType)))
            {
                int count = 0;
                foreach (var surface in surfaces)
                    if (surface.type == type) count++;
                Assert.AreEqual(1, count, $"Debe haber un suelo de tipo {type}.");
            }

            Assert.AreEqual(6, Object.FindObjectsByType<CaptureStop>(FindObjectsSortMode.None).Length);

            yield return null;

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WalkingInMainScenePublishesStepsAndCameraFollows()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            Assert.IsNotNull(player);
            var viewCamera = Camera.main;
            Assert.IsNotNull(viewCamera);
            var initialPlayerPosition = player.transform.position;
            var initialCameraPosition = viewCamera.transform.position;
            float initialDistance = HorizontalDistance(initialCameraPosition, initialPlayerPosition);

            GameInput.MoveOverride = Vector2.up;

            yield return new WaitForSeconds(2f);

            Assert.Greater(HorizontalDistance(player.transform.position, initialPlayerPosition), 2f);
            Assert.That(steps.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var step in steps) Assert.Greater(step.Radius, 0f);
            Assert.Greater(HorizontalDistance(viewCamera.transform.position, initialCameraPosition), 0f);
            Assert.That(HorizontalDistance(viewCamera.transform.position, player.transform.position),
                Is.EqualTo(initialDistance).Within(1f));
            LogAssert.NoUnexpectedReceived();
        }

        void RecordStep(StepNoise noise)
        {
            steps.Add(noise);
        }

        static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            var displacement = first - second;
            displacement.y = 0f;
            return displacement.magnitude;
        }
    }
}
