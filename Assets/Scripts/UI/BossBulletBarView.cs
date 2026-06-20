using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;

namespace Week14.UI
{
    public sealed class BossBulletBarView : MonoBehaviour
    {
        [Tooltip("씬에 이미 배치된, 실제 남은 탄환만큼 즉시 채워지는 본체 이미지입니다. Image Type이 Filled여야 합니다.")]
        [SerializeField] private Image foregroundImage;
        [Tooltip("씬에 이미 배치된, 빨리 따라오는 잔상 이미지입니다. Image Type이 Filled여야 합니다.")]
        [SerializeField] private Image fastGhostImage;
        [Tooltip("씬에 이미 배치된, 천천히 따라오는 잔상 이미지입니다. Image Type이 Filled여야 합니다.")]
        [SerializeField] private Image slowGhostImage;

        [SerializeField] private BulletGauge target;
        [SerializeField] private Color normalColor = new(1f, 0.55f, 0.1f);
        [SerializeField] private Color emptyColor = Color.red;
        [Tooltip("빨리 따라오는 잔상이 실제 탄환만큼 줄어드는 속도입니다. 값이 클수록 빠르게 줄어듭니다.")]
        [SerializeField, Min(0.1f)] private float fastGhostShrinkSpeed = 14f;
        [Tooltip("천천히 따라오는 잔상이 실제 탄환만큼 줄어드는 속도입니다. 값이 클수록 빠르게 줄어듭니다.")]
        [SerializeField, Min(0.1f)] private float slowGhostShrinkSpeed = 4f;
        [Tooltip("총알이 0이 되어 처형 가능 상태일 때 체력바 색입니다.")]
        [SerializeField] private Color executionWindowColor = new(0.95f, 0.05f, 0.05f, 1f);
        [Tooltip("처형 가능 상태에서 체력바가 깜빡이는 속도입니다.")]
        [SerializeField, Min(0.1f)] private float executionWindowBlinkSpeed = 6f;
        [Tooltip("처형 가능 상태에서 깜빡일 때 가장 어두워지는 알파값입니다.")]
        [SerializeField, Range(0f, 1f)] private float executionWindowBlinkMinAlpha = 0.35f;
        [Tooltip("처형의 마지막 발을 쏘는 순간, 남아있는 그로기 체력바가 0으로 줄어드는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float executionDrainSeconds = 0.2f;
        [Tooltip("다음 페이즈로 넘어갈 때 체력바가 다시 가득 차오르는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float phaseRefillSeconds = 0.6f;

        private float currentRatio;
        private float fastGhostRatio;
        private float slowGhostRatio;
        private bool executionWindowActive;
        private int displayedBulletCount = -1;
        private Coroutine ratioTransitionRoutine;

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            TickEffects();
        }

        public void SetTarget(BulletGauge nextTarget)
        {
            if (target == nextTarget)
            {
                return;
            }

            Unsubscribe();
            target = nextTarget;
            Subscribe();
            Refresh();
        }

        public void SetExecutionWindow(bool active, float remainingRatio)
        {
            executionWindowActive = active;
            if (!active)
            {
                return;
            }

            currentRatio = Mathf.Clamp01(remainingRatio);
            fastGhostRatio = currentRatio;
            slowGhostRatio = currentRatio;
            SetFillAmount(foregroundImage, currentRatio);
            SetFillAmount(fastGhostImage, fastGhostRatio);
            SetFillAmount(slowGhostImage, slowGhostRatio);
        }

        public void ClearExecutionWindow()
        {
            StopRatioTransition();

            if (!executionWindowActive)
            {
                return;
            }

            executionWindowActive = false;
            displayedBulletCount = -1;

            if (foregroundImage != null)
            {
                foregroundImage.color = ResolveBarColor(target != null ? target.CurrentBullets : 0, target != null ? target.MaxBullets : 1);
            }

            Refresh();
        }

        public void PlayExecutionDrain()
        {
            executionWindowActive = true;
            StopRatioTransition();
            ratioTransitionRoutine = StartCoroutine(AnimateRatio(currentRatio, 0f, executionDrainSeconds, true));
        }

        public void PlayPhaseRefill()
        {
            executionWindowActive = false;
            displayedBulletCount = -1;
            StopRatioTransition();
            ratioTransitionRoutine = StartCoroutine(AnimateRatio(currentRatio, 1f, phaseRefillSeconds, false));
        }

        private void StopRatioTransition()
        {
            if (ratioTransitionRoutine != null)
            {
                StopCoroutine(ratioTransitionRoutine);
                ratioTransitionRoutine = null;
            }
        }

        private IEnumerator AnimateRatio(float from, float to, float durationSeconds, bool blinkWhileAnimating)
        {
            float duration = Mathf.Max(0.01f, durationSeconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float ratio = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                ApplyRatio(ratio, blinkWhileAnimating);
                yield return null;
            }

            ApplyRatio(to, blinkWhileAnimating);

            if (!blinkWhileAnimating)
            {
                executionWindowActive = false;
                displayedBulletCount = target != null ? Mathf.Max(0, target.CurrentBullets) : -1;
            }

            ratioTransitionRoutine = null;
        }

        private void ApplyRatio(float ratio, bool useExecutionColor)
        {
            currentRatio = ratio;
            fastGhostRatio = ratio;
            slowGhostRatio = ratio;
            SetFillAmount(foregroundImage, ratio);
            SetFillAmount(fastGhostImage, ratio);
            SetFillAmount(slowGhostImage, ratio);

            if (foregroundImage != null && !useExecutionColor)
            {
                foregroundImage.color = ResolveBarColor(ratio, 1f);
            }
        }

        private void Subscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed += HandleChanged;
        }

        private void Unsubscribe()
        {
            if (target == null)
            {
                return;
            }

            target.Changed -= HandleChanged;
        }

        private void HandleChanged(int current, int max)
        {
            SetFilledValue(current, max);
        }

        private void Refresh()
        {
            if (target == null)
            {
                SetFilledValue(0, 1);
                return;
            }

            SetFilledValue(target.CurrentBullets, target.MaxBullets);
        }

        private void SetFilledValue(int current, int max)
        {
            if (executionWindowActive)
            {
                return;
            }

            bool hasPreviousValue = displayedBulletCount >= 0;
            int bulletCount = Mathf.Max(0, current);
            float ratio = Mathf.Clamp01(bulletCount / (float)Mathf.Max(1, max));

            currentRatio = ratio;
            if (foregroundImage != null)
            {
                foregroundImage.color = ResolveBarColor(current, max);
            }

            SetFillAmount(foregroundImage, currentRatio);

            if (!hasPreviousValue || ratio > fastGhostRatio)
            {
                fastGhostRatio = ratio;
            }

            if (!hasPreviousValue || ratio > slowGhostRatio)
            {
                slowGhostRatio = ratio;
            }

            SetFillAmount(fastGhostImage, fastGhostRatio);
            SetFillAmount(slowGhostImage, slowGhostRatio);
            displayedBulletCount = bulletCount;
        }

        private void TickEffects()
        {
            float deltaTime = Time.unscaledDeltaTime;

            if (executionWindowActive)
            {
                TickExecutionWindowBlink();
                return;
            }

            if (fastGhostImage != null && fastGhostRatio > currentRatio)
            {
                fastGhostRatio = Mathf.Max(currentRatio, Mathf.Lerp(fastGhostRatio, currentRatio, 1f - Mathf.Exp(-deltaTime * fastGhostShrinkSpeed)));
                SetFillAmount(fastGhostImage, fastGhostRatio);
            }

            if (slowGhostImage != null && slowGhostRatio > currentRatio)
            {
                slowGhostRatio = Mathf.Max(currentRatio, Mathf.Lerp(slowGhostRatio, currentRatio, 1f - Mathf.Exp(-deltaTime * slowGhostShrinkSpeed)));
                SetFillAmount(slowGhostImage, slowGhostRatio);
            }
        }

        private void TickExecutionWindowBlink()
        {
            if (foregroundImage == null)
            {
                return;
            }

            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * executionWindowBlinkSpeed * Mathf.PI * 2f);
            float alpha = Mathf.Lerp(executionWindowBlinkMinAlpha, 1f, blink);
            Color color = executionWindowColor;
            color.a *= alpha;
            foregroundImage.color = color;

            SetFillAmount(fastGhostImage, currentRatio);
            SetFillAmount(slowGhostImage, currentRatio);
        }

        private static void SetFillAmount(Image image, float ratio)
        {
            if (image != null)
            {
                image.fillAmount = ratio;
            }
        }

        private Color ResolveBarColor(float current, float max)
        {
            float amount = Mathf.Clamp01(current / Mathf.Max(1f, max));
            return Color.Lerp(emptyColor, normalColor, amount);
        }
    }
}
