using System.Collections;
using TMPro;
using UnityEngine;
using Week14.Bootstrap;

namespace Week14.UI
{
    public sealed class LocationIntroView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private string locationName = "훈련장";
        [SerializeField] private TMP_Text locationNameText;
        [SerializeField] private RectTransform[] flyObjects = System.Array.Empty<RectTransform>();

        [Header("Root")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool waitForSceneTransition = true;
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.15f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float flyOffsetX = 1200f;
        [SerializeField, Min(0f)] private float flyInSeconds = 0.35f;
        [SerializeField, Min(0f)] private float flyOutSeconds = 0.32f;
        [SerializeField, Min(0f)] private float objectStaggerSeconds = 0.045f;
        [SerializeField] private AnimationCurve flyInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve flyOutCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Text")]
        [SerializeField, Min(0f)] private float textFadeInSeconds = 0.18f;
        [SerializeField, Min(0f)] private float textHoldSeconds = 0.85f;
        [SerializeField, Min(0f)] private float textFadeOutSeconds = 0.45f;

        private Vector2[] targetPositions = System.Array.Empty<Vector2>();
        private Coroutine playRoutine;

        private void Awake()
        {
            ResolveCanvasGroup();
            CacheTargetPositions();
            SetVisible(false);
            SetTextAlpha(0f);
        }

        private void Start()
        {
            if (playOnStart)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
        }

        public void SetLocationName(string value)
        {
            locationName = value ?? string.Empty;
            SetText(locationName);
        }

        public void Play()
        {
            Play(locationName);
        }

        public void Play(string nextLocationName)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            locationName = nextLocationName ?? string.Empty;
            playRoutine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            if (waitForSceneTransition)
            {
                while (SceneTransition.IsTransitioning)
                {
                    yield return null;
                }
            }

            yield return WaitUnscaled(startDelaySeconds);

            ResolveCanvasGroup();
            CacheTargetPositions();
            SetText(locationName);
            SetTextAlpha(0f);
            SetObjectsAtOffset(-flyOffsetX);
            SetVisible(true);

            yield return AnimateObjects(-flyOffsetX, 0f, flyInSeconds, objectStaggerSeconds, flyInCurve);
            yield return FadeText(0f, 1f, textFadeInSeconds);
            yield return WaitUnscaled(textHoldSeconds);
            yield return FadeText(1f, 0f, textFadeOutSeconds);
            yield return AnimateObjects(0f, flyOffsetX, flyOutSeconds, objectStaggerSeconds, flyOutCurve);

            SetVisible(false);
            playRoutine = null;
        }

        private IEnumerator AnimateObjects(
            float fromOffsetX,
            float toOffsetX,
            float duration,
            float staggerSeconds,
            AnimationCurve curve)
        {
            int objectCount = flyObjects != null ? flyObjects.Length : 0;
            if (objectCount == 0)
            {
                yield break;
            }

            float totalSeconds = Mathf.Max(0f, duration) + Mathf.Max(0f, staggerSeconds) * (objectCount - 1);
            if (totalSeconds <= 0f)
            {
                SetObjectsAtOffset(toOffsetX);
                yield break;
            }

            for (float elapsed = 0f; elapsed < totalSeconds; elapsed += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < objectCount; i++)
                {
                    RectTransform target = flyObjects[i];
                    if (target == null || i >= targetPositions.Length)
                    {
                        continue;
                    }

                    float localElapsed = elapsed - staggerSeconds * i;
                    float progress = duration <= 0f ? 1f : Mathf.Clamp01(localElapsed / duration);
                    float eased = EvaluateCurve(curve, progress);
                    SetObjectOffset(target, targetPositions[i], Mathf.LerpUnclamped(fromOffsetX, toOffsetX, eased));
                }

                yield return null;
            }

            SetObjectsAtOffset(toOffsetX);
        }

        private IEnumerator FadeText(float fromAlpha, float toAlpha, float duration)
        {
            if (locationNameText == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                SetTextAlpha(toAlpha);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                SetTextAlpha(Mathf.Lerp(fromAlpha, toAlpha, progress));
                yield return null;
            }

            SetTextAlpha(toAlpha);
        }

        private void CacheTargetPositions()
        {
            int objectCount = flyObjects != null ? flyObjects.Length : 0;
            if (targetPositions.Length != objectCount)
            {
                targetPositions = new Vector2[objectCount];
            }

            for (int i = 0; i < objectCount; i++)
            {
                targetPositions[i] = flyObjects[i] != null ? flyObjects[i].anchoredPosition : Vector2.zero;
            }
        }

        private void SetObjectsAtOffset(float offsetX)
        {
            int objectCount = flyObjects != null ? flyObjects.Length : 0;
            for (int i = 0; i < objectCount; i++)
            {
                RectTransform target = flyObjects[i];
                if (target == null || i >= targetPositions.Length)
                {
                    continue;
                }

                SetObjectOffset(target, targetPositions[i], offsetX);
            }
        }

        private static void SetObjectOffset(RectTransform target, Vector2 origin, float offsetX)
        {
            target.anchoredPosition = origin + Vector2.right * offsetX;
        }

        private void SetVisible(bool visible)
        {
            ResolveCanvasGroup();
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void SetText(string value)
        {
            if (locationNameText != null)
            {
                locationNameText.text = value ?? string.Empty;
            }
        }

        private void SetTextAlpha(float alpha)
        {
            if (locationNameText == null)
            {
                return;
            }

            Color color = locationNameText.color;
            color.a = Mathf.Clamp01(alpha);
            locationNameText.color = color;
        }

        private void ResolveCanvasGroup()
        {
            if (canvasGroup != null)
            {
                return;
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private static float EvaluateCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }
    }
}
