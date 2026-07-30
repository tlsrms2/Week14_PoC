using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.UI;

namespace Week14.Cutscene
{
    public sealed class CutsceneDialoguePanelView : MonoBehaviour
    {
        private const string AdvancePromptObjectName = "MouseClick_Image";

        [SerializeField] private GameObject root;
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;
        [SerializeField] private Image advancePromptImage;
        [SerializeField, Min(0.05f)] private float advancePromptBlinkSeconds = 0.65f;
        [SerializeField, Range(0f, 1f)] private float advancePromptMinAlpha = 0.15f;

        public bool IsTyping { get; private set; }

        private CanvasGroup fallbackCanvasGroup;
        private Coroutine advancePromptRoutine;
        private Color advancePromptBaseColor = Color.white;
        private bool hasAdvancePromptBaseColor;

        private void Awake()
        {
            ClipDialogueToReferenceFrame();
            CacheAdvancePromptImage();
            Hide();
        }

        private void ClipDialogueToReferenceFrame()
        {
            UISafeFrameUtility.ClipToReferenceFrame(transform as RectTransform);
        }

        public void ShowLine(string speaker, string text)
        {
            SetRootVisible(true);
            SetAdvancePromptBlinking(false);

            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (speakerRoot != null)
            {
                speakerRoot.SetActive(hasSpeaker);
            }

            SetText(speakerText, hasSpeaker ? speaker : string.Empty);
            SetText(dialogueText, text);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }
        }

        public IEnumerator PlayTypewriter(
            string text,
            Func<bool> revealRequested = null,
            Func<bool> cancelRequested = null,
            Action characterRevealed = null)
        {
            if (dialogueText == null)
            {
                yield break;
            }

            IsTyping = true;
            SetAdvancePromptBlinking(false);
            dialogueText.text = text ?? string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalCharacters = dialogueText.textInfo.characterCount;
            float visibleCharacters = 0f;
            float speed = Mathf.Max(1f, charactersPerSecond);
            int previousVisibleCount = 0;
            bool canceled = false;
            while (visibleCharacters < totalCharacters)
            {
                if (cancelRequested?.Invoke() == true)
                {
                    canceled = true;
                    break;
                }

                if (revealRequested?.Invoke() == true)
                {
                    break;
                }

                visibleCharacters += Time.unscaledDeltaTime * speed;
                int visibleCount = Mathf.Clamp(
                    Mathf.CeilToInt(visibleCharacters),
                    0,
                    totalCharacters);
                dialogueText.maxVisibleCharacters = visibleCount;
                NotifyCharactersRevealed(
                    dialogueText.textInfo,
                    previousVisibleCount,
                    visibleCount,
                    characterRevealed);
                previousVisibleCount = visibleCount;
                yield return null;
            }

            RevealAll();
            IsTyping = false;
            SetAdvancePromptBlinking(!canceled);
        }

        private static void NotifyCharactersRevealed(
            TMP_TextInfo textInfo,
            int fromIndex,
            int toIndex,
            Action characterRevealed)
        {
            if (characterRevealed == null || textInfo == null)
            {
                return;
            }

            for (int i = fromIndex; i < toIndex && i < textInfo.characterCount; i++)
            {
                if (!char.IsWhiteSpace(textInfo.characterInfo[i].character))
                {
                    characterRevealed.Invoke();
                }
            }
        }

        public void RevealAll()
        {
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
        }

        public void Hide()
        {
            SetAdvancePromptBlinking(false);
            SetRootVisible(false);

            if (speakerRoot != null)
            {
                speakerRoot.SetActive(false);
            }

            SetText(speakerText, string.Empty);
            SetText(dialogueText, string.Empty);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }

            IsTyping = false;
        }

        private void CacheAdvancePromptImage()
        {
            if (advancePromptImage == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image image = images[i];
                    if (image != null && image.name == AdvancePromptObjectName)
                    {
                        advancePromptImage = image;
                        break;
                    }
                }
            }

            if (advancePromptImage == null || hasAdvancePromptBaseColor)
            {
                return;
            }

            advancePromptBaseColor = advancePromptImage.color;
            hasAdvancePromptBaseColor = true;
        }

        private void SetAdvancePromptBlinking(bool blinking)
        {
            CacheAdvancePromptImage();
            if (advancePromptRoutine != null)
            {
                StopCoroutine(advancePromptRoutine);
                advancePromptRoutine = null;
            }

            if (advancePromptImage == null)
            {
                return;
            }

            advancePromptImage.gameObject.SetActive(blinking);
            if (!blinking)
            {
                SetAdvancePromptAlpha(0f);
                return;
            }

            SetAdvancePromptAlpha(advancePromptBaseColor.a);
            advancePromptRoutine = StartCoroutine(AdvancePromptBlinkRoutine());
        }

        private IEnumerator AdvancePromptBlinkRoutine()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, advancePromptBlinkSeconds);
            float maxAlpha = hasAdvancePromptBaseColor ? advancePromptBaseColor.a : 1f;
            float minAlpha = Mathf.Clamp01(advancePromptMinAlpha) * maxAlpha;

            while (advancePromptImage != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float wave = (Mathf.Sin(elapsed / duration * Mathf.PI * 2f) + 1f) * 0.5f;
                SetAdvancePromptAlpha(Mathf.Lerp(minAlpha, maxAlpha, wave));
                yield return null;
            }

            advancePromptRoutine = null;
        }

        private void SetAdvancePromptAlpha(float alpha)
        {
            if (advancePromptImage == null)
            {
                return;
            }

            Color color = hasAdvancePromptBaseColor ? advancePromptBaseColor : advancePromptImage.color;
            color.a = Mathf.Clamp01(alpha);
            advancePromptImage.color = color;
        }

        private void SetRootVisible(bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
                return;
            }

            fallbackCanvasGroup ??= GetComponent<CanvasGroup>();
            fallbackCanvasGroup ??= gameObject.AddComponent<CanvasGroup>();
            fallbackCanvasGroup.alpha = visible ? 1f : 0f;
            fallbackCanvasGroup.interactable = visible;
            fallbackCanvasGroup.blocksRaycasts = visible;
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
