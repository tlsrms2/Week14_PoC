using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;
using Week14.Story;
using Week14.UI;

namespace Week14.Tutorial
{
    public sealed class TutorialDialoguePanelView : MonoBehaviour
    {
        [Serializable]
        private sealed class SpeakerProfile
        {
            [SerializeField] private string speaker;
            [SerializeField] private string expressionId;
            [SerializeField] private Sprite profileSprite;

            public Sprite ProfileSprite => profileSprite;
            public bool IsDefaultExpression => string.IsNullOrWhiteSpace(expressionId);

            public bool Matches(string value)
            {
                return MatchesSpeaker(value) && IsDefaultExpression;
            }

            public bool MatchesSpeaker(string value)
            {
                return !string.IsNullOrWhiteSpace(speaker)
                    && string.Equals(speaker, value, StringComparison.OrdinalIgnoreCase);
            }

            public bool MatchesExpression(string value)
            {
                if (IsDefaultExpression && string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                return !string.IsNullOrWhiteSpace(expressionId)
                    && !string.IsNullOrWhiteSpace(value)
                    && string.Equals(expressionId, value, StringComparison.OrdinalIgnoreCase);
            }
        }

        private readonly struct PortraitVisualState
        {
            public readonly Vector2 Offset;
            public readonly float Scale;
            public readonly float Alpha;
            public readonly float Brightness;

            public PortraitVisualState(Vector2 offset, float scale, float alpha, float brightness)
            {
                Offset = offset;
                Scale = scale;
                Alpha = alpha;
                Brightness = brightness;
            }

            public static PortraitVisualState Lerp(PortraitVisualState from, PortraitVisualState to, float t)
            {
                return new PortraitVisualState(
                    Vector2.LerpUnclamped(from.Offset, to.Offset, t),
                    Mathf.LerpUnclamped(from.Scale, to.Scale, t),
                    Mathf.LerpUnclamped(from.Alpha, to.Alpha, t),
                    Mathf.LerpUnclamped(from.Brightness, to.Brightness, t));
            }
        }

        [Serializable]
        private sealed class PortraitSlot
        {
            [SerializeField] private Image image;
            [SerializeField] private RectTransform rectTransform;

            [NonSerialized] public string Speaker;
            [NonSerialized] public string ExpressionId;
            [NonSerialized] public Coroutine TransitionRoutine;
            [NonSerialized] public Vector2 BaseAnchoredPosition;
            [NonSerialized] public Vector3 BaseLocalScale = Vector3.one;
            [NonSerialized] public Color BaseColor = Color.white;
            [NonSerialized] public PortraitVisualState CurrentState = new(Vector2.zero, 1f, 0f, 1f);
            [NonSerialized] public bool HasCachedBase;
            [NonSerialized] public bool ClearSpriteWhenHidden;

            public Image Image => image;
            public RectTransform RectTransform => rectTransform;
            public bool HasImage => image != null;
            public bool HasCharacter => HasImage
                && image.sprite != null
                && !string.IsNullOrWhiteSpace(Speaker);

            public void Cache()
            {
                if (image == null)
                {
                    return;
                }

                rectTransform ??= image.rectTransform;
                if (HasCachedBase)
                {
                    return;
                }

                BaseAnchoredPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
                BaseLocalScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
                BaseColor = image.color;
                if (BaseColor.a <= 0.001f)
                {
                    BaseColor.a = 1f;
                }

                HasCachedBase = true;
            }

            public void ClearIdentity()
            {
                Speaker = null;
                ExpressionId = null;
            }
        }

        private const string DialoguePrefix = ">> ";
        private const string ObjectiveTitleTable = "Story";
        private const string ObjectiveTitleKey = "TutorialDialogueSet_Objective";
        private const string ObjectiveTitleObjectName = "Dialogue_Text-title";
        private const string ObjectiveTextObjectName = "Dialogue_Text-objective";
        private const string ObjectiveKeyTextObjectName = "Dialogue_Text-key";
        private const string AdvancePromptObjectName = "MouseClick_Image";

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform slideRoot;

