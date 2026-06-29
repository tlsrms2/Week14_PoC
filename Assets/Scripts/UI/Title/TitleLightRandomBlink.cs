using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class TitleLightRandomBlink : MonoBehaviour
    {
        [Header("대상")]
        [Tooltip("무작위로 깜빡일 Light2D입니다. 비워두면 이 오브젝트의 Light2D를 사용합니다.")]
        [SerializeField] private Light2D targetLight;

        [Header("Intensity")]
        [Tooltip("깜빡임 중 사용할 Intensity 범위입니다.")]
        [SerializeField] private Vector2 intensityRange = new Vector2(1f, 1.5f);
        [Tooltip("비활성화될 때 시작 Intensity로 되돌릴지 여부입니다.")]
        [SerializeField] private bool restoreInitialIntensityOnDisable = true;

        [Header("타이밍")]
        [Tooltip("다음 목표 Intensity까지 이동하는 시간 범위입니다.")]
        [SerializeField] private Vector2 transitionSecondsRange = new Vector2(0.18f, 0.55f);
        [Tooltip("목표 Intensity에 도달한 뒤 잠깐 머무르는 시간 범위입니다.")]
        [SerializeField] private Vector2 holdSecondsRange = new Vector2(0.04f, 0.22f);
        [Tooltip("Intensity가 변하는 형태입니다. X는 진행률, Y는 보간 비율입니다.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("일시정지나 Time.timeScale의 영향을 받지 않고 계속 진행할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("활성화되면 자동으로 blink를 시작할지 여부입니다.")]
        [SerializeField] private bool playOnEnable = true;

        private Coroutine blinkRoutine;
        private float initialIntensity;

        private void Awake()
        {
            ResolveTarget();

            if (targetLight != null)
            {
                initialIntensity = targetLight.intensity;
            }
        }

        private void OnEnable()
        {
            ResolveTarget();

            if (targetLight != null)
            {
                initialIntensity = targetLight.intensity;
            }

            if (playOnEnable)
            {
                StartBlinking();
            }
        }

        private void OnDisable()
        {
            StopBlinking();

            if (restoreInitialIntensityOnDisable && targetLight != null)
            {
                targetLight.intensity = initialIntensity;
            }
        }

        public void StartBlinking()
        {
            if (blinkRoutine != null || targetLight == null)
            {
                return;
            }

            blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        public void StopBlinking()
        {
            if (blinkRoutine == null)
            {
                return;
            }

            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        private IEnumerator BlinkRoutine()
        {
            while (true)
            {
                float startIntensity = targetLight.intensity;
                float targetIntensity = GetRandomRange(intensityRange);
                float transitionSeconds = GetRandomRange(transitionSecondsRange);

                yield return TransitionTo(startIntensity, targetIntensity, transitionSeconds);
                yield return WaitForSeconds(GetRandomRange(holdSecondsRange));
            }
        }

        private IEnumerator TransitionTo(float startIntensity, float targetIntensity, float seconds)
        {
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float ratio = Mathf.Clamp01(elapsed / duration);
                float easedRatio = transitionCurve != null ? Mathf.Clamp01(transitionCurve.Evaluate(ratio)) : ratio;
                targetLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, easedRatio);
                yield return null;
            }

            targetLight.intensity = targetIntensity;
        }

        private void ResolveTarget()
        {
            if (targetLight == null)
            {
                targetLight = GetComponent<Light2D>();
            }
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private object WaitForSeconds(float seconds)
        {
            float clampedSeconds = Mathf.Max(0f, seconds);
            return useUnscaledTime ? new WaitForSecondsRealtime(clampedSeconds) : new WaitForSeconds(clampedSeconds);
        }

        private static float GetRandomRange(Vector2 range)
        {
            return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }
    }
}
