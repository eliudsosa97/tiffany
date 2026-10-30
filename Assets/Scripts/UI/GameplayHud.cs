using UnityEngine;
using UnityEngine.UI;

namespace TiffanyGame
{
    public sealed class GameplayHud : MonoBehaviour
    {
        public GameInterface gameInterface;
        public Text objectiveText;
        public Text interactionText;

        readonly InteractionPrompt prompt = new InteractionPrompt();
        MemoryCollector memories;
        PlayerHiding hiding;
        CandleKindler kindler;
        PlayerSoul soul;
        CandleSpot[] candles;
        Camera viewCamera;
        RectTransform canvasRect;
        int foundCount = -1;

        void Start()
        {
            memories = gameInterface.memories;
            soul = gameInterface.soul;
            hiding = soul.GetComponent<PlayerHiding>();
            kindler = soul.GetComponent<CandleKindler>();
            viewCamera = memories.viewCamera;
            canvasRect = (RectTransform)transform;
            candles = FindObjectsByType<CandleSpot>(FindObjectsSortMode.None);
            memories.Collected += RefreshObjective;
            RefreshObjective(0);
            RefreshVisibility();
        }

        void RefreshObjective(int memoryId)
        {
            objectiveText.text = GameplayObjective.Text(memories.Progress);
            foundCount = memories.Progress.FoundCount;
        }

        void LateUpdate()
        {
            if (foundCount != memories.Progress.FoundCount) RefreshObjective(0);
            RefreshVisibility();
            if (gameInterface.Screen != InterfaceScreen.Playing || gameInterface.IsFrozen) return;

            prompt.Clear();
            if (hiding.Hidden) prompt.Consider(hiding, 0f, InteractionKind.Leave, hiding.isActiveAndEnabled);
            else
            {
                var hide = hiding.FindNearby(out float hideDistance);
                prompt.Consider(hide, hideDistance, InteractionKind.Hide);
                var memory = memories.FindNearby(out float memoryDistance);
                if (memory != null)
                    prompt.Consider(memory, memoryDistance,
                        memory.kind == MemorySpotKind.Ball ? InteractionKind.Collect : InteractionKind.Touch);
                var candle = kindler != null && kindler.isActiveAndEnabled ? kindler.FindTarget() : null;
                if (candle != null)
                {
                    Vector3 offset = candle.transform.position - soul.transform.position;
                    offset.y = 0f;
                    prompt.Consider(candle, offset.sqrMagnitude, InteractionKind.Kindle);
                }
                for (int i = 0; i < candles.Length; i++)
                {
                    var family = candles[i];
                    if (family == null || !family.isActiveAndEnabled || family.player != soul
                        || family.Candle.Kind != CandleKind.Family || !family.Site.Contains(soul.transform.position)) continue;
                    Vector3 offset = family.transform.position - soul.transform.position;
                    offset.y = 0f;
                    prompt.Consider(family, offset.sqrMagnitude, InteractionKind.Recharge);
                }
            }

            var target = prompt.Target as Component;
            bool visible = target != null && viewCamera != null;
            if (visible)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(target.transform.position + Vector3.up);
                visible = screen.z > 0f;
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var point))
                    interactionText.rectTransform.anchoredPosition = point;
            }
            interactionText.text = prompt.Text;
            SetVisible(interactionText, visible);
        }

        void RefreshVisibility()
        {
            bool playing = gameInterface.Screen == InterfaceScreen.Playing;
            SetVisible(objectiveText, playing);
            if (!playing || gameInterface.IsFrozen) SetVisible(interactionText, false);
        }

        static void SetVisible(Text text, bool visible)
        {
            if (text.gameObject.activeSelf != visible) text.gameObject.SetActive(visible);
        }

        void OnDestroy()
        {
            if (memories != null) memories.Collected -= RefreshObjective;
        }
    }
}
