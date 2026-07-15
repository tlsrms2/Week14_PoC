using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.Tutorial
{
    public sealed class TutorialDialoguePanelView : MonoBehaviour
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

        private const string DialoguePrefix = ">> ";
        private const string ObjectiveTitle = "[ 목표 ]";
        private const string ObjectiveTitleObjectName = "Dialogue_Text-title";
        private const string ObjectiveTextObjectName = "Dialogue_Text-objective";
        private const string AdvancePromptObjectName = "MouseClick_Image";

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform slideRoot;

        [Header("Text")]
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text objectiveTitleText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;

        [Header("Advance Prompt")]
        [SerializeField] private Image advancePromptImage;
        [SerializeField, Min(0.05f)] private float advancePromptBlinkSeconds = 0.65f;
        [SerializeField, Range(0f, 1f)] private float advancePromptMinAlpha = 0.15f;

        [Header("Profile")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Sprite fallbackProfileSprite;
        [SerializeField] private List<SpeakerProfile> speakerProfiles = new();

        [Header("Slide")]
        [SerializeField] private Vector2 hiddenOffset = new(-520f, 0f);
        [SerializeField, Min(0f)] private float showSeconds = 0.25f;
        [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Objective Complete")]
        [SerializeField] private Color objectiveCompleteColor = new(0.3f, 1f, 0.45f, 1f);
        [SerializeField, Min(1f)] private float objectiveStrikeLineHeight = 2f;
        [SerializeField, Min(0f)] private float objectiveCompleteHoldSeconds = 0.45f;
        [SerializeField, Min(0f)] private float objectiveCompleteFadeSeconds = 0.2f;

        private Coroutine showRoutine;
        private Coroutine advancePromptRoutine;
        private Image objectiveStrikeLine;
        private Vector2 shownAnchoredPosition;
        private Color defaultDialogueColor = Color.white;
        private Color advancePromptBaseColor = Color.white;
        private bool hasShownAnchoredPosition;
        private bool hasDefaultDialogueColor;
        private bool hasAdvancePromptBaseColor;
        private bool isVisible;

        public bool IsTyping { get; private set; }

        private void Awake()
        {
            ResolveObjectiveTextReferences();
            CacheShownPosition();
            CacheDefaultDialogueColor();
            CacheAdvancePromptImage();

            Hide();
        }

        public void ShowLine(string speaker, string text)
        {
            ShowPanel();
            SetAdvancePromptBlinking(false);
            SetObjectiveStrikeLineVisible(false);
            SetObjectiveTitleVisible(false);
            SetSpeaker(speaker);
            SetDialogueColor(defaultDialogueColor);
            SetDialogueText(FormatDialogue(text), 0);
        }

        public IEnumerator PlayTypewriter(string text, Func<bool> revealRequested = null, Func<bool> cancelRequested = null)
        {
            if (dialogueText == null)
            {
                yield break;
            }

            IsTyping = true;
            SetAdvancePromptBlinking(false);
            SetObjectiveStrikeLineVisible(false);
            SetDialogueColor(defaultDialogueColor);
            dialogueText.text = FormatDialogue(text);
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int totalCharacters = dialogueText.textInfo.characterCount;
            float visibleCharacters = 0f;
            float speed = Mathf.Max(1f, charactersPerSecond);
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
                dialogueText.maxVisibleCharacters = Mathf.Clamp(
                    Mathf.CeilToInt(visibleCharacters),
                    0,
                    totalCharacters);
                yield return null;
            }

            RevealAll();
            IsTyping = false;
            SetAdvancePromptBlinking(!canceled);
        }

        public void ShowObjective(string text)
        {
            ShowPanel();
            SetAdvancePromptBlinking(false);
            SetObjectiveStrikeLineVisible(false);
            SetSpeaker(null);
            SetObjectiveTitleVisible(true);
            SetDialogueColor(defaultDialogueColor);
            SetDialogueText(FormatObjective(text), int.MaxValue);
        }

        public IEnumerator PlayObjectiveCompleted(string text, bool fadeOut = true, bool clearText = true)
        {
            ShowPanel();
            SetAdvancePromptBlinking(false);
            SetSpeaker(null);
            SetObjectiveTitleVisible(true);
            SetDialogueColor(objectiveCompleteColor);
            SetDialogueText(FormatObjective(text), int.MaxValue);
            ShowObjectiveStrikeLine();

            yield return WaitUnscaled(objectiveCompleteHoldSeconds);

            if (fadeOut && dialogueText != null && objectiveCompleteFadeSeconds > 0f)
            {
                Color from = dialogueText.color;
                for (float elapsed = 0f; elapsed < objectiveCompleteFadeSeconds; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.Clamp01(elapsed / objectiveCompleteFadeSeconds);
                    Color color = from;
                    color.a = Mathf.Lerp(from.a, 0f, t);
                    dialogueText.color = color;
                    SetObjectiveStrikeLineAlpha(color.a);
                    yield return null;
                }
            }

            if (clearText)
            {
                SetObjectiveStrikeLineVisible(false);
                SetDialogueColor(defaultDialogueColor);
                SetDialogueText(string.Empty, 0);
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
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            SetAdvancePromptBlinking(false);
            SetRootVisible(false);
            SetSpeaker(null);
            SetObjectiveStrikeLineVisible(false);
            SetObjectiveTitleVisible(false);
            SetDialogueColor(defaultDialogueColor);
            SetText(dialogueText, string.Empty);

            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = 0;
            }

            SetSlideHidden();
            IsTyping = false;
            isVisible = false;
        }

        public IEnumerator HideAnimated()
        {
            if (showRoutine != null)
            {
                StopCoroutine(showRoutine);
                showRoutine = null;
            }

            SetAdvancePromptBlinking(false);
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

        private void SetDialogueText(string text, int maxVisibleCharacters)
        {
            SetText(dialogueText, text);
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = maxVisibleCharacters;
            }
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

        private void ResolveObjectiveTextReferences()
        {
            objectiveTitleText ??= FindChildText(ObjectiveTitleObjectName);

            if (dialogueText == null || IsNamed(dialogueText, ObjectiveTitleObjectName))
            {
                TMP_Text objectiveText = FindChildText(ObjectiveTextObjectName);
                if (objectiveText != null)
                {
                    dialogueText = objectiveText;
                }
            }
        }

        private TMP_Text FindChildText(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (IsNamed(text, objectName))
                {
                    return text;
                }
            }

            return null;
        }

        private static bool IsNamed(Component component, string objectName)
        {
            return component != null
                && string.Equals(component.name, objectName, StringComparison.Ordinal);
        }

        private void SetObjectiveTitleVisible(bool visible)
        {
            if (objectiveTitleText == null)
            {
                return;
            }

            objectiveTitleText.gameObject.SetActive(visible);
            SetText(objectiveTitleText, visible ? ObjectiveTitle : string.Empty);
        }

        private void ShowObjectiveStrikeLine()
        {
            if (dialogueText == null)
            {
                return;
            }

            Image line = EnsureObjectiveStrikeLine();
            if (line == null)
            {
                return;
            }

            dialogueText.ForceMeshUpdate();
            Bounds bounds = dialogueText.textBounds;
            RectTransform rectTransform = line.rectTransform;
            rectTransform.localPosition = new Vector3(bounds.center.x, bounds.center.y, 0f);
            rectTransform.sizeDelta = new Vector2(Mathf.Max(1f, bounds.size.x), Mathf.Max(1f, objectiveStrikeLineHeight));

            Color color = objectiveCompleteColor;
            line.color = color;
            line.enabled = true;
            line.gameObject.SetActive(true);
        }

        private Image EnsureObjectiveStrikeLine()
        {
            if (objectiveStrikeLine != null)
            {
                return objectiveStrikeLine;
            }

            if (dialogueText == null)
            {
                return null;
            }

            GameObject lineObject = new("ObjectiveStrikeLine", typeof(RectTransform), typeof(Image));
            lineObject.transform.SetParent(dialogueText.transform, false);

            RectTransform rectTransform = lineObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;

            objectiveStrikeLine = lineObject.GetComponent<Image>();
            objectiveStrikeLine.raycastTarget = false;
            objectiveStrikeLine.enabled = false;
            return objectiveStrikeLine;
        }

        private void SetObjectiveStrikeLineVisible(bool visible)
        {
            if (objectiveStrikeLine == null)
            {
                return;
            }

            objectiveStrikeLine.enabled = visible;
            objectiveStrikeLine.gameObject.SetActive(visible);
        }

        private void SetObjectiveStrikeLineAlpha(float alpha)
        {
            if (objectiveStrikeLine == null)
            {
                return;
            }

            Color color = objectiveStrikeLine.color;
            color.a = Mathf.Clamp01(alpha);
            objectiveStrikeLine.color = color;
        }

        private void CacheDefaultDialogueColor()
        {
            if (dialogueText == null || hasDefaultDialogueColor)
            {
                return;
            }

            defaultDialogueColor = dialogueText.color;
            hasDefaultDialogueColor = true;
        }

        private void SetDialogueColor(Color color)
        {
            CacheDefaultDialogueColor();
            if (dialogueText != null)
            {
                dialogueText.color = color;
            }
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

            gameObject.SetActive(visible);
        }

        private static string FormatDialogue(string text)
        {
            string value = text ?? string.Empty;
            return value.StartsWith(DialoguePrefix, StringComparison.Ordinal) ? value : DialoguePrefix + value;
        }

        private static string FormatObjective(string text)
        {
            return text ?? string.Empty;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
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
