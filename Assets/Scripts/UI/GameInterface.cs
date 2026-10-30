using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameInterface : MonoBehaviour
    {
        public static bool SkipTitle;

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
        public MemoryCollector memories;
        public GameObject soulPanel;
        public Image soulFill;
        public GameObject pausePanel;
        public GameObject notebookPanel;
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
        TimedLine echoSubtitle;
        bool showingEcho;
        InterfaceScreen? appliedScreen;
        bool? appliedFrozen;
        bool pageDirty = true;
        int noticeVersion = -1;
        int subtitleVersion = -1;
        int startFrame = -1;
        SaveGame saveGame;
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
            echoSubtitle = new TimedLine();
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
            if (Screen == InterfaceScreen.Title)
            {
                UpdateTitle();
            }
            else if (Screen == InterfaceScreen.Story) UpdateStory();
            else if (Time.frameCount != startFrame)
            {
                if (GameInput.PausePressed) flow.PressPause();
                if (GameInput.NotebookPressed) flow.PressNotebook();
            }
            ApplyScreen();
            if (Screen == InterfaceScreen.Notebook)
            {
                if (GameInput.NotebookNextPressed) NextPage();
                if (GameInput.NotebookPreviousPressed) PreviousPage();
            }

            if (!IsFrozen)
            {
                if (showingEcho) echoSubtitle.Tick(Time.deltaTime);
                else
                {
                    notice.Tick(Time.deltaTime);
                    subtitle.Tick(Time.deltaTime);
                }
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
                && RouteGuidance.CanRemind(Screen, showingEcho || (memories != null && memories.IsEchoActive),
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

        public void ShowPowerNotice(Power power)
        {
            notice.Enqueue(InterfaceTexts.ControllerText(InterfaceTexts.PowerNotice(power), HasControllerSymbols(noticeText.font)),
                InterfaceTexts.PowerNoticeSeconds);
            RefreshLines();
        }

        public void ShowSubtitle(string text, float seconds)
        {
            (showingEcho ? echoSubtitle : subtitle).Enqueue(text, seconds);
            RefreshLines();
        }

        public void ReplaceSubtitle(string text, float seconds)
        {
            (showingEcho ? echoSubtitle : subtitle).Clear();
            ShowSubtitle(text, seconds);
        }

        public void BeginEcho()
        {
            showingEcho = true;
            echoSubtitle.Clear();
            RefreshEchoLines();
        }

        public void EndEcho()
        {
            showingEcho = false;
            echoSubtitle.Clear();
            RefreshEchoLines();
        }

        void RefreshEchoLines()
        {
            subtitleVersion = -1;
            appliedScreen = null;
            ApplyScreen();
            RefreshLines();
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

        public void TogglePause()
        {
            flow.PressPause();
            ApplyScreen();
        }

        public void ShowEnding(EndingRules ending)
        {
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
            if (IsFrozen || appliedFrozen == true) GameInput.ConsumePointerPress();
            if (appliedFrozen != IsFrozen)
            {
                Time.timeScale = IsFrozen ? 0f : 1f;
                AudioListener.pause = IsFrozen;
                appliedFrozen = IsFrozen;
            }
            ApplySoulVisibility();
            if (appliedScreen == Screen) return;
            if (titlePanel != null) titlePanel.SetActive(Screen == InterfaceScreen.Title);
            if (storyPanel != null) storyPanel.SetActive(Screen == InterfaceScreen.Story);
            pausePanel.SetActive(Screen == InterfaceScreen.Paused);
            notebookPanel.SetActive(Screen == InterfaceScreen.Notebook);
            bool showLines = Screen == InterfaceScreen.Playing || Screen == InterfaceScreen.Paused;
            noticeText.gameObject.SetActive(!showingEcho && showLines);
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
            if (Screen != InterfaceScreen.Notebook || !pageDirty) return;
            pageDirty = false;
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
            string imageName = page.Image ?? string.Empty;
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
            var line = showingEcho ? echoSubtitle : subtitle;
            if (subtitleVersion != line.Version)
            {
                subtitleText.text = line.Text;
                subtitleVersion = line.Version;
            }
        }

        void OnDisable()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            appliedFrozen = null;
            appliedScreen = null;
        }
    }
}