        [Header("Text")]
        [SerializeField] private GameObject speakerRoot;
        [SerializeField] private TMP_Text speakerText;
        [SerializeField] private TMP_Text objectiveTitleText;
        [SerializeField] private TMP_Text objectiveKeyText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField, Min(1f)] private float charactersPerSecond = 45f;
        [SerializeField] private LocalizedString localizedObjectiveTitleText = new(ObjectiveTitleTable, ObjectiveTitleKey);

        [Header("Advance Prompt")]
        [SerializeField] private Image advancePromptImage;
        [SerializeField, Min(0.05f)] private float advancePromptBlinkSeconds = 0.65f;
        [SerializeField, Range(0f, 1f)] private float advancePromptMinAlpha = 0.15f;

        [Header("Profile")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Sprite fallbackProfileSprite;
        [SerializeField] private List<SpeakerProfile> speakerProfiles = new();

        [Header("Visual Novel Portraits")]
        [SerializeField] private bool useVisualNovelPortraits;
        [SerializeField] private PortraitSlot leftPortrait = new();
        [SerializeField] private PortraitSlot rightPortrait = new();
        [SerializeField, Min(0f)] private float portraitTransitionSeconds = 0.2f;
        [SerializeField] private AnimationCurve portraitTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private Vector2 activePortraitOffset = Vector2.zero;
        [SerializeField] private Vector2 inactivePortraitOffset = new(0f, -35f);
        [SerializeField] private Vector2 hiddenPortraitOffset = new(0f, -80f);
        [SerializeField] private Vector2 leftHiddenPortraitSlideOffset = new(-120f, 0f);
        [SerializeField] private Vector2 rightHiddenPortraitSlideOffset = new(120f, 0f);
        [SerializeField, Min(0f)] private float activePortraitScale = 1f;
        [SerializeField, Min(0f)] private float inactivePortraitScale = 0.92f;
        [SerializeField, Range(0f, 1f)] private float inactivePortraitBrightness = 0.45f;

        [Header("Slide")]
        [SerializeField] private Vector2 hiddenOffset = new(-520f, 0f);
        [SerializeField, Min(0f)] private float showSeconds = 0.25f;
        [SerializeField] private AnimationCurve showCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Objective Complete")]
        [SerializeField] private Color objectiveCompleteColor = new(0.3f, 1f, 0.45f, 1f);
        [SerializeField] private Vector2 objectiveLineOffset = new(0f, 0f);
        [SerializeField, Min(1f)] private float objectiveStrikeLineHeight = 2f;
        [SerializeField, Min(0f)] private float objectiveCompleteHoldSeconds = 0.45f;
        [SerializeField, Min(0f)] private float objectiveCompleteFadeSeconds = 0.2f;

        private Coroutine showRoutine;
        private Coroutine advancePromptRoutine;
        private Image objectiveStrikeLine;
        private Vector2 shownAnchoredPosition;
        private Color defaultDialogueColor = Color.white;
        private Color defaultObjectiveKeyColor = Color.white;
        private Color advancePromptBaseColor = Color.white;
        private bool hasShownAnchoredPosition;
        private bool hasDefaultDialogueColor;
        private bool hasDefaultObjectiveKeyColor;
        private bool hasAdvancePromptBaseColor;
        private bool isVisible;
        private PortraitSlot activePortraitSlot;

        public bool IsTyping { get; private set; }

        private void Awake()
        {
            ClipDialogueToReferenceFrame();
            ResolveObjectiveTextReferences();
            CacheShownPosition();
            CacheDefaultDialogueColor();
            CacheAdvancePromptImage();
            CachePortraitSlots();

            Hide();
        }

        private void ClipDialogueToReferenceFrame()
        {
            UISafeFrameUtility.ClipToReferenceFrame(transform as RectTransform);
        }

        public void ShowLine(
            string speaker,
            string text,
            string profileSpeaker = null,
            string expressionId = null,
            InGameDialoguePortraitSlot portraitSlot = InGameDialoguePortraitSlot.Auto,
            bool clearPortraitsBeforeLine = false)
        {
            ShowPanel();
            SetAdvancePromptBlinking(false);
            SetObjectiveStrikeLineVisible(false);
            SetObjectiveTitleVisible(false);
            SetObjectiveKeyText(string.Empty, false);
            SetSpeaker(speaker, profileSpeaker, expressionId, portraitSlot, clearPortraitsBeforeLine);
            SetDialogueColor(defaultDialogueColor);
            SetDialogueText(FormatDialogue(text), 0);
        }

        public void ReplaceLineText(
            string speaker,
            string text,
            string profileSpeaker = null,
            string expressionId = null,
            InGameDialoguePortraitSlot portraitSlot = InGameDialoguePortraitSlot.Auto)
        {
            SetSpeaker(speaker, profileSpeaker, expressionId, portraitSlot);
            SetDialogueColor(defaultDialogueColor);

            int visibleCharacters = dialogueText != null ? dialogueText.maxVisibleCharacters : 0;
            if (!IsTyping)
            {
                visibleCharacters = int.MaxValue;
            }

            SetDialogueText(FormatDialogue(text), visibleCharacters);
        }

        public IEnumerator PlayTypewriter(
            string text,
            Func<bool> revealRequested = null,
            Func<bool> cancelRequested = null,
            Action<char> onCharacterRevealed = null)
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
            int revealedCharacterCount = 0;
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
                int nextVisibleCharacterCount = Mathf.Clamp(
                    Mathf.CeilToInt(visibleCharacters),
                    0,
                    totalCharacters);
                dialogueText.maxVisibleCharacters = nextVisibleCharacterCount;
                DialogueTypewriterUtility.NotifyRevealedCharacters(
                    dialogueText,
                    revealedCharacterCount,
                    nextVisibleCharacterCount,
                    onCharacterRevealed);
                revealedCharacterCount = nextVisibleCharacterCount;
                yield return null;
            }

