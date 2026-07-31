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
        [Tooltip("CanvasGroup alpha가 이 값 이하가 되면 UI 입력 차단을 해제합니다.")]
        [SerializeField, Range(0f, 1f)] private float unblockRaycastsAtAlpha = 0.5f;

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

            // fadeRoutine이 이미 있다면(다른 스크립트의 Awake에서 이 OnEnable보다 먼저
            // BeginFadeOut()이 중첩 호출된 경우) 그 진행 중인 페이드를 건드리지 않는다.
            if (canvasGroup == null || fadeRoutine != null)
            {
                return;
            }

            // 검은 화면만 미리 켜두고, 실제로 옅어지기 시작하는 건 BeginFadeOut()이 호출된 뒤부터다
            // (언어 설정/로그 수집 동의 패널이 있다면 그 패널들이 다 닫힌 뒤에 호출해준다).
            PrepareBlackout();
        }

        private void OnDisable()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }
        }

        // 언어 설정/로그 수집 동의 패널이 없거나 이미 다 응답된 상태라면 곧바로 호출해서 즉시 페이드아웃을
        // 시작시키고, 패널이 떴다면 그 패널들이 닫힌 시점에 호출해서 그제서야 타이틀 화면이 드러나게 한다.
        // 호출하는 쪽의 Awake가 이 컴포넌트의 Awake보다 먼저 실행될 수도 있으므로 여기서도 직접 참조를 보장한다.
        public void BeginFadeOut()
        {
            ResolveCanvasGroup();

            if (canvasGroup == null || fadeRoutine != null)
            {
                return;
            }

            fadeRoutine = StartCoroutine(FadeInRoutine());
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
                float alpha = 1f - easedRatio;
                SetAlpha(alpha);

                if (blockRaycastsWhileVisible && alpha <= unblockRaycastsAtAlpha)
                {
                    SetBlocksRaycasts(false);
                }

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
