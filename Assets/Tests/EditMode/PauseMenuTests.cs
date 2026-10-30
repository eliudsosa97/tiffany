using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TiffanyGame.Tests
{
    public sealed class PauseMenuTests
    {
        static void Call(Component target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(target, null);

        [Test]
        public void PauseTableKeepsColumnsAndControllerFallbackAfterStart()
        {
            GameInterface ui = null;
            try
            {
                ui = (GameInterface)typeof(EditorTools.SceneBuilder).GetMethod("BuildInterface", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { null });
                Call(ui, "Start");
                var table = ui.pausePanel.transform.Find("Controls");
                var background = table.GetComponent<UnityEngine.UI.Image>();
                Assert.AreEqual(new Color(0f, 0f, 0f, 0.6f), background.color);
                Assert.IsNull(background.sprite);
                var columns = table.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                Assert.AreEqual(3, columns.Length);
                var headers = new[] { "Acción", "Teclado", "Mando" };
                var values = new[] { InterfaceTexts.PauseActions, InterfaceTexts.PauseKeyboard, InterfaceTexts.PauseController };
                for (int i = 0; i < columns.Length; i++)
                {
                    string expected = "<color=#999999>" + headers[i] + "</color>\n" + values[i];
                    Assert.AreEqual(InterfaceTexts.ControllerText(expected, GameInterface.HasControllerSymbols(columns[i].font)), columns[i].text);
                    Assert.AreEqual(13, columns[i].text.Split('\n').Length);
                }
                Assert.AreSame(columns[2], ui.pauseControls);
            }
            finally { if (ui != null) Object.DestroyImmediate(ui.gameObject); }
        }

        [Test]
        public void ReturnToCheckpointCancelsCaptureAndPreservesMemoriesAndPowers()
        {
            var player = new GameObject("Pause Respawn Test");
            try
            {
                var soul = player.AddComponent<PlayerSoul>();
                var spot = player.AddComponent<MemorySpot>();
                spot.memoryId = 1;
                var memories = player.AddComponent<MemoryCollector>();
                memories.spots = new[] { spot };
                Call(memories, "Awake");
                memories.Progress.MarkFound(1);
                soul.Powers.Unlock(Power.Manifest);
                soul.Respawn.Touch(new Candle(CandleKind.Family, Vector3.right * 6f));
                var respawn = player.AddComponent<PlayerRespawn>();
                Call(respawn, "Awake");
                respawn.groundMask = 0;
                respawn.Trigger();
                respawn.ReturnToCheckpoint();
                Call(respawn, "Update");
                Assert.IsFalse(respawn.Active);
                Assert.AreEqual(Vector3.right * 6f + Vector3.back * respawn.sideOffset, player.transform.position);
                Assert.AreEqual(50f, soul.Energy.Current);
                Assert.IsTrue(memories.IsFound(1) && soul.Powers.Has(Power.Manifest));
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void NavigationWrapsAcrossAllFourOptions()
        {
            var menu = new PauseMenu();
            Assert.AreEqual(0, menu.Index);
            menu.Previous();
            Assert.AreEqual(3, menu.Index);
            for (int i = 0; i < PauseMenu.OptionCount; i++)
            {
                menu.Next();
                Assert.AreEqual(i, menu.Index);
            }
        }

        [Test]
        public void FullscreenSubmissionKeepsPauseOpenAndOptionsOrdered()
        {
            GameInterface ui = null;
            Gamepad pad = null;
            var devices = new DualSenseInputTests();
            try
            {
                devices.SetUp();
                pad = InputSystem.AddDevice<Gamepad>();
                InputSystem.Update();
                ui = (GameInterface)typeof(EditorTools.SceneBuilder).GetMethod("BuildInterface", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { null });
                Call(ui, "Awake");
                ui.StartGame();
                ui.TogglePause();
                CollectionAssert.AreEqual(new[] { InterfaceTexts.PauseResume, InterfaceTexts.PauseRespawn,
                    InterfaceTexts.PauseFullscreen, InterfaceTexts.PauseRestart }, System.Array.ConvertAll(ui.pauseOptions, option => option.text));
                var menu = (PauseMenu)typeof(GameInterface).GetField("pauseMenu", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                menu.Select(2);
                GameInputTests.Press(Keyboard.current.enterKey);
                Call(ui, "Update");
                Assert.AreEqual((InterfaceScreen.Paused, true, 0f, true), (ui.Screen, ui.IsFrozen, Time.timeScale, AudioListener.pause));
            }
            finally
            {
                if (ui != null) { Call(ui, "OnDisable"); Object.DestroyImmediate(ui.gameObject); }
                if (pad != null) InputSystem.RemoveDevice(pad);
                devices.TearDown();
            }
        }

        [TestCase(0, PauseAction.Resume)]
        [TestCase(1, PauseAction.Respawn)]
        [TestCase(2, PauseAction.ToggleFullscreen)]
        public void NonRestartOptionsSubmitImmediately(int index, PauseAction action)
        {
            var menu = new PauseMenu();
            menu.Select(index);
            Assert.AreEqual(action, menu.Submit());
            Assert.IsFalse(menu.ConfirmingRestart);
        }

        [Test]
        public void RestartRequiresTwoSubmissionsEvenWhenHoverSelectsItAgain()
        {
            var menu = new PauseMenu();
            menu.Select(3);
            Assert.AreEqual(PauseAction.None, menu.Submit());
            Assert.IsTrue(menu.ConfirmingRestart);
            menu.Select(3);
            Assert.AreEqual(PauseAction.Restart, menu.Submit());
            Assert.IsFalse(menu.ConfirmingRestart);
        }

        [TestCase("next")]
        [TestCase("previous")]
        [TestCase("select")]
        [TestCase("close")]
        public void MovingOrClosingCancelsRestartConfirmation(string input)
        {
            var menu = new PauseMenu();
            menu.Select(3);
            menu.Submit();
            if (input == "next") menu.Next();
            else if (input == "previous") menu.Previous();
            else if (input == "select") menu.Select(0);
            else menu.Reset();
            Assert.IsFalse(menu.ConfirmingRestart);
            if (input == "close") Assert.AreEqual(0, menu.Index);
            menu.Select(3);
            Assert.AreEqual(PauseAction.None, menu.Submit());
        }
    }
}
