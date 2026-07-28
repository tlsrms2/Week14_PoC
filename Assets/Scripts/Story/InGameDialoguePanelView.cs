using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.UI;

namespace Week14.Story
{
    public sealed class InGameDialoguePanelView : MonoBehaviour
    {
        private const string AdvancePromptObjectName = "MouseClick_Image";

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

        [Header("Advance Prompt")]
        [SerializeField] private Image advancePromptImage;
        [SerializeField, Min(0.05f)] private float advancePromptBlinkSeconds = 0.65f;
        [SerializeField, Range(0f, 1f)] private float advancePromptMinAlpha = 0.15f;

        [Header("Profile")]
        [SerializeField] private Image profileImage;
        [SerializeField] private Sprite fallbackProfileSprite;
        [SerializeField] private List<SpeakerProfile> speakerProfiles = new();

        [Header("Visual Novel Portraits")]
        [SerializeField] private bool useVisualNovelPortraits = true;
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

        [Header("Skip")]
        [SerializeField] private GameObject skipRoot;
        [SerializeField] private Image skipFillImage;

        public bool IsTyping { get; private set; }

        private CanvasGroup fallbackCanvasGroup;
        private Coroutine showRoutine;
        private Coroutine advancePromptRoutine;
        private Vector2 shownAnchoredPosition;
        private Color advancePromptBaseColor = Color.white;
        private bool hasShownAnchoredPosition;
        private bool hasAdvancePromptBaseColor;
        private bool isVisible;
        private PortraitSlot activePortraitSlot;

        private void Awake()
        {
            ClipDialogueToReferenceFrame();
            CacheShownPosition();
            CacheAdvancePromptImage();
            CachePortraitSlots();
            Hide();
            SetSkipProgress(false, 0f);
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
            SetSpeaker(speaker, profileSpeaker, expressionId, portraitSlot, clearPortraitsBeforeLine);
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
            SetAdvancePromptBlinking(false);
            dialogueText.text = text ?? string.Empty;
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
