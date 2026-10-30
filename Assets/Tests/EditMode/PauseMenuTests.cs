using NUnit.Framework;
using UnityEngine;

namespace TiffanyGame.Tests
{
    public sealed class PauseMenuTests
    {
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
                memories.SendMessage("Awake");
                memories.Progress.MarkFound(1);
                soul.Powers.Unlock(Power.Manifest);
                soul.Respawn.Touch(new Candle(CandleKind.Family, Vector3.right * 6f));
                var respawn = player.AddComponent<PlayerRespawn>();
                respawn.SendMessage("Awake");
                respawn.groundMask = 0;
                respawn.Trigger();
                respawn.ReturnToCheckpoint();
                respawn.SendMessage("Update");
                Assert.IsFalse(respawn.Active);
                Assert.AreEqual(Vector3.right * 6f + Vector3.back * respawn.sideOffset, player.transform.position);
                Assert.AreEqual(50f, soul.Energy.Current);
                Assert.IsTrue(memories.IsFound(1) && soul.Powers.Has(Power.Manifest));
            }
            finally { Object.DestroyImmediate(player); }
        }

        [Test]
        public void NavigationWrapsAcrossAllThreeOptions()
        {
            var menu = new PauseMenu();
            Assert.AreEqual(0, menu.Index);
            menu.Previous();
            Assert.AreEqual(2, menu.Index);
            menu.Next();
            Assert.AreEqual(0, menu.Index);
            menu.Next();
            Assert.AreEqual(1, menu.Index);
            menu.Next();
            Assert.AreEqual(2, menu.Index);
        }

        [TestCase(0, PauseAction.Resume)]
        [TestCase(1, PauseAction.Respawn)]
        public void ResumeAndRespawnSubmitImmediately(int index, PauseAction action)
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
            menu.Select(2);
            Assert.AreEqual(PauseAction.None, menu.Submit());
            Assert.IsTrue(menu.ConfirmingRestart);
            menu.Select(2);
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
            menu.Select(2);
            menu.Submit();
            if (input == "next") menu.Next();
            else if (input == "previous") menu.Previous();
            else if (input == "select") menu.Select(0);
            else menu.Reset();
            Assert.IsFalse(menu.ConfirmingRestart);
            if (input == "close") Assert.AreEqual(0, menu.Index);
            menu.Select(2);
            Assert.AreEqual(PauseAction.None, menu.Submit());
        }
    }
}
