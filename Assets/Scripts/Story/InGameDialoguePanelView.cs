using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.Story
{
    public sealed class InGameDialoguePanelView : MonoBehaviour
    {
        [Serializable]
        private sealed class SpeakerProfile
        {
            [SerializeField] private string speaker;
            [SerializeField] private Sprite profileSprite;

            public Sprite ProfileSprite => profileSprite;

            public bool Matches(string value)
            {
                return !string.IsNullOrWhiteSpace(speaker)
                    && string.Equals(speaker, value, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform slideRoot;

        [Header("Slide")]
        [SerializeField] private Vector2 hiddenOffset = new(0f, -800f);
        [SerializeField, Min(0f)] private float showSeconds = 0.5f;
        [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Text")]
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;

        [Header("Profile")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Sprite fallbackProfileSprite;
        [SerializeField] private List<SpeakerProfile> speakerProfiles = new();

        [Header("Skip")]
        [SerializeField] private GameObject skipRoot;
        [SerializeField] private Image skipFillImage;

        public bool IsTyping { get; private set; }

        private CanvasGroup fallbackCanvasGroup;
        private Coroutine showRoutine;
        private Vector2 shownAnchoredPosition;
        private bool hasShownAnchoredPosition;
        private bool isVisible;

        private void Awake()
        {
            CacheShownPosition();
            Hide();
            SetSkipProgress(false, 0f);
        }

        public void ShowLine(string speaker, string text)
        {
            ShowPanel();
            SetSpeaker(speaker);
            SetText(dialogueText, text);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }
        }

        public IEnumerator PlayTypewriter(
            string text,
            Func<bool> revealRequested = null,
            Func<bool> cancelRequested = null)
        {
            if (dialogueText == null)
            {
                yield break;
            }

            IsTyping = true;
            dialogueText.text = text ?? string.Empty;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalCharacters = dialogueText.textInfo.characterCount;
            float visibleCharacters = 0f;
            float speed = Mathf.Max(1f, charactersPerSecond);
            while (visibleCharacters < totalCharacters)
            {
                if (cancelRequested?.Invoke() == true || revealRequested?.Invoke() == true)
                {
                    break;
                }

                visibleCharacters += Time.unscaledDeltaTime * speed;
                dialogueText.maxVisibleCharacters = Mathf.Clamp(
                    Mathf.CeilToInt(visibleCharacters),
                    0,
                    totalCharacters);
                yield return null;
            }

            RevealAll();
            IsTyping = false;
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
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            SetRootVisible(false);
            SetSpeaker(null);
            SetText(dialogueText, string.Empty);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }

            IsTyping = false;
            SetSkipProgress(false, 0f);
            SetSlideHidden();
            isVisible = false;
        }

        public IEnumerator HideAnimated()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            if (!isVisible)
            {
                Hide();
                yield break;
            }

            CacheShownPosition();
            if (showSeconds <= 0f || slideRoot == null)
            {
                Hide();
                yield break;
            }

            Vector2 from = slideRoot.anchoredPosition;
            Vector2 to = shownAnchoredPosition + hiddenOffset;
            for (float elapsed = 0f; elapsed < showSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / showSeconds);
                float eased = showCurve != null ? showCurve.Evaluate(t) : t;
                slideRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                yield return null;
            }

            Hide();
        }

        public void SetSkipProgress(bool visible, float progress)
        {
            if (skipRoot != null)
            {
                skipRoot.SetActive(visible);
            }

            if (skipFillImage != null)
            {
                skipFillImage.fillAmount = Mathf.Clamp01(progress);
                skipFillImage.gameObject.SetActive(visible);
            }
        }

        private void SetSpeaker(string speaker)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (speakerRoot != null)
            {
                speakerRoot.SetActive(hasSpeaker);
            }

            SetText(speakerText, hasSpeaker ? speaker : string.Empty);

            Sprite profileSprite = hasSpeaker ? ResolveProfileSprite(speaker) : null;
            if (profileImage != null)
            {
                profileImage.sprite = profileSprite;
                profileImage.enabled = profileSprite != null;
            }
        }

        private Sprite ResolveProfileSprite(string speaker)
        {
            for (int i = 0; i < speakerProfiles.Count; i++)
            {
                SpeakerProfile profile = speakerProfiles[i];
                if (profile != null && profile.Matches(speaker))
                {
                    return profile.ProfileSprite;
                }
            }

            return fallbackProfileSprite;
        }

        private void ShowPanel()
        {
            CacheShownPosition();
            bool shouldAnimate = !isVisible;
            SetRootVisible(true);
            isVisible = true;

            if (shouldAnimate)
            {
                PlayShowAnimation();
            }
        }

        private void PlayShowAnimation()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
            }

            if (showSeconds <= 0f || slideRoot == null)
            {
                SetSlideShown();
                showRoutine = null;
                return;
            }

            showRoutine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            Vector2 from = shownAnchoredPosition + hiddenOffset;
            Vector2 to = shownAnchoredPosition;
            slideRoot.anchoredPosition = from;

            for (float elapsed = 0f; elapsed < showSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / showSeconds);
                float eased = showCurve != null ? showCurve.Evaluate(t) : t;
                slideRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                yield return null;
            }

            SetSlideShown();
            showRoutine = null;
        }

        private void CacheShownPosition()
        {
            if (slideRoot == null)
            {
                slideRoot = root != null ? root.GetComponent<RectTransform>() : GetComponent<RectTransform>();
            }

            if (slideRoot != null && !hasShownAnchoredPosition)
            {
                shownAnchoredPosition = slideRoot.anchoredPosition;
                hasShownAnchoredPosition = true;
            }
        }

        private void SetSlideHidden()
        {
            if (slideRoot != null && hasShownAnchoredPosition)
            {
                slideRoot.anchoredPosition = shownAnchoredPosition + hiddenOffset;
            }
        }

        private void SetSlideShown()
        {
            if (slideRoot != null && hasShownAnchoredPosition)
            {
                slideRoot.anchoredPosition = shownAnchoredPosition;
            }
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
