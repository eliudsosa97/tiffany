using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TiffanyGame.Tests
{
    public sealed class EchoPlayerTests
    {
        GameObject root;
        GameInterface ui;
        EchoPlayer player;
        TextAsset asset;

        [SetUp] public void SetUp()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.ClearOverrides();
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.SlowOverride = GameInput.RunOverride = GameInput.InteractOverride = false;
            root = new GameObject("Echo Test");
            root.SetActive(false);
            var canvas = Child("Interface", root.transform, typeof(RectTransform), typeof(Canvas));
            ui = canvas.AddComponent<GameInterface>();
            ui.pausePanel = Child("Pause", canvas.transform);
            ui.notebookPanel = Child("Notebook", canvas.transform);
            ui.noticeText = Text("Notice", canvas.transform);
            ui.subtitleText = Text("Subtitle", canvas.transform);
            var owner = Child("Echo", root.transform);
            player = owner.AddComponent<EchoPlayer>();
            player.gameInterface = ui;
            player.blackout = Child("Blackout", canvas.transform, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            player.stageRoot = Child("Stage", owner.transform);
            player.girl = Child("Girl", player.stageRoot.transform).transform;
            player.tyler = Child("Tyler", player.stageRoot.transform).transform;
            var data = new EchoData
            {
                id = 1,
                duration = 10f,
                girl = new EchoTrack
                {
                    disappear = 10f,
                    points = new[] { new EchoPoint(), new EchoPoint { time = 10f, position = Vector3.right * 10f } }
                },
                tyler = new EchoTrack { disappear = 10f },
                subtitles = new[] { new EchoSubtitle { time = 0.1f, duration = 8f, text = "¡Tyler!" } },
                sounds = Array.Empty<EchoSound>(),
                props = Array.Empty<EchoProp>(),
                blackout = new EchoBlackout { time = 1f, duration = 0.1f }
            };
            asset = new TextAsset(JsonUtility.ToJson(data)) { name = "test-echo" };
            player.echoAsset = asset;
            root.SetActive(true);
            ui.ReplaceSubtitle("Anterior", 20f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(asset);
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameInput.ClearOverrides();
            yield return null;
        }

        [UnityTest] public IEnumerator PlaysMovesSubtitlesBlackoutAndHidesEverythingAtEnd()
        {
            Assert.IsFalse(player.stageRoot.activeSelf);
            Assert.IsTrue(player.Play());
            Assert.IsTrue(player.stageRoot.activeSelf);
            Assert.IsEmpty(ui.subtitleText.text);
            Assert.IsFalse(ui.noticeText.gameObject.activeSelf);
            Time.timeScale = 10f;
            yield return new WaitForSeconds(2f);
            Assert.Greater(player.girl.localPosition.x, 0f);
            Assert.AreEqual("¡Tyler!", ui.subtitleText.text);
            Assert.IsTrue(player.blackout.gameObject.activeSelf);
            Assert.AreEqual(1f, player.blackout.color.a);
            yield return new WaitForSeconds(9f);
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(player.stageRoot.activeSelf);
            Assert.IsFalse(player.girl.gameObject.activeInHierarchy);
            Assert.IsFalse(player.tyler.gameObject.activeInHierarchy);
            Assert.IsFalse(player.blackout.gameObject.activeSelf);
            Assert.AreEqual("Anterior", ui.subtitleText.text);
            Assert.IsTrue(ui.noticeText.gameObject.activeSelf);
        }

        [UnityTest] public IEnumerator PauseFreezesTheEchoAndDisableCleansUpImmediately()
        {
            Assert.IsTrue(player.Play());
            yield return new WaitForSeconds(0.2f);
            ui.TogglePause();
            float time = player.Elapsed;
            Vector3 position = player.girl.localPosition;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(time, player.Elapsed);
            Assert.AreEqual(position, player.girl.localPosition);
            ui.TogglePause();
            player.enabled = false;
            Assert.IsFalse(player.IsPlaying);
            Assert.IsFalse(player.stageRoot.activeSelf);
            Assert.IsFalse(player.blackout.gameObject.activeSelf);
            Assert.AreEqual("Anterior", ui.subtitleText.text);
        }

        static GameObject Child(string name, Transform parent, params Type[] components)
        {
            var child = new GameObject(name, components);
            child.transform.SetParent(parent, false);
            return child;
        }

        static Text Text(string name, Transform parent)
        {
            var text = Child(name, parent, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return text;
        }
    }
}
