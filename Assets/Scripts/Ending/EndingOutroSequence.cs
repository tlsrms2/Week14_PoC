using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Ending
{
    public sealed class EndingOutroSequence : MonoBehaviour
    {
        [Header("Fade / Scroll")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform introBlock;
        [SerializeField] private RectTransform outroBlock;
        [SerializeField, Min(0f)] private float fadeInDuration = 1f;
        [SerializeField, Min(0f)] private float scrollSpeed = 60f;
        [SerializeField, Min(0f)] private float introHoldSeconds = 0f;
        [SerializeField, Min(0f)] private float outroHoldSeconds = 0f;

        [Header("Skip Hold (Esc)")]
        [SerializeField] private Image skipHoldFillImage;
        [SerializeField] private TMP_Text skipHoldPromptText;
        [SerializeField, Min(0.1f)] private float skipHoldSeconds = 1.2f;

        private Coroutine playRoutine;
        private Action onComplete;
        private bool playing;
        private bool completed;
        private float skipHoldElapsed;
        private float startY;
        private float endY;

        private void Awake()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }

        public void Play(Action onComplete)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            this.onComplete = onComplete;
            playing = true;
            completed = false;
            skipHoldElapsed = 0f;
            SetSkipHoldProgress(0f);
            SetSkipHoldVisible(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            float viewportHeight = viewport != null ? viewport.rect.height : 0f;
            float contentHeight = content.rect.height;
            float introHeight = introBlock != null ? introBlock.rect.height : 0f;
            float outroHeight = outroBlock != null ? outroBlock.rect.height : 0f;

            // content는 top-anchor/top-pivot 기준. anchoredPosition.y == 0이면 content 상단이 뷰포트 상단과 일치.
            // introBlock(ThanksTo)이 뷰포트 정중앙에 오는 y
            startY = (introHeight - viewportHeight) * 0.5f;
            // outroBlock(마지막 블록)이 뷰포트 정중앙에 오는 y
            endY = contentHeight - (outroHeight + viewportHeight) * 0.5f;

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, startY);

            playRoutine = StartCoroutine(Run());
        }

        public void Skip()
        {
            if (!playing || completed)
            {
                return;
            }

            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            content.anchoredPosition = new Vector2(content.anchoredPosition.x, endY);
            Complete();
        }

        private void Update()
        {
            if (!playing || completed)
            {
                return;
            }

            UpdateSkipHold();
        }

        private IEnumerator Run()
        {
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                }
                yield return null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }

            if (introHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(introHoldSeconds);
            }

            float y = startY;
            while (y < endY)
            {
                y += scrollSpeed * Time.unscaledDeltaTime;
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Min(y, endY));
                yield return null;
            }

            if (outroHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(outroHoldSeconds);
            }

            playRoutine = null;
            Complete();
        }

        private void Complete()
        {
            playing = false;
            completed = true;
            SetSkipHoldVisible(false);
            onComplete?.Invoke();
        }

        private void UpdateSkipHold()
        {
            if (SkipHeld())
            {
                skipHoldElapsed += Time.unscaledDeltaTime;
                if (skipHoldElapsed >= skipHoldSeconds)
                {
                    Skip();
                    return;
                }
            }
            else
            {
                skipHoldElapsed = 0f;
            }

            SetSkipHoldProgress(skipHoldElapsed / skipHoldSeconds);
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
