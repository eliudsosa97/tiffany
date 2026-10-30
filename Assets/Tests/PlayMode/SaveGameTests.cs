using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TiffanyGame.Tests
{
    public sealed class SaveGameTests
    {
        const string SaveKey = "Tiffany.Tests.SaveGame";
        const string CheckpointId = "Route 3 Train Tracks/Family Candle";
        SaveGame save;
        GameInterface ui;
        TextAsset[] memoryAssets;
        Texture2D drawing;
        bool previousSkipTitle;
        string previousTestKey;
        InputSettings.BackgroundBehavior previousBackgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInputBehavior;
        InputDevice device;

        static SaveData Prepared() => new SaveData
        {
            version = SaveRules.Version, memories = new[] { 1, MemoryProgress.BallId, 2, 3, 4, 5 },
            candleId = CheckpointId, chapter = Chapter.Return, energy = 37f, completedChallenges = 15
        };

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousSkipTitle = GameInterface.SkipTitle;
            previousTestKey = SaveGame.TestKey;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            GameInterface.SkipTitle = false;
            SaveGame.TestKey = SaveKey;
            GameInput.MoveOverride = GameInput.AimOverride = Vector2.zero;
            GameInput.RunOverride = GameInput.SlowOverride = GameInput.InteractOverride = false;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Prepared()));
            PlayerPrefs.Save();
            yield return Load();
        }

        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            save = Object.FindAnyObjectByType<SaveGame>();
            Assert.IsNotNull(save, "Regenera Main desde SceneBuilder.");
            ui = save.gameInterface;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (device != null) InputSystem.RemoveDevice(device);
            device = null;
            GameInput.ClearOverrides();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.CreateScene(nameof(SaveGameTests));
            var main = SceneManager.GetSceneByName("Main");
            if (main.isLoaded) yield return SceneManager.UnloadSceneAsync(main);
            if (memoryAssets != null)
                foreach (var asset in memoryAssets) Object.DestroyImmediate(asset);
            memoryAssets = null;
            Object.DestroyImmediate(drawing);
            drawing = null;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            SaveGame.TestKey = previousTestKey;
            GameInterface.SkipTitle = previousSkipTitle;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
        }

        [Test]
        public void ContinueRestoresCheckpointMemoriesPowersTylerChapterAndEnergyWithoutEcho()
        {
            memoryAssets = new TextAsset[5];
            drawing = new Texture2D(1, 1) { name = "save-test-drawing" };
            for (int id = 1; id <= memoryAssets.Length; id++)
            {
                var memory = new MemoryData
                {
                    id = id,
                    title = $"Recuerdo de prueba {id}",
                    drawings = new[] { new MemoryDrawing { image = drawing.name, phrase = $"Página de prueba {id}" } }
                };
                memoryAssets[id - 1] = new TextAsset(JsonUtility.ToJson(memory));
            }
            ui.LoadMemories(memoryAssets, new[] { drawing });
            Assert.IsTrue(ui.HasContinue);
            Assert.AreEqual(InterfaceTexts.Continue, ui.continueOption.text);
            Assert.AreEqual(InterfaceTexts.NewGame, ui.newGameOption.text);
            Assert.IsTrue(ui.continueOption.gameObject.activeInHierarchy && ui.newGameOption.gameObject.activeInHierarchy);
            Assert.IsFalse(ui.startHint.gameObject.activeSelf);
            Assert.IsFalse(save.memories.IsFound(5));
            Assert.IsFalse(ui.soulPanel.activeSelf);
            ui.StartGame();
            Assert.IsTrue(ui.soulPanel.activeSelf);
            var candle = System.Array.Find(save.candles, candidate => candidate.saveId == CheckpointId);
            Assert.AreEqual(candle.Candle.Position, save.soul.Respawn.Position);
            Assert.Less(Vector3.Distance(save.soul.transform.position, candle.transform.position), candle.radius);
            Assert.AreEqual(candle.transform.position.z - save.GetComponent<PlayerRespawn>().sideOffset,
                save.soul.transform.position.z, 0.01f);
            Assert.AreEqual(50f, save.soul.Energy.Current);
            for (int id = 1; id <= 5; id++)
            {
                Assert.IsTrue(save.memories.IsFound(id));
                Assert.IsTrue(save.soul.Powers.Has(PowerSet.ForMemory(id)));
            }
            Assert.IsTrue(save.memories.IsFound(MemoryProgress.BallId));
            Assert.IsTrue(save.memories.tyler.gameObject.activeSelf);
            Assert.IsNull(save.memories.tyler.nextMemory);
            Assert.AreEqual(Chapter.Return, save.witch.chapter);
            foreach (var challenge in save.memories.challenges)
            {
                Assert.IsTrue(challenge.Rules.Completed);
                Assert.IsFalse(challenge.Underway);
                foreach (var gate in challenge.darkness) Assert.IsFalse(gate.activeSelf);
            }
            Assert.IsNull(save.witch.WatchPassage);
            Assert.IsNull(save.memories.ActiveEcho);
            Assert.IsEmpty(ui.noticeText.text);
            foreach (var spot in save.memories.spots) Assert.IsFalse(spot.visual.activeSelf);
            ui.ToggleNotebook();
            Assert.IsFalse(ui.notebookEmpty.gameObject.activeSelf);
            for (int page = 1; page < memoryAssets.Length; page++) ui.PreviousPage();
            for (int id = 1; id <= memoryAssets.Length; id++)
            {
                Assert.AreEqual($"Recuerdo de prueba {id}", ui.notebookTitle.text);
                Assert.AreEqual($"Página de prueba {id}", ui.notebookPhrase.text);
                Assert.AreEqual($"{id} / {memoryAssets.Length}", ui.notebookPage.text);
                Assert.AreSame(drawing, ui.drawingImage.texture);
                ui.NextPage();
            }
        }

        [Test]
        public void NewGameDeletesSaveAndKeepsFreshState()
        {
            ui.NewGame();
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
            Assert.AreEqual(0, save.memories.Progress.FoundCount);
            Assert.IsFalse(save.soul.Powers.Has(Power.Manifest));
            Assert.AreEqual(save.soul.startEnergy, save.soul.Energy.Current);
            Assert.AreEqual(Chapter.Cemetery, save.witch.chapter);
            Assert.IsFalse(save.memories.tyler.gameObject.activeSelf);
            Assert.AreEqual(GameObject.Find("Route").transform.GetChild(0).Find("Spawn").position, save.soul.Respawn.Position);
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
        }

        [UnityTest]
        public IEnumerator MissingAndInvalidSaveKeepOriginalStartScreen()
        {
            foreach (string json in new[] { string.Empty, "{", "{\"version\":99}" })
            {
                if (json.Length == 0) PlayerPrefs.DeleteKey(SaveKey);
                else PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
                yield return Load();
                Assert.IsFalse(ui.HasContinue);
                Assert.IsTrue(ui.startHint.gameObject.activeInHierarchy);
                Assert.IsFalse(ui.continueOption.gameObject.activeSelf || ui.newGameOption.gameObject.activeSelf);
                Assert.AreEqual(0, save.memories.Progress.FoundCount);
            }
        }

        [UnityTest]
        public IEnumerator FamilyCandleWritesOnceOnEntryAndMemoryWritesUpdatedProgress()
        {
            ui.NewGame();
            save.witch.gameObject.SetActive(false);
            yield return null;
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
            var candle = save.initialCandle;
            MoveTo(candle.transform.position + Vector3.back * 1.5f);
            save.soul.Energy.ResetTo(42f);
            yield return null;
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Assert.AreEqual(candle.saveId, data.candleId);
            Assert.AreEqual(save.soul.Respawn.Position, candle.Candle.Position);
            Assert.That(data.energy, Is.InRange(42f, 50f));
            string first = PlayerPrefs.GetString(SaveKey);
            save.soul.Energy.ResetTo(10f);
            yield return null;
            yield return null;
            Assert.AreEqual(first, PlayerPrefs.GetString(SaveKey));
            Assert.IsTrue(save.memories.Collect(MemoryProgress.BallId));
            data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            CollectionAssert.AreEqual(new[] { MemoryProgress.BallId }, data.memories);
            Assert.IsTrue(save.memories.Collect(1));
            data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            CollectionAssert.AreEquivalent(new[] { 1, MemoryProgress.BallId }, data.memories);
            Assert.AreEqual(save.soul.Energy.Current, data.energy);
            yield return Load();
            Assert.IsTrue(ui.HasContinue);
            ui.StartGame();
            Assert.IsTrue(save.memories.IsFound(1) && save.memories.IsFound(MemoryProgress.BallId));
            Assert.AreEqual(save.initialCandle.Candle.Position, save.soul.Respawn.Position);
            Assert.AreEqual(50f, save.soul.Energy.Current);
        }

        [UnityTest]
        public IEnumerator SolvedChallengesSurviveNextCandleAndContinueAllowsNextMemory()
        {
            ui.NewGame();
            save.witch.gameObject.SetActive(false);
            Assert.IsTrue(save.memories.Collect(1));
            Assert.IsTrue(save.memories.Collect(MemoryProgress.BallId));
            Assert.IsTrue(save.memories.Collect(2));
            var candles = System.Array.Find(save.memories.challenges, challenge => challenge.kind == ChallengeKind.Candles);
            candles.Rules.MarkCompleted();
            Assert.IsTrue(save.memories.Collect(3));
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Assert.AreEqual(1, data.completedChallenges);
            var distract = System.Array.Find(save.memories.challenges, challenge => challenge.kind == ChallengeKind.Distract);
            distract.Rules.MarkCompleted();
            MoveTo(distract.RechargePoint);
            yield return null;
            data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Assert.AreEqual(distract.checkpoint.saveId, data.candleId);
            Assert.AreEqual(3, data.completedChallenges);
            string checkpointId = data.candleId;
            yield return Load();
            Assert.IsTrue(ui.HasContinue);
            foreach (var challenge in save.memories.challenges) Assert.IsFalse(challenge.Rules.Completed);
            ui.StartGame();
            Assert.AreEqual(50f, save.soul.Energy.Current);
            foreach (var challenge in save.memories.challenges)
            {
                bool completed = challenge.kind == ChallengeKind.Candles || challenge.kind == ChallengeKind.Distract;
                Assert.AreEqual(completed, challenge.Rules.Completed);
                foreach (var gate in challenge.darkness) Assert.AreEqual(!completed, gate.activeSelf);
            }
            distract = System.Array.Find(save.memories.challenges, challenge => challenge.kind == ChallengeKind.Distract);
            Assert.AreEqual(distract.checkpoint.Candle.Position, save.soul.Respawn.Position);
            Assert.Less(Vector3.Distance(save.soul.transform.position, distract.checkpoint.transform.position), distract.checkpoint.radius);
            MoveTo(distract.Entry);
            yield return null;
            yield return null;
            Assert.IsFalse(distract.Underway);
            Assert.IsNull(save.witch.WatchPassage);
            Assert.IsTrue(save.memories.Collect(4));
            Assert.IsFalse(save.memories.Collect(5));
            data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Assert.AreEqual(checkpointId, data.candleId);
            Assert.AreEqual(3, data.completedChallenges);
            CollectionAssert.Contains(data.memories, 4);
        }

        [Test]
        public void ApplyingRestoredChallengeOpensDarknessAndEndsItsActiveWatch()
        {
            var challenge = System.Array.Find(save.memories.challenges, candidate => candidate.kind == ChallengeKind.Hut);
            save.witch.BeginWatch(challenge.transform, challenge.post.position, true, true);
            Assert.AreSame(challenge.transform, save.witch.WatchPassage);
            challenge.Rules.MarkCompleted();
            challenge.ApplyRestoredState();
            Assert.IsNull(save.witch.WatchPassage);
            Assert.IsFalse(challenge.Underway);
            foreach (var gate in challenge.darkness) Assert.IsFalse(gate.activeSelf);
        }

        [Test]
        public void MemoryBeforeFirstCandleUsesInitialRouteCandle()
        {
            ui.NewGame();
            Assert.IsTrue(save.memories.Collect(1));
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            Assert.AreEqual(save.initialCandle.saveId, data.candleId);
            CollectionAssert.AreEqual(new[] { 1 }, data.memories);
        }

        [UnityTest]
        public IEnumerator CreditsDeleteSaveAndDoNotWriteItAgain()
        {
            ui.StartGame();
            yield return null;
            MoveTo(save.ending.transform.position + Vector3.back * 1.5f);
            GameInput.InteractOverride = true;
            yield return null;
            GameInput.InteractOverride = false;
            Assert.AreEqual(EndingStage.Approach, save.ending.Rules.Stage);
            Assert.IsTrue(PlayerPrefs.HasKey(SaveKey));
            save.ending.Rules.Tick(EndingRules.ApproachSeconds + EndingRules.FadeSeconds + EndingRules.TextSeconds);
            yield return null;
            Assert.AreEqual(EndingStage.Credits, save.ending.Rules.Stage);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
            yield return null;
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
        }

        [UnityTest]
        public IEnumerator KeyboardCanSelectNewGameAndConfirm()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            device = keyboard;
            Assert.IsTrue(keyboard.enabled);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
            yield return null;
            Assert.AreEqual(InterfaceScreen.Title, ui.Screen);
            Assert.AreEqual(Color.gray, ui.continueOption.color);
            Assert.IsTrue(PlayerPrefs.HasKey(SaveKey));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
        }

        [UnityTest]
        public IEnumerator GamepadCanSelectNewGameAndConfirm()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            device = gamepad;
            Assert.IsTrue(gamepad.enabled);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown));
            yield return null;
            Assert.AreEqual(InterfaceScreen.Title, ui.Screen);
            Assert.AreEqual(Color.gray, ui.continueOption.color);
            Assert.IsTrue(PlayerPrefs.HasKey(SaveKey));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
        }

        [UnityTest]
        public IEnumerator MouseCanClickNewGame()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            device = mouse;
            Assert.IsTrue(mouse.enabled);
            Canvas.ForceUpdateCanvases();
            var point = RectTransformUtility.WorldToScreenPoint(null, ui.newGameOption.rectTransform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            Assert.AreEqual(InterfaceScreen.Title, ui.Screen);
            Assert.IsTrue(PlayerPrefs.HasKey(SaveKey));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            Assert.AreEqual(InterfaceScreen.Playing, ui.Screen);
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKey));
        }

        void MoveTo(Vector3 position)
        {
            var character = save.GetComponent<CharacterController>();
            character.enabled = false;
            save.transform.position = position;
            character.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
