using System.Collections;
using UnityEngine;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TitleFadeInPanel : MonoBehaviour
    {
        [Tooltip("페이드 인에 사용할 CanvasGroup입니다. 비워두면 이 오브젝트의 CanvasGroup을 사용합니다.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("처음 검은 화면을 유지할 시간입니다. 이 시간이 지난 뒤 alpha가 줄어들기 시작합니다.")]
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.5f;
        [Tooltip("검은 화면이 완전히 사라질 때까지 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 1.2f;
        [Tooltip("일시정지나 timeScale의 영향을 받지 않고 페이드 인을 진행할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("검은 화면이 보이는 동안 UI 입력을 막을지 여부입니다.")]
        [SerializeField] private bool blockRaycastsWhileVisible = true;

        private Coroutine fadeRoutine;

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            ResolveCanvasGroup();
        }

        private void OnEnable()
        {
            ResolveCanvasGroup();

            if (canvasGroup == null)
            {
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            PrepareBlackout();
            fadeRoutine = StartCoroutine(FadeInRoutine());
        }

        private void OnDisable()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

        private void ResolveCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private IEnumerator FadeInRoutine()
        {
            PrepareBlackout();

            // Play 진입 프레임의 큰 deltaTime 때문에 대기가 건너뛰지 않도록 한 프레임 고정합니다.
            yield return null;

            if (startDelaySeconds > 0f)
            {
                float fadeStartTime = GetTime() + startDelaySeconds;
                while (GetTime() < fadeStartTime)
                {
                    yield return null;
                }
            }

            float fadeEndTime = GetTime() + fadeSeconds;
            while (GetTime() < fadeEndTime)
            {
                float ratio = 1f - Mathf.Clamp01((fadeEndTime - GetTime()) / fadeSeconds);
                float easedRatio = Mathf.SmoothStep(0f, 1f, ratio);
                SetAlpha(1f - easedRatio);
                yield return null;
            }

            SetAlpha(0f);
            SetBlocksRaycasts(false);
            fadeRoutine = null;
        }

        private void PrepareBlackout()
        {
            SetAlpha(1f);
            SetBlocksRaycasts(blockRaycastsWhileVisible);
        }

        private void SetAlpha(float alpha)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
            canvasGroup.interactable = false;
        }

        private void SetBlocksRaycasts(bool blocksRaycasts)
        {
            canvasGroup.blocksRaycasts = blocksRaycasts;
        }

        private float GetTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }
    }
}
