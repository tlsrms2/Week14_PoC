using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Cutscene
{
    public sealed class CutscenePlayer : MonoBehaviour, IBackClosable
    {
        [Header("Root")]
        [SerializeField] private GameObject cutsceneRoot;

        [Header("Visual")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image backgroundTransitionImage;
        [SerializeField] private RectTransform backgroundMotionTarget;
        [SerializeField] private CanvasGroup fadeGroup;

        [Header("Views")]
        [SerializeField] private CutsceneDialoguePanelView dialoguePanelView;

        [Header("Dialogue")]
        [SerializeField, Min(0f)] private float dialogueStartDelaySeconds = 0.25f;

        [Header("Skip Hold")]
        [SerializeField] private Image skipHoldFillImage;
        [SerializeField] private TMP_Text skipHoldPromptText;
        [SerializeField, Min(0.1f)] private float skipHoldSeconds = 1.2f;

        private CutsceneDefinition currentDefinition;
        private Coroutine playRoutine;
        private Coroutine motionRoutine;
        private Coroutine backgroundGlitchRoutine;
        private Action completed;
        private Sprite backgroundGlitchBaseSprite;
        private bool isPlaying;
        private bool isTyping;
        private bool advanceRequested;
        private bool revealRequested;
        private bool skipRequested;
        private bool previousInputBlock;
        private bool keepCoveredOnCompleted;
        private bool startCovered;
        private bool canUseSkip;
        private bool skipWholeCutscene;
        private bool isCurrentStepSkippable;
        private float skipHoldElapsed;
        private bool skipSectionRequested;

        public bool IsPlaying => isPlaying;

        private void Awake()
        {
            ResolveDialoguePanelView();
            SetRootVisible(false);
            ClearDialogueView();
            HideTransitionImage();
            SetFadeAlpha(0f);
            SetSkipHoldVisible(false);
            SetSkipHoldProgress(0f);
        }

        private void OnDestroy()
        {
            UIBackStack.Remove(this);
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            if (AdvancePressed())
            {
                RequestAdvance();
            }

            UpdateSkipHold();
        }

        public void Play(
            CutsceneDefinition definition,
            Action onCompleted = null,
            bool keepCoveredOnCompleted = false,
            bool? skippableOverride = null,
            bool startCovered = false)
        {
            StopPlayback(invokeCompleted: false);

            if (definition == null)
            {
                Debug.LogWarning($"{nameof(CutscenePlayer)}: definition is null.");
                onCompleted?.Invoke();
                return;
            }

            currentDefinition = definition;
            completed = onCompleted;
            this.keepCoveredOnCompleted = keepCoveredOnCompleted;
            this.startCovered = startCovered;
            canUseSkip = skippableOverride ?? definition.Skippable;
            skipWholeCutscene = definition.Skippable && skippableOverride != false;
            playRoutine = StartCoroutine(PlayRoutine(definition));
        }

        public void RequestAdvance()
        {
            if (isTyping)
            {
                revealRequested = true;
                return;
            }

            advanceRequested = true;
        }

        public void Skip()
        {
            if (!CanSkipCurrentCutscene())
            {
                return;
            }

            if (skipWholeCutscene)
            {
                skipRequested = true;
            }
            else
            {
                skipSectionRequested = true;
            }

            revealRequested = true;
            advanceRequested = true;
            SetSkipHoldProgress(1f);
        }

        public bool CloseByBack()
        {
            if (!isPlaying)
            {
                return false;
            }

            return true;
        }

        private IEnumerator PlayRoutine(CutsceneDefinition definition)
        {
            BeginPlayback(definition);
            if (startCovered)
            {
                SceneTransition.ReleaseCoveredScreen();
            }

            IReadOnlyList<CutsceneStep> steps = definition.Steps;
            CutsceneStep lastPlayedStep = null;
            for (int i = 0; i < steps.Count && !skipRequested; i++)
            {
                CutsceneStep step = steps[i];
                if (step == null)
                {
                    continue;
                }

                SetCurrentStep(step);
                yield return ExecuteStep(step, i > 0);
                lastPlayedStep = step;

                if (skipSectionRequested)
                {
                    i = FindNextNonSkippableStepIndex(steps, i + 1) - 1;
                    skipSectionRequested = false;
                    revealRequested = false;
                    advanceRequested = false;
                    skipHoldElapsed = 0f;
                    SetSkipHoldProgress(0f);
                    ClearDialogueView();
                }
            }

            StopBackgroundGlitch();
            yield return CoverScreenForCompletion(lastPlayedStep);
            CompletePlayback(invokeCompleted: true);
        }

        private void BeginPlayback(CutsceneDefinition definition)
        {
            isPlaying = true;
            isTyping = false;
            advanceRequested = false;
            revealRequested = false;
            skipRequested = false;
            skipSectionRequested = false;
            skipHoldElapsed = 0f;
            previousInputBlock = GameModalState.BlocksGameplayInput;
            GameModalState.BlocksGameplayInput = true;
            UIBackStack.Push(this);
            ClearDialogueView();
            HideTransitionImage();
            SetFadeAlpha(startCovered ? 1f : 0f);
            isCurrentStepSkippable = false;
            RefreshSkipHoldVisible();
            SetSkipHoldProgress(0f);
            SetRootVisible(true);
        }

        private IEnumerator ExecuteStep(CutsceneStep step, bool hasPreviousStep)
        {
            StopBackgroundGlitch();
            ClearDialogueView();

            yield return ApplyStepTransition(step, hasPreviousStep);
            StartBackgroundMotion(step);
            StartBackgroundGlitch(step);
            ApplyAudio(step);

            IReadOnlyList<CutsceneDialogue> dialogues = step.Dialogues;
            if (dialogues.Count == 0)
            {
                yield return WaitNoDialogueHold(step.NoDialogueHoldSeconds);
                yield break;
            }

            yield return WaitDialogueStartDelay();

            for (int i = 0; i < dialogues.Count && !skipRequested && !skipSectionRequested; i++)
            {
                CutsceneDialogue dialogue = dialogues[i];
                if (dialogue == null)
                {
                    continue;
                }

                yield return PlayDialogue(dialogue);
            }
        }

        private IEnumerator WaitNoDialogueHold(float seconds)
        {
            for (float elapsed = 0f;
                 elapsed < seconds && !skipRequested && !skipSectionRequested;
                 elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        private IEnumerator ApplyStepTransition(CutsceneStep step, bool hasPreviousStep)
        {
            return step.TransitionMode == CutsceneStepTransitionMode.ImageFade
                ? ApplyImageFadeTransition(step)
                : ApplyBlackFadeTransition(step, hasPreviousStep);
        }

        private IEnumerator ApplyBlackFadeTransition(CutsceneStep step, bool hasPreviousStep)
        {
            float fadeInSeconds = step.FadeInSeconds;
            float fadeOutSeconds = step.FadeOutSeconds;
            float darkHoldSeconds = step.DarkHoldSeconds;
            if (fadeInSeconds <= 0f && fadeOutSeconds <= 0f && darkHoldSeconds <= 0f)
            {
                if (step.BackgroundImage != null)
                {
                    SetImage(backgroundImage, step.BackgroundImage, 1f);
                }

                HideTransitionImage();
                SetFadeAlpha(0f);
                yield break;
            }

            if (hasPreviousStep)
            {
                yield return PlayFade(0f, 1f, fadeOutSeconds);
            }
            else if (fadeInSeconds > 0f || darkHoldSeconds > 0f)
            {
                SetFadeAlpha(1f);
            }

            if (step.BackgroundImage != null)
            {
                SetImage(backgroundImage, step.BackgroundImage, 1f);
            }
            HideTransitionImage();

            if (darkHoldSeconds > 0f)
            {
                yield return WaitDarkHold(darkHoldSeconds);
            }

            if (fadeInSeconds > 0f)
            {
                yield return PlayFade(1f, 0f, fadeInSeconds);
                yield break;
            }

            SetFadeAlpha(0f);
        }

        private IEnumerator ApplyImageFadeTransition(CutsceneStep step)
        {
            SetFadeAlpha(0f);
            float fadeSeconds = step.FadeInSeconds;
            Sprite nextSprite = step.BackgroundImage;

            if (nextSprite == null)
            {
                HideTransitionImage();
                yield break;
            }

            if (fadeSeconds > 0f)
            {
                yield return PlayImageFadeIn(nextSprite, fadeSeconds);
                yield break;
            }

            SetImage(backgroundImage, nextSprite, 1f);
            HideTransitionImage();
        }

        private IEnumerator PlayImageFadeIn(Sprite nextSprite, float fadeSeconds)
        {
            Image transitionImage = EnsureBackgroundTransitionImage();
            if (transitionImage != null && transitionImage != backgroundImage)
            {
                if (backgroundImage != null && backgroundImage.sprite != null)
                {
                    SetImageAlpha(backgroundImage, 1f);
                }

                SetImage(transitionImage, nextSprite, 0f);
                yield return PlayImageAlpha(transitionImage, 0f, 1f, fadeSeconds);
                SetImage(backgroundImage, nextSprite, 1f);
                HideTransitionImage();
                yield break;
            }

            SetImage(backgroundImage, nextSprite, 0f);
            yield return PlayImageAlpha(backgroundImage, 0f, 1f, fadeSeconds);
        }

        private IEnumerator PlayFade(float fromAlpha, float toAlpha, float seconds)
        {
            float duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                SetFadeAlpha(toAlpha);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration && !skipRequested && !skipSectionRequested; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                SetFadeAlpha(Mathf.Lerp(fromAlpha, toAlpha, progress));
                yield return null;
            }

            SetFadeAlpha(toAlpha);
        }

        private IEnumerator WaitDarkHold(float seconds)
        {
            for (float elapsed = 0f;
                 elapsed < seconds && !skipRequested && !skipSectionRequested;
                 elapsed += Time.unscaledDeltaTime)
            {
                SetFadeAlpha(1f);
                yield return null;
            }
        }

        private IEnumerator CoverScreenForCompletion(CutsceneStep lastPlayedStep)
        {
            ClearDialogueView();

            if (skipRequested)
            {
                SetFadeAlpha(1f);
                yield break;
            }

            float fadeSeconds = lastPlayedStep != null ? lastPlayedStep.FadeOutSeconds : 0f;
            if (fadeSeconds <= 0f)
            {
                SetFadeAlpha(1f);
                yield break;
            }

            yield return PlayFade(0f, 1f, fadeSeconds);
        }

        private void ApplyAudio(CutsceneStep step)
        {
            if (!string.IsNullOrWhiteSpace(step.BgmId))
            {
                SoundManager.PlayBgm(step.BgmId, step.BgmFadeSeconds);
            }
        }

        private IEnumerator PlayDialogue(CutsceneDialogue dialogue)
        {
            if (dialoguePanelView == null)
            {
                Debug.LogWarning($"{nameof(CutscenePlayer)} requires {nameof(CutsceneDialoguePanelView)}.", this);
                yield break;
            }

            advanceRequested = false;
            revealRequested = false;
            isTyping = true;
            PlayDialogueSfx(dialogue.SfxId);

            string speakerName = dialogue.HasLocalizedName ? dialogue.LocalizedName.GetLocalizedString() : dialogue.Name;
            string dialogueText = dialogue.HasLocalizedText ? dialogue.LocalizedText.GetLocalizedString() : dialogue.Text;
            dialoguePanelView?.ShowLine(speakerName, dialogueText);
            yield return dialoguePanelView?.PlayTypewriter(dialogueText, () => revealRequested || skipRequested || skipSectionRequested, () => skipRequested || skipSectionRequested);
            isTyping = false;
            revealRequested = false;
            advanceRequested = false;

            while (!advanceRequested && !skipRequested && !skipSectionRequested)
            {
                yield return null;
            }

            advanceRequested = false;
        }

        private static void PlayDialogueSfx(string sfxId)
        {
            if (!string.IsNullOrWhiteSpace(sfxId))
            {
                SoundManager.PlaySfx(sfxId);
            }
        }

        private IEnumerator WaitDialogueStartDelay()
        {
            for (float elapsed = 0f;
                 elapsed < dialogueStartDelaySeconds && !skipRequested && !skipSectionRequested;
                 elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        private IEnumerator PlayImageAlpha(Image image, float fromAlpha, float toAlpha, float seconds)
        {
            if (image == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0f, seconds);
            if (duration <= 0f)
            {
                SetImageAlpha(image, toAlpha);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration && !skipRequested && !skipSectionRequested; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                SetImageAlpha(image, Mathf.Lerp(fromAlpha, toAlpha, progress));
                yield return null;
            }

            SetImageAlpha(image, toAlpha);
        }

        private void StartBackgroundMotion(CutsceneStep step)
        {
            StopBackgroundMotion();

            RectTransform target = GetBackgroundMotionTarget();
            if (target == null)
            {
                return;
            }

            ApplyBackgroundTransform(target, step.StartOffset, step.StartZoom);
            if (step.HasBackgroundMotion)
            {
                motionRoutine = StartCoroutine(PlayBackgroundMotion(step, target));
            }
        }

        private IEnumerator PlayBackgroundMotion(CutsceneStep step, RectTransform target)
        {
            float duration = Mathf.Max(0f, step.MotionSeconds);
            if (duration <= 0f)
            {
                ApplyBackgroundTransform(target, step.EndOffset, step.EndZoom);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration && !skipRequested && !skipSectionRequested; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                Vector2 offset = Vector2.LerpUnclamped(step.StartOffset, step.EndOffset, progress);
                float zoom = Mathf.LerpUnclamped(step.StartZoom, step.EndZoom, progress);
                ApplyBackgroundTransform(target, offset, zoom);
                yield return null;
            }

            ApplyBackgroundTransform(target, step.EndOffset, step.EndZoom);
            motionRoutine = null;
        }

        private void StopBackgroundMotion()
        {
            if (motionRoutine == null)
            {
                return;
            }

            StopCoroutine(motionRoutine);
            motionRoutine = null;
        }

        private void StartBackgroundGlitch(CutsceneStep step)
        {
            StopBackgroundGlitch();
            if (step == null || !step.HasBackgroundGlitch || backgroundImage == null)
            {
                return;
            }

            backgroundGlitchBaseSprite = backgroundImage.sprite;
            if (backgroundGlitchBaseSprite == null)
            {
                return;
            }

            backgroundGlitchRoutine = StartCoroutine(PlayBackgroundGlitch(step));
        }

        private IEnumerator PlayBackgroundGlitch(CutsceneStep step)
        {
            IReadOnlyList<Sprite> glitchFrames = step.GlitchFrames;
            int previousFrameIndex = -1;

            while (!skipRequested && !skipSectionRequested)
            {
                SetImage(backgroundImage, backgroundGlitchBaseSprite, 1f);
                yield return WaitForBackgroundGlitchSeconds(
                    UnityEngine.Random.Range(step.GlitchHoldMinSeconds, step.GlitchHoldMaxSeconds));

                if (skipRequested || skipSectionRequested)
                {
                    break;
                }

                int frameCount = UnityEngine.Random.Range(step.GlitchBurstMinFrames, step.GlitchBurstMaxFrames + 1);
                for (int i = 0; i < frameCount && !skipRequested && !skipSectionRequested; i++)
                {
                    Sprite frame = GetNextGlitchFrame(glitchFrames, ref previousFrameIndex);
                    if (frame != null)
                    {
                        SetImage(backgroundImage, frame, 1f);
                    }

                    yield return WaitForBackgroundGlitchSeconds(
                        UnityEngine.Random.Range(step.GlitchFrameMinSeconds, step.GlitchFrameMaxSeconds));
                }
            }

            SetImage(backgroundImage, backgroundGlitchBaseSprite, 1f);
            backgroundGlitchRoutine = null;
        }

        private IEnumerator WaitForBackgroundGlitchSeconds(float seconds)
        {
            for (float elapsed = 0f;
                 elapsed < seconds && !skipRequested && !skipSectionRequested;
                 elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        private static Sprite GetNextGlitchFrame(IReadOnlyList<Sprite> frames, ref int previousFrameIndex)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            int startIndex = UnityEngine.Random.Range(0, frames.Count);
            for (int i = 0; i < frames.Count; i++)
            {
                int index = (startIndex + i) % frames.Count;
                if (index != previousFrameIndex && frames[index] != null)
                {
                    previousFrameIndex = index;
                    return frames[index];
                }
            }

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null)
                {
                    previousFrameIndex = i;
                    return frames[i];
                }
            }

            return null;
        }

        private void StopBackgroundGlitch()
        {
            if (backgroundGlitchRoutine != null)
            {
                StopCoroutine(backgroundGlitchRoutine);
                backgroundGlitchRoutine = null;
            }

            if (backgroundGlitchBaseSprite != null)
            {
                SetImage(backgroundImage, backgroundGlitchBaseSprite, 1f);
                backgroundGlitchBaseSprite = null;
            }
        }

        private RectTransform GetBackgroundMotionTarget()
        {
            if (backgroundMotionTarget != null)
            {
                return backgroundMotionTarget;
            }

            return backgroundImage != null ? backgroundImage.rectTransform : null;
        }

        private static void ApplyBackgroundTransform(RectTransform target, Vector2 offset, float zoom)
        {
            target.anchoredPosition = offset;
            target.localScale = Vector3.one * Mathf.Max(0.01f, zoom);
        }

        private void UpdateSkipHold()
        {
            if (!CanSkipCurrentCutscene())
            {
                skipHoldElapsed = 0f;
                SetSkipHoldProgress(0f);
                RefreshSkipHoldVisible();
                return;
            }

            if (SkipHeld())
            {
                skipHoldElapsed += Time.unscaledDeltaTime;
                if (skipHoldElapsed >= skipHoldSeconds)
                {
                    Skip();
                }
            }
            else
            {
                skipHoldElapsed = 0f;
            }

            SetSkipHoldProgress(skipHoldElapsed / skipHoldSeconds);
            RefreshSkipHoldVisible();
        }

        private void StopPlayback(bool invokeCompleted)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            StopBackgroundMotion();
            StopBackgroundGlitch();

            if (isPlaying)
            {
                CompletePlayback(invokeCompleted);
            }
        }

        private bool CanSkipCurrentCutscene()
        {
            return currentDefinition != null
                && canUseSkip
                && (skipWholeCutscene || isCurrentStepSkippable);
        }

        private void SetCurrentStep(CutsceneStep step)
        {
            isCurrentStepSkippable = step != null && step.Skippable;
            skipHoldElapsed = 0f;
            SetSkipHoldProgress(0f);
            RefreshSkipHoldVisible();
        }

        private void RefreshSkipHoldVisible()
        {
            SetSkipHoldVisible(CanSkipCurrentCutscene());
        }

        private static int FindNextNonSkippableStepIndex(IReadOnlyList<CutsceneStep> steps, int startIndex)
        {
            for (int i = Mathf.Max(0, startIndex); i < steps.Count; i++)
            {
                if (steps[i] != null && !steps[i].Skippable)
                {
                    return i;
                }
            }

            return steps.Count;
        }

        private void CompletePlayback(bool invokeCompleted)
        {
            StopBackgroundGlitch();
            Action callback = completed;
            bool keepCovered = keepCoveredOnCompleted;
            playRoutine = null;
            currentDefinition = null;
            completed = null;
            isPlaying = false;
            isTyping = false;
            advanceRequested = false;
            revealRequested = false;
            skipRequested = false;
            skipSectionRequested = false;
            skipHoldElapsed = 0f;
            startCovered = false;

            UIBackStack.Remove(this);
            GameModalState.BlocksGameplayInput = previousInputBlock;

            if (invokeCompleted)
            {
                callback?.Invoke();
                if (callback != null && keepCovered)
                {
                    return;
                }
            }

            SetRootVisible(false);
            ClearDialogueView();
            HideTransitionImage();
            SetFadeAlpha(0f);
            SetSkipHoldVisible(false);
            SetSkipHoldProgress(0f);
        }

        private void SetRootVisible(bool visible)
        {
            if (cutsceneRoot != null)
            {
                cutsceneRoot.SetActive(visible);
            }
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            SetImage(image, sprite, 1f);
        }

        private static void SetImage(Image image, Sprite sprite, float alpha)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
            SetImageAlpha(image, sprite != null ? alpha : 0f);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        private void HideTransitionImage()
        {
            SetImage(backgroundTransitionImage, null, 0f);
        }

        private Image EnsureBackgroundTransitionImage()
        {
            if (backgroundTransitionImage != null && backgroundTransitionImage != backgroundImage)
            {
                return backgroundTransitionImage;
            }

            if (backgroundImage == null || backgroundImage.transform.parent == null)
            {
                return backgroundTransitionImage;
            }

            GameObject transitionObject = new("Background_TransitionImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            transitionObject.layer = backgroundImage.gameObject.layer;

            RectTransform sourceRect = backgroundImage.rectTransform;
            RectTransform transitionRect = transitionObject.GetComponent<RectTransform>();
            transitionRect.SetParent(sourceRect.parent, false);
            CopyRectTransform(sourceRect, transitionRect);
            transitionRect.SetSiblingIndex(sourceRect.GetSiblingIndex() + 1);

            Image transitionImage = transitionObject.GetComponent<Image>();
            CopyImageSettings(backgroundImage, transitionImage);
            transitionImage.raycastTarget = false;
            backgroundTransitionImage = transitionImage;
            HideTransitionImage();
            return backgroundTransitionImage;
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyImageSettings(Image source, Image target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillAmount = source.fillAmount;
            target.fillClockwise = source.fillClockwise;
            target.fillOrigin = source.fillOrigin;
            target.useSpriteMesh = source.useSpriteMesh;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.maskable = source.maskable;
        }

        private void ClearDialogueView()
        {
            dialoguePanelView?.Hide();
        }

        private void ResolveDialoguePanelView()
        {
            if (dialoguePanelView == null)
            {
                dialoguePanelView = GetComponentInChildren<CutsceneDialoguePanelView>(true);
            }
        }

        private void SetFadeAlpha(float alpha)
        {
            if (fadeGroup != null)
            {
                fadeGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void SetSkipHoldVisible(bool visible)
        {
            if (skipHoldFillImage != null)
            {
                skipHoldFillImage.gameObject.SetActive(visible);
            }

            if (skipHoldPromptText != null)
            {
                skipHoldPromptText.gameObject.SetActive(visible);
            }
        }

        private void SetSkipHoldProgress(float progress)
        {
            if (skipHoldFillImage != null)
            {
                skipHoldFillImage.fillAmount = Mathf.Clamp01(progress);
            }
        }

        private static bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return (keyboard != null
                    && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetMouseButtonDown(0);
#endif
        }

        private static bool SkipHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.isPressed;
#else
            return Input.GetKey(KeyCode.Escape);
#endif
        }
    }
}
