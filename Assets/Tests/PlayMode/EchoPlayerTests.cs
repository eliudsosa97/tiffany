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
            player.props = new[]
            {
                Child("Ball", player.stageRoot.transform).transform,
                Child("Ribbon", player.girl).transform
            };
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
                props = new[]
                {
                    new EchoProp { type = "ball", position = new Vector3(1f, 0.2f, 1f), disappear = 10f },
                    new EchoProp { type = "ribbon", parent = "girl", position = new Vector3(0.2f, 0.7f, 0f), disappear = 10f }
                },
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

        [UnityTest] public IEnumerator MovingFiguresAndWorldPropsFollowTheGroundWhileHeldPropsKeepTheirOffsets()
        {
            root.transform.SetPositionAndRotation(new Vector3(108f, 7f, 168f), Quaternion.Euler(0f, 35f, 0f));
            var ground = Child("Ground", root.transform, typeof(BoxCollider));
            ground.transform.localPosition = Vector3.down;
            ground.transform.localRotation = Quaternion.Euler(8f, 0f, -6f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            player.ground = ground.GetComponent<Collider>();
            var roof = Child("Roof", root.transform, typeof(BoxCollider));
            roof.transform.localPosition = Vector3.up * 3f;
            roof.transform.localScale = new Vector3(40f, 0.5f, 40f);
            Physics.SyncTransforms();
            var surface = new Plane(ground.transform.up, ground.transform.TransformPoint(Vector3.up * 0.5f));
            Assert.IsTrue(player.Play());
            for (int i = 0; i < 2; i++)
            {
                AssertOnGround(player.girl, EchoEvaluator.Pose(player.Data.girl, player.Elapsed).Position.y, surface);
                AssertOnGround(player.tyler, 0f, surface);
                AssertOnGround(player.props[0], player.Data.props[0].position.y, surface);
                Assert.AreEqual(player.Data.props[1].position, player.props[1].localPosition);
                yield return new WaitForSeconds(0.2f);
            }
            Assert.Greater(player.girl.localPosition.x, 0f);
        }

        static void AssertOnGround(Transform target, float offset, Plane surface)
        {
            Vector3 position = target.position;
            float height = -(surface.normal.x * position.x + surface.normal.z * position.z + surface.distance) / surface.normal.y;
            Assert.AreEqual(height + offset, position.y, 0.001f, target.name);
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
