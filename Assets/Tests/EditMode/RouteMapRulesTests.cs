using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TiffanyGame.Tests
{
    public sealed class RouteMapRulesTests
    {
        [TestCase("m", true, false, false)]
        [TestCase("l1", true, false, false)]
        [TestCase("select", false, true, false)]
        [TestCase("escape", false, false, true)]
        public void MapBindingsKeepNotebookSeparate(string input, bool map, bool notebook, bool close)
        {
            var devices = new DualSenseInputTests();
            devices.SetUp();
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.Update();
                GameInputTests.Press(input == "m" ? Keyboard.current.mKey : input == "escape" ? Keyboard.current.escapeKey
                    : input == "l1" ? pad.leftShoulder : (ButtonControl)pad.selectButton);
                Assert.That(GameInput.MapPressed, Is.EqualTo(map));
                Assert.That(GameInput.NotebookPressed, Is.EqualTo(notebook));
                Assert.That(GameInput.MapClosePressed, Is.EqualTo(close));
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                devices.TearDown();
            }
        }

        [Test]
        public void EscapeClosesMapAndKeepsGameRunning()
        {
            GameInterface ui = null;
            var devices = new DualSenseInputTests();
            devices.SetUp();
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                ui = (GameInterface)typeof(EditorTools.SceneBuilder).GetMethod("BuildInterface",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { null });
                EditorTools.RouteMapBuilder.Build(ui);
                typeof(GameInterface).GetMethod("Awake", flags).Invoke(ui, null);
                typeof(GameInterface).GetField("skipStory", flags).SetValue(ui, true);
                ui.StartGame();
                typeof(GameInterface).GetField("startFrame", flags).SetValue(ui, -1);
                GameInputTests.Press(Keyboard.current.mKey);
                typeof(GameInterface).GetMethod("Update", flags).Invoke(ui, null);
                Assert.IsTrue(ui.routeMap.paper.activeSelf);
                Assert.IsFalse(ui.IsFrozen);
                GameInputTests.Press(Keyboard.current.escapeKey);
                typeof(GameInterface).GetMethod("Update", flags).Invoke(ui, null);
                Assert.IsFalse(ui.routeMap.paper.activeSelf);
                Assert.That(ui.Screen, Is.EqualTo(InterfaceScreen.Playing));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.IsFalse(AudioListener.pause);
            }
            finally
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
                if (ui != null) Object.DestroyImmediate(ui.gameObject);
                devices.TearDown();
            }
        }

        [Test]
        public void AllClearingsAndPathPointsFitInsideMargins()
        {
            var map = new RouteMapRules();
            for (int i = 0; i < RouteLayout.Count; i++)
            {
                AssertInside(map.Project(RouteLayout.Clearing(i)));
                if (i == RouteLayout.Count - 1) continue;
                foreach (var point in RouteLayout.Path(i)) AssertInside(map.Project(point));
            }
        }

        static void AssertInside(Vector2 point)
        {
            Assert.That(point.x, Is.InRange(RouteMapRules.Margin - 0.0001f, 1f - RouteMapRules.Margin + 0.0001f));
            Assert.That(point.y, Is.InRange(RouteMapRules.Margin - 0.0001f, 1f - RouteMapRules.Margin + 0.0001f));
        }

        [Test]
        public void ProjectionPreservesClearingOrderAndNorthUpWithoutHeight()
        {
            var map = new RouteMapRules();
            for (int i = 1; i < RouteLayout.Count; i++)
            {
                Vector3 a = RouteLayout.Clearing(i - 1), b = RouteLayout.Clearing(i);
                Vector2 delta = map.Project(b) - map.Project(a);
                Assert.That(Mathf.Sign(delta.y), Is.EqualTo(Mathf.Sign(b.z - a.z)));
                Assert.That(delta.x, Is.EqualTo((b.x - a.x) * delta.y / (b.z - a.z)).Within(0.0001f));
                Assert.That(map.Project(b + Vector3.up * 100f), Is.EqualTo(map.Project(b)));
            }
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, -90f)]
        [TestCase(-1f, 0f, 90f)]
        [TestCase(0f, -1f, -180f)]
        public void HeadingMatchesMapDirections(float x, float z, float degrees) =>
            Assert.That(RouteMapRules.Heading(new Vector3(x, 0f, z)), Is.EqualTo(degrees).Within(0.0001f));

        [TestCase(InterfaceScreen.Title)]
        [TestCase(InterfaceScreen.Paused)]
        [TestCase(InterfaceScreen.Notebook)]
        [TestCase(InterfaceScreen.Story)]
        [TestCase(InterfaceScreen.Ending)]
        public void CannotOpenOutsidePlayingAndClosesOnScreenChange(InterfaceScreen screen)
        {
            var map = new RouteMapRules();
            map.Tick(screen, true, false);
            Assert.IsFalse(map.IsOpen);
            map.Tick(InterfaceScreen.Playing, true, false);
            Assert.IsTrue(map.IsOpen);
            map.Tick(screen, true, false);
            Assert.IsFalse(map.IsOpen);
        }

        [Test]
        public void MapInputTogglesAndEscapeClosesWithoutChangingPlayingScreen()
        {
            var map = new RouteMapRules();
            var flow = new InterfaceFlow();
            map.Tick(flow.Screen, true, false);
            Assert.IsTrue(map.IsOpen);
            map.Tick(flow.Screen, false, false);
            Assert.IsTrue(map.IsOpen);
            map.Tick(flow.Screen, true, false);
            Assert.IsFalse(map.IsOpen);
            map.Tick(flow.Screen, true, false);
            map.Tick(flow.Screen, true, true);
            Assert.IsFalse(map.IsOpen);
            Assert.IsFalse(flow.IsFrozen);
        }

        [Test]
        public void EchoPreventsOpeningAndClosesAnOpenMap()
        {
            var map = new RouteMapRules();
            map.Tick(InterfaceScreen.Playing, true, false, true);
            Assert.IsFalse(map.IsOpen);
            map.Tick(InterfaceScreen.Playing, true, false);
            map.Tick(InterfaceScreen.Playing, false, false, true);
            Assert.IsFalse(map.IsOpen);
        }

        [TestCase(-1, true, 0)]
        [TestCase(0, true, 1)]
        [TestCase(6, true, 7)]
        [TestCase(7, false, -1)]
        [TestCase(7, true, -1)]
        [TestCase(2, false, -1)]
        public void NextClearingFollowsTracker(int visited, bool hasNext, int expected) =>
            Assert.That(RouteMapRules.NextClearing(visited, hasNext), Is.EqualTo(expected));
    }
}
