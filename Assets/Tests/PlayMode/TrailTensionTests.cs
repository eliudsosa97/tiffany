using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace TiffanyGame.Tests
{
    public sealed class TrailTensionTests
    {
        private GameObject root;
        private TrailTension tension;
        private int events;
        [SetUp] public void SetUp()
        {
            (Time.timeScale, events) = (1f, 0);
            root = new GameObject("Trail Test");
            tension = root.AddComponent<TrailTension>();
            tension.tracker = root.AddComponent<WitchChapterTracker>();
            tension.tracker.player = Child("Player");
            tension.tracker.clearings = new[] { Child("First Clearing"), Child("Second Clearing") };
            tension.tracker.clearings[0].position = new Vector3(-50f, 100f, 0f);
            tension.tracker.clearings[1].position = Vector3.right * 50f;
            Application.logMessageReceived += CountEvents;
        }
        private Transform Child(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
            return child;
        }
        private void CountEvents(string message, string stack, LogType type)
        {
            if (message.StartsWith(GameInfo.LogPrefix + " Sendero:")) events++;
        }
        [UnityTest] public IEnumerator FirstStepsFireWithinElevenSecondsBetweenClearings()
        {
            LogAssert.Expect(LogType.Log, $"{GameInfo.LogPrefix} Sendero: PhantomSteps");
            yield return new WaitForSeconds(11f);
            Assert.AreEqual(1, events);
        }
        [UnityTest] public IEnumerator WitchTenMetersAwayBlocksEvents()
        {
            tension.witch = Child("Witch").gameObject.AddComponent<WitchController>();
            tension.witch.enabled = false;
            tension.witch.transform.position = new Vector3(10f, 100f, 0f);
            yield return new WaitForSeconds(11f);
            Assert.AreEqual(0, events);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            Application.logMessageReceived -= CountEvents;
            Time.timeScale = 1f;
            Object.Destroy(root);
            yield return null;
        }
    }
}
