using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    public sealed class GameInterface : MonoBehaviour
    {
        public TextAsset[] memoryAssets;
        public Texture2D[] drawings;
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
        InterfaceScreen? appliedScreen;
        bool? appliedFrozen;
        bool pageDirty = true;
        int noticeVersion = -1;
        int subtitleVersion = -1;

        public InterfaceScreen Screen => flow.Screen;
        public bool IsFrozen => flow.IsFrozen;

        void Awake()
        {
            flow = new InterfaceFlow();
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
            if (GameInput.PausePressed) flow.PressPause();
            if (GameInput.NotebookPressed) flow.PressNotebook();
            ApplyScreen();
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
        }

        public bool MemoryFound(int memoryId)
        {
            if (!catalog.TryGet(memoryId, out var memory) || !pages.Add(memory)) return false;
            if (memoryId >= 1 && memoryId <= 5)
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
            notice.Enqueue(InterfaceTexts.PowerNotice(power), InterfaceTexts.PowerNoticeSeconds);
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
            if (appliedFrozen != IsFrozen)
            {
                Time.timeScale = IsFrozen ? 0f : 1f;
                AudioListener.pause = IsFrozen;
                appliedFrozen = IsFrozen;
            }
            if (appliedScreen == Screen) return;
            pausePanel.SetActive(Screen == InterfaceScreen.Paused);
            notebookPanel.SetActive(Screen == InterfaceScreen.Notebook);
            bool showLines = Screen == InterfaceScreen.Playing || Screen == InterfaceScreen.Paused;
            noticeText.gameObject.SetActive(showLines);
            subtitleText.gameObject.SetActive(showLines);
            appliedScreen = Screen;
            pageDirty = true;
            RefreshNotebook();
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
            if (noticeVersion != notice.Version)
            {
                noticeText.text = notice.Text;
                noticeVersion = notice.Version;
            }
            if (subtitleVersion != subtitle.Version)
            {
                subtitleText.text = subtitle.Text;
                subtitleVersion = subtitle.Version;
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