            RevealAll();
            IsTyping = false;
            SetAdvancePromptBlinking(!canceled);
        }

        public void ShowObjective(string text, string keyText = null)
        {
            ShowPanel();
            SetAdvancePromptBlinking(false);
            SetObjectiveStrikeLineVisible(false);
            SetSpeaker(null);
            SetObjectiveTitleVisible(true);
            SetDialogueColor(defaultDialogueColor);
            SetObjectiveKeyColor(defaultObjectiveKeyColor);
            SetObjectiveKeyText(keyText, true);
            SetDialogueText(FormatObjective(text), int.MaxValue);
        }

        public IEnumerator PlayObjectiveCompleted(string text, string keyText = null, bool fadeOut = true, bool clearText = true)
        {
            ShowPanel();
            SetAdvancePromptBlinking(false);
            SetSpeaker(null);
            SetObjectiveTitleVisible(true);
            SetDialogueColor(objectiveCompleteColor);
            SetObjectiveKeyColor(objectiveCompleteColor);
            SetObjectiveKeyText(keyText, true);
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
                    SetObjectiveKeyAlpha(color.a);
                    SetObjectiveStrikeLineAlpha(color.a);
                    yield return null;
                }
            }

            if (clearText)
            {
                SetObjectiveStrikeLineVisible(false);
                SetDialogueColor(defaultDialogueColor);
                SetObjectiveKeyColor(defaultObjectiveKeyColor);
                SetObjectiveKeyText(string.Empty, false);
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
            ClearPortraitSlots(true);
            SetObjectiveStrikeLineVisible(false);
            SetObjectiveTitleVisible(false);
            SetObjectiveKeyText(string.Empty, false);
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
            bool animatePortraits = HasVisiblePortraits();
            if (animatePortraits)
            {
                ClearPortraitSlots(false);
            }

            float slideSeconds = showSeconds > 0f && slideRoot != null ? showSeconds : 0f;
            float exitSeconds = Mathf.Max(slideSeconds, animatePortraits ? portraitTransitionSeconds : 0f);
            if (exitSeconds <= 0f)
            {
                Hide();
                yield break;
            }

            Vector2 from = slideRoot != null ? slideRoot.anchoredPosition : Vector2.zero;
            Vector2 to = slideRoot != null ? shownAnchoredPosition + hiddenOffset : Vector2.zero;
            for (float elapsed = 0f; elapsed < exitSeconds; elapsed += Time.unscaledDeltaTime)
            {
                if (slideSeconds > 0f)
                {
                    float t = Mathf.Clamp01(elapsed / slideSeconds);
                    float eased = showCurve != null ? showCurve.Evaluate(t) : t;
                    slideRoot.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
                }

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

        private void SetSpeaker(
            string speaker,
            string profileSpeaker = null,
            string expressionId = null,
            InGameDialoguePortraitSlot portraitSlot = InGameDialoguePortraitSlot.Auto,
            bool clearPortraitsBeforeLine = false)
        {
            bool hasSpeaker = !string.IsNullOrWhiteSpace(speaker);
            if (speakerRoot != null)
            {
                speakerRoot.SetActive(hasSpeaker);
            }

            SetText(speakerText, hasSpeaker ? speaker : string.Empty);

            string profileKey = !string.IsNullOrWhiteSpace(profileSpeaker) ? profileSpeaker : speaker;
            Sprite profileSprite = hasSpeaker ? ResolveProfileSprite(profileKey, expressionId) : null;
            bool usingVisualNovelPortraits = CanUseVisualNovelPortraits();
            if (profileImage != null)
            {
                profileImage.sprite = profileSprite;
                profileImage.enabled = profileSprite != null && !usingVisualNovelPortraits;
            }

            if (usingVisualNovelPortraits)
            {
                UpdatePortraitStage(
                    hasSpeaker ? profileKey : null,
                    expressionId,
                    portraitSlot,
                    clearPortraitsBeforeLine);
            }
        }

        private Sprite ResolveProfileSprite(string speaker)
        {
            return ResolveProfileSprite(speaker, null);
        }

        private Sprite ResolveProfileSprite(string speaker, string expressionId)
        {
            Sprite defaultSprite = null;
            Sprite firstSpeakerSprite = null;
            bool hasExpression = !string.IsNullOrWhiteSpace(expressionId);

            for (int i = 0; i < speakerProfiles.Count; i++)
            {
                SpeakerProfile profile = speakerProfiles[i];
                if (profile == null || !profile.MatchesSpeaker(speaker))
                {
                    continue;
                }

                if (firstSpeakerSprite == null)
                {
                    firstSpeakerSprite = profile.ProfileSprite;
                }

                if (hasExpression && profile.MatchesExpression(expressionId) && profile.ProfileSprite != null)
                {
                    return profile.ProfileSprite;
                }

                if (profile.IsDefaultExpression && defaultSprite == null)
                {
                    defaultSprite = profile.ProfileSprite;
                }
            }

            return defaultSprite != null
                ? defaultSprite
                : firstSpeakerSprite != null ? firstSpeakerSprite : fallbackProfileSprite;
        }

        private void CachePortraitSlots()
        {
            leftPortrait?.Cache();
            rightPortrait?.Cache();
        }

        private bool CanUseVisualNovelPortraits()
        {
            CachePortraitSlots();
            return useVisualNovelPortraits
                && (IsPortraitSlotConfigured(leftPortrait) || IsPortraitSlotConfigured(rightPortrait));
        }

        private bool HasVisiblePortraits()
        {
            return CanUseVisualNovelPortraits()
                && ((leftPortrait != null && leftPortrait.HasCharacter)
                    || (rightPortrait != null && rightPortrait.HasCharacter));
        }

        private void UpdatePortraitStage(
            string speaker,
            string expressionId,
            InGameDialoguePortraitSlot portraitSlot,
            bool clearPortraitsBeforeLine)
        {
            if (clearPortraitsBeforeLine)
            {
                ClearPortraitSlots(false);
            }

            if (string.IsNullOrWhiteSpace(speaker))
            {
                SetPortraitFocus(null, false);
                return;
            }

            if (portraitSlot == InGameDialoguePortraitSlot.Hidden)
            {
                ClearSpeakerPortrait(speaker, false);
                SetPortraitFocus(null, false);
                return;
            }

            Sprite portraitSprite = ResolveProfileSprite(speaker, expressionId);
            PortraitSlot targetSlot = ResolvePortraitSlot(speaker, portraitSlot);
            if (targetSlot == null || portraitSprite == null)
            {
                SetPortraitFocus(null, false);
                return;
            }

            ClearSpeakerFromOtherSlots(speaker, targetSlot);
            SetPortraitSprite(targetSlot, speaker, expressionId, portraitSprite);
            SetPortraitFocus(targetSlot, false);
        }

        private PortraitSlot ResolvePortraitSlot(string speaker, InGameDialoguePortraitSlot requestedSlot)
        {
            PortraitSlot requestedPortraitSlot = GetPortraitSlot(requestedSlot);
            if (IsPortraitSlotConfigured(requestedPortraitSlot))
            {
                return requestedPortraitSlot;
            }

            PortraitSlot existingSlot = FindPortraitSlotBySpeaker(speaker);
            if (existingSlot != null)
            {
                return existingSlot;
            }

            if (IsPortraitSlotEmpty(leftPortrait))
            {
                return leftPortrait;
            }

            if (IsPortraitSlotEmpty(rightPortrait))
            {
                return rightPortrait;
            }

            if (activePortraitSlot == leftPortrait && IsPortraitSlotConfigured(rightPortrait))
            {
                return rightPortrait;
            }

            if (activePortraitSlot == rightPortrait && IsPortraitSlotConfigured(leftPortrait))
            {
                return leftPortrait;
            }

            return IsPortraitSlotConfigured(leftPortrait)
                ? leftPortrait
                : IsPortraitSlotConfigured(rightPortrait) ? rightPortrait : null;
        }

        private PortraitSlot GetPortraitSlot(InGameDialoguePortraitSlot requestedSlot)
        {
            return requestedSlot switch
            {
                InGameDialoguePortraitSlot.Left => leftPortrait,
                InGameDialoguePortraitSlot.Right => rightPortrait,
                _ => null
            };
        }

        private PortraitSlot FindPortraitSlotBySpeaker(string speaker)
        {
            if (PortraitSlotMatchesSpeaker(leftPortrait, speaker))
            {
                return leftPortrait;
            }

            return PortraitSlotMatchesSpeaker(rightPortrait, speaker) ? rightPortrait : null;
        }

        private void SetPortraitSprite(PortraitSlot slot, string speaker, string expressionId, Sprite sprite)
        {
            if (!IsPortraitSlotConfigured(slot) || sprite == null)
            {
                return;
            }

            slot.Cache();
            bool hadCharacter = slot.HasCharacter;
            bool speakerChanged = !string.Equals(slot.Speaker, speaker, StringComparison.OrdinalIgnoreCase);

            if (!hadCharacter || speakerChanged)
            {
                ApplyPortraitState(slot, GetHiddenPortraitState(slot));
            }

            slot.Speaker = speaker;
            slot.ExpressionId = expressionId;
            slot.ClearSpriteWhenHidden = false;
            slot.Image.sprite = sprite;
            slot.Image.preserveAspect = true;
            slot.Image.enabled = true;
        }

        private void SetPortraitFocus(PortraitSlot focusedSlot, bool immediate)
        {
            activePortraitSlot = focusedSlot != null && focusedSlot.HasCharacter ? focusedSlot : null;
            RefreshPortraitState(leftPortrait, immediate);
            RefreshPortraitState(rightPortrait, immediate);
        }

        private void RefreshPortraitState(PortraitSlot slot, bool immediate)
        {
            if (!IsPortraitSlotConfigured(slot))
            {
                return;
            }

            bool visible = slot.HasCharacter;
            bool active = visible && slot == activePortraitSlot;
            TransitionPortrait(slot, GetPortraitState(slot, visible, active), immediate);
        }

        private PortraitVisualState GetPortraitState(PortraitSlot slot, bool visible, bool active)
        {
            if (!visible)
            {
                return GetHiddenPortraitState(slot);
            }

            return active
                ? new PortraitVisualState(activePortraitOffset, activePortraitScale, 1f, 1f)
                : new PortraitVisualState(
                    inactivePortraitOffset,
                    inactivePortraitScale,
                    1f,
                    inactivePortraitBrightness);
        }

        private PortraitVisualState GetHiddenPortraitState(PortraitSlot slot)
        {
            Vector2 slideOffset = Vector2.zero;
            if (slot == leftPortrait)
            {
                slideOffset = leftHiddenPortraitSlideOffset;
            }
            else if (slot == rightPortrait)
            {
                slideOffset = rightHiddenPortraitSlideOffset;
            }

            return new PortraitVisualState(
                hiddenPortraitOffset + slideOffset,
                inactivePortraitScale,
                0f,
                inactivePortraitBrightness);
        }

        private void TransitionPortrait(PortraitSlot slot, PortraitVisualState targetState, bool immediate)
        {
            if (!IsPortraitSlotConfigured(slot))
            {
                return;
            }

            StopPortraitTransition(slot);
            slot.Cache();
            if (immediate || portraitTransitionSeconds <= 0f || !isActiveAndEnabled)
            {
                ApplyPortraitState(slot, targetState);
                CompletePortraitTransition(slot, targetState);
                return;
            }

            slot.TransitionRoutine = StartCoroutine(PortraitTransitionRoutine(slot, slot.CurrentState, targetState));
        }

        private IEnumerator PortraitTransitionRoutine(PortraitSlot slot, PortraitVisualState from, PortraitVisualState to)
        {
            float duration = Mathf.Max(0.001f, portraitTransitionSeconds);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = portraitTransitionCurve != null ? portraitTransitionCurve.Evaluate(t) : t;
                ApplyPortraitState(slot, PortraitVisualState.Lerp(from, to, eased));
                yield return null;
            }

            ApplyPortraitState(slot, to);
            CompletePortraitTransition(slot, to);
            slot.TransitionRoutine = null;
        }

        private void CompletePortraitTransition(PortraitSlot slot, PortraitVisualState state)
        {
            if (!IsPortraitSlotConfigured(slot)
                || state.Alpha > 0.001f
                || !slot.ClearSpriteWhenHidden)
            {
                return;
            }

            slot.Image.sprite = null;
            slot.Image.enabled = false;
            slot.ClearSpriteWhenHidden = false;
        }

        private void ApplyPortraitState(PortraitSlot slot, PortraitVisualState state)
        {
            if (!IsPortraitSlotConfigured(slot))
            {
                return;
            }

            slot.Cache();
            slot.CurrentState = state;
            if (slot.RectTransform != null)
            {
                slot.RectTransform.anchoredPosition = slot.BaseAnchoredPosition + state.Offset;
                slot.RectTransform.localScale = slot.BaseLocalScale * Mathf.Max(0f, state.Scale);
            }

            Image image = slot.Image;
            Color color = slot.BaseColor;
            float brightness = Mathf.Max(0f, state.Brightness);
            color.r *= brightness;
            color.g *= brightness;
            color.b *= brightness;
            color.a *= Mathf.Clamp01(state.Alpha);
            image.color = color;
            image.enabled = image.sprite != null && state.Alpha > 0.001f;
        }

        private void ClearPortraitSlots(bool immediate)
        {
            ClearPortraitSlot(leftPortrait, immediate);
            ClearPortraitSlot(rightPortrait, immediate);
            activePortraitSlot = null;
        }

        private void ClearPortraitSlot(PortraitSlot slot, bool immediate)
        {
            if (!IsPortraitSlotConfigured(slot))
            {
                return;
            }

            StopPortraitTransition(slot);
            slot.ClearIdentity();
            if (immediate)
            {
                slot.Image.sprite = null;
                slot.ClearSpriteWhenHidden = false;
            }
            else
            {
                slot.ClearSpriteWhenHidden = slot.Image.sprite != null;
            }

            TransitionPortrait(slot, GetHiddenPortraitState(slot), immediate);
        }

        private void ClearSpeakerPortrait(string speaker, bool immediate)
        {
            if (PortraitSlotMatchesSpeaker(leftPortrait, speaker))
            {
                ClearPortraitSlot(leftPortrait, immediate);
            }

            if (PortraitSlotMatchesSpeaker(rightPortrait, speaker))
            {
                ClearPortraitSlot(rightPortrait, immediate);
            }
        }

        private void ClearSpeakerFromOtherSlots(string speaker, PortraitSlot targetSlot)
        {
            if (targetSlot != leftPortrait && PortraitSlotMatchesSpeaker(leftPortrait, speaker))
            {
                ClearPortraitSlot(leftPortrait, true);
            }

            if (targetSlot != rightPortrait && PortraitSlotMatchesSpeaker(rightPortrait, speaker))
            {
                ClearPortraitSlot(rightPortrait, true);
            }
        }

        private void StopPortraitTransition(PortraitSlot slot)
        {
            if (slot?.TransitionRoutine == null)
            {
                return;
            }

            StopCoroutine(slot.TransitionRoutine);
            slot.TransitionRoutine = null;
        }

        private static bool IsPortraitSlotConfigured(PortraitSlot slot)
        {
            return slot != null && slot.HasImage;
        }

        private static bool IsPortraitSlotEmpty(PortraitSlot slot)
        {
            return IsPortraitSlotConfigured(slot) && !slot.HasCharacter;
        }

        private static bool PortraitSlotMatchesSpeaker(PortraitSlot slot, string speaker)
        {
            return IsPortraitSlotConfigured(slot)
                && !string.IsNullOrWhiteSpace(slot.Speaker)
                && string.Equals(slot.Speaker, speaker, StringComparison.OrdinalIgnoreCase);
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
            objectiveKeyText ??= FindChildText(ObjectiveKeyTextObjectName);

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
            SetText(objectiveTitleText, visible ? ResolveObjectiveTitleText() : string.Empty);
        }

        private void SetObjectiveKeyText(string value, bool objectiveVisible)
        {
            if (objectiveKeyText == null)
            {
                return;
            }

            bool visible = objectiveVisible && !string.IsNullOrWhiteSpace(value);
            objectiveKeyText.gameObject.SetActive(visible);
            SetText(objectiveKeyText, visible ? value : string.Empty);
        }

        private string ResolveObjectiveTitleText()
        {
            if (HasLocalizedString(localizedObjectiveTitleText))
            {
                return localizedObjectiveTitleText.GetLocalizedString();
            }

            LocalizedString fallback = new(ObjectiveTitleTable, ObjectiveTitleKey);
            return fallback.GetLocalizedString();
        }

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
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
            rectTransform.localPosition = new Vector3(bounds.center.x + objectiveLineOffset.x, bounds.center.y + objectiveLineOffset.y, 0f);
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

            if (objectiveKeyText != null && !hasDefaultObjectiveKeyColor)
            {
                defaultObjectiveKeyColor = objectiveKeyText.color;
                hasDefaultObjectiveKeyColor = true;
            }
        }

        private void SetDialogueColor(Color color)
        {
            CacheDefaultDialogueColor();
            if (dialogueText != null)
            {
                dialogueText.color = color;
            }
        }

        private void SetObjectiveKeyColor(Color color)
        {
            CacheDefaultDialogueColor();
            if (objectiveKeyText != null)
            {
                objectiveKeyText.color = color;
            }
        }

        private void SetObjectiveKeyAlpha(float alpha)
        {
            if (objectiveKeyText == null)
            {
                return;
            }

            Color color = objectiveKeyText.color;
            color.a = Mathf.Clamp01(alpha);
            objectiveKeyText.color = color;
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
