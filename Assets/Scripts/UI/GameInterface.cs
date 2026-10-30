using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameInterface : MonoBehaviour
    {
        public static bool SkipTitle;

        public AudioSource memoryMusic;
        public TextAsset[] memoryAssets;
        public Texture2D[] drawings;
        public GameObject titlePanel;
        public Text startHint;
        public Text continueOption;
        public Text newGameOption;
        public GameObject storyPanel;
        public Text storyText;
        public PlayerSoul soul;
        public WitchChapterTracker route;
        public RouteMap routeMap;
        public MemoryCollector memories;
        public GameObject soulPanel;
        public Image soulFill;
        public GameObject pausePanel;
        public Text pauseControls;
        public Text[] pauseOptions = Array.Empty<Text>();
        public PlayerRespawn playerRespawn;
        public GameObject notebookPanel;
        public Image notebookBackground;
        public Text notebookHint;
        public RawImage drawingImage;
        public AspectRatioFitter drawingFitter;
        public Text notebookTitle;
        public Text notebookPhrase;
        public Text notebookPage;
        public Text notebookEmpty;
        public Text noticeText;
        public Text subtitleText;
        public Image endingPanel;
        public Text endingText;

        MemoryCatalog catalog;
        Dictionary<string, Texture2D> textures;
        HashSet<string> missingDrawings;
        InterfaceFlow flow;
        NotebookPages pages;
        TimedLine notice;
        TimedLine subtitle;
        InterfaceScreen? appliedScreen;
        bool? appliedFrozen;
        bool pageDirty = true;
        int noticeVersion = -1;
        int subtitleVersion = -1;
        int startFrame = -1;
        SaveGame saveGame;
        MemorySlideshow slideshow;
        EchoPlayer memoryAudio;
        bool memoryNotice;
        readonly PauseMenu pauseMenu = new PauseMenu();
        StorySequence story;
        SoulMeter soulMeter;
        readonly RouteGuidance routeGuidance = new RouteGuidance();
        RouteTerrain guidanceTerrain;
        bool appliedReminder;
        RectTransform soulFillRect;
        bool skipStory;

        public InterfaceScreen Screen => flow.Screen;
        public bool IsFrozen => flow.IsFrozen || Time.frameCount == startFrame;
        public bool HasContinue => flow.HasContinue;

        public static bool HasControllerSymbols(Font font) => font.HasCharacter('△') && font.HasCharacter('□')
            && font.HasCharacter('○') && font.HasCharacter('✕');

        void Start()
        {
            if (pauseControls != null)
                pauseControls.text = InterfaceTexts.ControllerText(InterfaceTexts.Controls, HasControllerSymbols(pauseControls.font));
            var controls = titlePanel != null ? titlePanel.transform.Find("Controls")?.GetComponent<Text>() : null;
            if (controls == null) return;
            controls.text = InterfaceTexts.ControllerText(InterfaceTexts.Controls, HasControllerSymbols(controls.font));
            controls.fontSize = 20;
            controls.lineSpacing = 1f;
        }

        void Awake()
        {
            flow = new InterfaceFlow(!SkipTitle);
            skipStory = StorySequence.ShouldSkip(Environment.GetCommandLineArgs(),
                Application.isBatchMode || SaveGame.DisabledForTests);
#if UNITY_WEBGL && !UNITY_EDITOR && TIFFANY_WEB_DEBUG
            skipStory |= WebDebugQuery.Has(Application.absoluteURL, "playthrough");
#endif
            if (soul != null && soulFill != null)
            {
                soulMeter = new SoulMeter(soul.Energy);
                soulFillRect = soulFill.rectTransform;
            }
            if (route != null) guidanceTerrain = new RouteTerrain();
            notice = new TimedLine();
            subtitle = new TimedLine();
            pausePanel.SetActive(false);
            notebookPanel.SetActive(false);
            LoadMemories(memoryAssets, drawings);
        }

        public void LoadMemories(TextAsset[] memories, Texture2D[] drawingTextures)
        {
            memoryAssets = memories ?? Array.Empty<TextAsset>();
            drawings = drawingTextures ?? Array.Empty<Texture2D>();
            catalog = new MemoryCatalog(memoryAssets);
            textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            missingDrawings = new HashSet<string>(StringComparer.Ordinal);
            foreach (var drawing in drawings)
                if (drawing != null) textures[drawing.name] = drawing;
            pages = new NotebookPages();
            notice.Clear();
            pageDirty = true;
            RefreshNotebook();
            RefreshLines();
        }

        void OnEnable()
        {
            ApplyScreen();
        }

        void Update()
        {
            bool mapClosed = routeMap != null && routeMap.ReadInput(Screen, Screen == InterfaceScreen.Memory);
            if (Screen == InterfaceScreen.Title)
            {
                UpdateTitle();
            }
            else if (Screen == InterfaceScreen.Paused)
            {
                if (GameInput.PausePressed) flow.PressPause();
                else UpdatePause();
            }
            else if (Screen == InterfaceScreen.Story) UpdateStory();
            else if (Time.frameCount != startFrame)
            {
                if (GameInput.PausePressed && !mapClosed) flow.PressPause();
                if (GameInput.NotebookPressed) flow.PressNotebook();
            }
            ApplyScreen();
            if (Screen == InterfaceScreen.Memory) UpdateMemory();
            if (Screen == InterfaceScreen.Notebook)
            {
                if (GameInput.NotebookNextPressed) NextPage();
                if (GameInput.NotebookPreviousPressed) PreviousPage();
            }

            if (!IsFrozen)
            {
                notice.Tick(Time.deltaTime);
                subtitle.Tick(Time.deltaTime);
            }
            RefreshLines();
            RefreshSoul();
        }

        void LateUpdate()
        {
            if (guidanceTerrain == null || route.player == null) return;
            var witch = route.witch;
            bool chasing = witch != null && TensionMusicRules.IsChasing(witch.Decision, witch.Profile.FollowSpeed);
            bool inChallenge = false;
            if (memories != null)
                for (int i = 0; i < memories.challenges.Length; i++)
                    if (memories.challenges[i] != null && memories.challenges[i].Underway)
                    {
                        inChallenge = true;
                        break;
                    }
            Vector3 position = route.player.position;
            Vector3 local = route.transform.InverseTransformPoint(position);
            Vector3 toNext = route.NextClearingPosition - position;
            toNext.y = 0f;
            bool canRemind = route.HasNextClearing && !IsFrozen && !notice.IsVisible
                && RouteGuidance.CanRemind(Screen, memories != null && memories.IsEchoActive,
                    chasing, inChallenge, memories != null ? memories.Progress : null);
            routeGuidance.Tick(Time.deltaTime, guidanceTerrain.DistanceToRoute(local.x, local.z),
                route.LastVisitedClearingIndex, toNext.magnitude, canRemind);
            RefreshLines();
        }

        void UpdateStory()
        {
            if (GameInput.StoryAdvancePressed) story.Advance();
            else story.Tick(Time.unscaledDeltaTime);
            if (story.IsFinished)
            {
                flow.FinishStory();
                startFrame = Time.frameCount;
            }
            else
            {
                storyText.text = InterfaceTexts.StoryLine(story.Index);
                storyText.color = new Color(1f, 1f, 1f, story.Alpha);
            }
        }

        void RefreshSoul()
        {
            if (soulMeter == null) return;
            soulMeter.Tick(Time.deltaTime);
            soulFillRect.anchorMax = new Vector2(soulMeter.Fraction, 1f);
            soulFill.color = soulMeter.IsLow ? new Color(0.9f, 0.35f, 0.3f)
                : soulMeter.Change > 0 ? new Color(0.65f, 1f, 0.75f)
                : soulMeter.Change < 0 ? new Color(1f, 0.5f, 0.3f) : new Color(1f, 0.87f, 0.63f);
        }

        public bool MemoryFound(int memoryId, bool notify = true)
        {
            if (memoryId >= 1 && memoryId <= 5)
            {
                soulMeter?.Unlock(PowerSet.ForMemory(memoryId));
                ApplySoulVisibility();
            }
            if (!catalog.TryGet(memoryId, out var memory) || !pages.Add(memory)) return false;
            if (notify && memoryId >= 1 && memoryId <= 5)
                ShowPowerNotice(PowerSet.ForMemory(memoryId));
            pages.ShowFirstOf(memoryId);
            pageDirty = true;
            RefreshNotebook();
            RefreshLines();
            return true;
        }

        public bool TryGetMemory(int memoryId, out MemoryData memory) => catalog.TryGet(memoryId, out memory);

        public bool ShowMemory(int memoryId, EchoPlayer audio, bool notify = false)
        {
            if (memoryId < 1 || memoryId > 5 || Screen != InterfaceScreen.Playing || !catalog.TryGet(memoryId, out var memory)
                || memory.drawings.Length == 0) return false;
            slideshow = new MemorySlideshow(memory, audio != null ? audio.PrepareAudio() : null);
            memoryAudio = audio;
            memoryNotice = notify;
            startFrame = Time.frameCount;
            flow.SetMemory(true);
            ApplyScreen();
            if (memoryAudio != null) memoryAudio.Play(slideshow);
            if (memoryMusic != null)
            {
                memoryMusic.volume = slideshow.MusicVolume;
                memoryMusic.Play();
            }
            return true;
        }

        void UpdateMemory()
        {
            var stage = slideshow.Stage;
            int index = slideshow.PageIndex;
            slideshow.Tick(Time.unscaledDeltaTime, Time.frameCount != startFrame && GameInput.InteractPressed);
            if (memoryAudio != null) memoryAudio.ApplyAudio();
            if (memoryMusic != null) memoryMusic.volume = slideshow.MusicVolume;
            if (notebookBackground != null)
                notebookBackground.color = new Color(0f, 0f, 0f, slideshow.BlackOpacity);
            if (slideshow.Finished)
            {
                int id = slideshow.Memory.id;
                bool notify = memoryNotice;
                StopMemory();
                startFrame = Time.frameCount;
                ApplyScreen();
                if (notify) ShowPowerNotice(PowerSet.ForMemory(id));
                return;
            }
            if (stage == slideshow.Stage && index == slideshow.PageIndex) return;
            pageDirty = true;
            RefreshNotebook();
        }

        public void StopMemory()
        {
            if (slideshow == null) return;
            if (memoryAudio != null) memoryAudio.Stop();
            if (memoryMusic != null) memoryMusic.Stop();
            slideshow = null;
            memoryAudio = null;
            memoryNotice = false;
            flow.SetMemory(false);
            if (notebookBackground != null) notebookBackground.color = new Color(0f, 0f, 0f, 0.92f);
            ApplyScreen();
        }

        public void ShowPowerNotice(Power power)
        {
            notice.Enqueue(InterfaceTexts.ControllerText(InterfaceTexts.PowerNotice(power), HasControllerSymbols(noticeText.font)),
                InterfaceTexts.PowerNoticeSeconds);
            RefreshLines();
        }

        public void ShowTylerNotice()
        {
            notice.Enqueue(InterfaceTexts.TylerNotice, InterfaceTexts.PowerNoticeSeconds);
            RefreshLines();
        }

        public void ShowSubtitle(string text, float seconds)
        {
            subtitle.Enqueue(text, seconds);
            RefreshLines();
        }

        public void ReplaceSubtitle(string text, float seconds)
        {
            subtitle.Clear();
            ShowSubtitle(text, seconds);
        }

        public void StartGame()
        {
            if (Screen != InterfaceScreen.Title) return;
            if (saveGame != null)
            {
                if (flow.ContinueSelected) saveGame.Continue();
                else saveGame.Delete();
            }
            startFrame = Time.frameCount;
            flow.PressStart(!skipStory);
            if (Screen == InterfaceScreen.Story) story = new StorySequence();
            ApplyScreen();
        }

        public void ConfigureSave(SaveGame save, bool hasValidSave)
        {
            saveGame = save;
            flow.ConfigureTitle(hasValidSave);
            if (startHint != null) startHint.gameObject.SetActive(!HasContinue);
            if (continueOption != null) continueOption.gameObject.SetActive(HasContinue);
            if (newGameOption != null) newGameOption.gameObject.SetActive(HasContinue);
            RefreshTitleSelection();
        }

        public void NewGame()
        {
            if (Screen == InterfaceScreen.Paused)
            {
                saveGame?.Delete();
                SkipTitle = false;
                SceneManager.LoadScene(gameObject.scene.name);
                return;
            }
            if (Screen != InterfaceScreen.Title) return;
            flow.SelectTitleOption(false);
            StartGame();
        }

        void UpdateTitle()
        {
            bool submit = GameInput.StartPressed;
            if (!HasContinue)
            {
                if (submit) StartGame();
                return;
            }
            bool selected = flow.ContinueSelected;
            if (GameInput.TitlePreviousPressed) flow.SelectTitleOption(true);
            if (GameInput.TitleNextPressed) flow.SelectTitleOption(false);
            bool hit = false;
            bool clicked = GameInput.PointerPressed;
            if (GameInput.PointerMoved || clicked)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(continueOption.rectTransform, GameInput.PointerPosition))
                {
                    flow.SelectTitleOption(true);
                    hit = true;
                }
                else if (RectTransformUtility.RectangleContainsScreenPoint(newGameOption.rectTransform, GameInput.PointerPosition))
                {
                    flow.SelectTitleOption(false);
                    hit = true;
                }
            }
            if (selected != flow.ContinueSelected) RefreshTitleSelection();
            if (submit && (!clicked || hit)) StartGame();
        }

        void RefreshTitleSelection()
        {
            var highlight = new Color(1f, 0.87f, 0.63f);
            if (continueOption != null) continueOption.color = flow.ContinueSelected ? highlight : Color.gray;
            if (newGameOption != null) newGameOption.color = flow.ContinueSelected ? Color.gray : highlight;
        }

        void UpdatePause()
        {
            int previous = pauseMenu.Index;
            bool confirming = pauseMenu.ConfirmingRestart;
            if (GameInput.TitlePreviousPressed) pauseMenu.Previous();
            if (GameInput.TitleNextPressed) pauseMenu.Next();
            bool clicked = GameInput.PointerPressed;
            bool hit = false;
            if (GameInput.PointerMoved || clicked)
                for (int i = 0; i < pauseOptions.Length; i++)
                    if (RectTransformUtility.RectangleContainsScreenPoint(pauseOptions[i].rectTransform, GameInput.PointerPosition))
                    {
                        pauseMenu.Select(i);
                        hit = true;
                        break;
                    }
            if (GameInput.StartPressed && (!clicked || hit))
            {
                var action = pauseMenu.Submit();
                if (action == PauseAction.ToggleFullscreen && Application.isPlaying)
                    UnityEngine.Screen.fullScreen = !UnityEngine.Screen.fullScreen;
                if (action == PauseAction.Restart) { NewGame(); return; }
                if (action == PauseAction.Respawn) playerRespawn.ReturnToCheckpoint();
                if (action == PauseAction.Resume || action == PauseAction.Respawn)
                {
                    startFrame = Time.frameCount;
                    flow.PressPause();
                }
            }
            if (previous != pauseMenu.Index || confirming != pauseMenu.ConfirmingRestart) RefreshPauseSelection();
        }

        void RefreshPauseSelection()
        {
            for (int i = 0; i < pauseOptions.Length; i++)
                pauseOptions[i].color = i == pauseMenu.Index ? new Color(1f, 0.87f, 0.63f) : Color.gray;
            if (pauseOptions.Length == PauseMenu.OptionCount)
                pauseOptions[3].text = pauseMenu.ConfirmingRestart ? InterfaceTexts.PauseConfirmRestart : InterfaceTexts.PauseRestart;
        }

        public void TogglePause()
        {
            flow.PressPause();
            ApplyScreen();
        }

        public void ShowEnding(EndingRules ending)
        {
            if (ending.Stage == EndingStage.Approach) return;
            flow.SetEnding(ending.Stage != EndingStage.Idle);
            ApplyScreen();
            endingPanel.gameObject.SetActive(ending.Stage != EndingStage.Idle);
            endingPanel.color = new Color(1f, 1f, 1f, ending.FadeAlpha);
            endingText.text = ending.Stage == EndingStage.Credits ? InterfaceTexts.EndingCredits
                : ending.Stage == EndingStage.Text ? InterfaceTexts.EndingStory : string.Empty;
        }

        public void ToggleNotebook()
        {
            flow.PressNotebook();
            ApplyScreen();
        }

        public void NextPage()
        {
            int previous = pages.Index;
            pages.Next();
            if (pages.Index == previous) return;
            pageDirty = true;
            RefreshNotebook();
        }

        public void PreviousPage()
        {
            int previous = pages.Index;
            pages.Previous();
            if (pages.Index == previous) return;
            pageDirty = true;
            RefreshNotebook();
        }

        void ApplyScreen()
        {
            if (routeMap != null) routeMap.SetAvailability(Screen, Screen == InterfaceScreen.Memory);
            if (IsFrozen || appliedFrozen == true) GameInput.ConsumePointerPress();
            if (appliedFrozen != IsFrozen)
            {
                Time.timeScale = IsFrozen ? 0f : 1f;
                AudioListener.pause = IsFrozen;
                appliedFrozen = IsFrozen;
            }
            ApplySoulVisibility();
            if (appliedScreen == Screen) return;
            pauseMenu.Reset();
            if (Screen == InterfaceScreen.Paused) RefreshPauseSelection();
            if (titlePanel != null) titlePanel.SetActive(Screen == InterfaceScreen.Title);
            if (storyPanel != null) storyPanel.SetActive(Screen == InterfaceScreen.Story);
            pausePanel.SetActive(Screen == InterfaceScreen.Paused);
            notebookPanel.SetActive(Screen == InterfaceScreen.Notebook || Screen == InterfaceScreen.Memory);
            bool showLines = Screen == InterfaceScreen.Playing || Screen == InterfaceScreen.Paused;
            noticeText.gameObject.SetActive(showLines);
            subtitleText.gameObject.SetActive(showLines);
            appliedScreen = Screen;
            pageDirty = true;
            RefreshNotebook();
        }

        void ApplySoulVisibility()
        {
            bool visible = soulMeter != null && soulMeter.IsUnlocked
                && (Screen == InterfaceScreen.Playing || Screen == InterfaceScreen.Paused);
            if (soulPanel != null && soulPanel.activeSelf != visible) soulPanel.SetActive(visible);
        }

        void RefreshNotebook()
        {
            if (!pageDirty) return;
            if (Screen == InterfaceScreen.Memory)
            {
                pageDirty = false;
                bool visible = slideshow.PageVisible;
                notebookEmpty.gameObject.SetActive(false);
                drawingImage.gameObject.SetActive(visible);
                notebookTitle.gameObject.SetActive(visible);
                notebookPhrase.gameObject.SetActive(visible);
                notebookPage.gameObject.SetActive(false);
                if (notebookHint != null)
                {
                    notebookHint.text = InterfaceTexts.MemoryHint;
                    notebookHint.gameObject.SetActive(visible);
                }
                if (notebookBackground != null)
                    notebookBackground.color = new Color(0f, 0f, 0f, slideshow.BlackOpacity);
                if (!visible) return;
                var memory = slideshow.Memory;
                var drawing = memory.drawings[slideshow.PageIndex];
                notebookTitle.text = memory.title;
                notebookPhrase.text = drawing?.phrase;
                ShowDrawing(drawing?.image);
                return;
            }
            if (Screen != InterfaceScreen.Notebook) return;
            pageDirty = false;
            if (notebookHint != null)
            {
                notebookHint.text = InterfaceTexts.NotebookHint;
                notebookHint.gameObject.SetActive(true);
            }
            notebookEmpty.gameObject.SetActive(pages.IsEmpty);
            drawingImage.gameObject.SetActive(!pages.IsEmpty);
            notebookTitle.gameObject.SetActive(!pages.IsEmpty);
            notebookPhrase.gameObject.SetActive(!pages.IsEmpty);
            notebookPage.gameObject.SetActive(!pages.IsEmpty);
            if (pages.IsEmpty)
            {
                drawingImage.texture = null;
                return;
            }

            var page = pages.Current;
            notebookTitle.text = page.Title;
            notebookPhrase.text = page.Phrase;
            notebookPage.text = $"{page.Number} / {pages.Count}";
            ShowDrawing(page.Image);
        }

        void ShowDrawing(string imageName)
        {
            imageName ??= string.Empty;
            textures.TryGetValue(imageName, out var texture);
            drawingImage.texture = texture;
            drawingImage.enabled = texture != null;
            if (texture != null)
                drawingFitter.aspectRatio = (float)texture.width / texture.height;
            else if (missingDrawings.Add(imageName))
                Debug.LogWarning($"{GameInfo.LogPrefix} No se encontró el dibujo '{imageName}'.");
        }

        void RefreshLines()
        {
            bool reminderVisible = routeGuidance.IsVisible && !notice.IsVisible;
            if (noticeVersion != notice.Version || appliedReminder != reminderVisible)
            {
                noticeText.text = reminderVisible ? InterfaceTexts.RouteReminder : notice.Text;
                noticeVersion = notice.Version;
                appliedReminder = reminderVisible;
            }
            if (subtitleVersion != subtitle.Version)
            {
                subtitleText.text = subtitle.Text;
                subtitleVersion = subtitle.Version;
            }
        }

        void OnDisable()
        {
            StopMemory();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            appliedFrozen = null;
            appliedScreen = null;
        }
    }
}
