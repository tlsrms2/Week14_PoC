using System;
using UnityEngine;

namespace Week14.Combat
{
    public sealed class MouseParryReticle : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] colorOnlyRenderers = Array.Empty<SpriteRenderer>();
        [Tooltip("colorOnlyRenderers의 평상시(비위협) 색상입니다.")]
        [SerializeField] private Color colorOnlyIdleColor = Color.white;
        [Tooltip("colorOnlyRenderers의 위협(threatened) 상태 색상입니다.")]
        [SerializeField] private Color colorOnlyThreatenedColor = new(1f, 0.45f, 0.05f, 1f);
        [Tooltip("PlayMissFeedback 호출 시 colorOnlyRenderers에 잠깐 적용되는 미스 피드백 색상입니다.")]
        [SerializeField] private Color missColorOnlyColor = new(1f, 0.12f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float colorSpeed = 8f;
        [SerializeField] private bool useUnscaledTime;

        private bool threatened;
        private bool hacked;
        private bool visible = true;
        private Vector3[] colorOnlyBaseLocalPositions = Array.Empty<Vector3>();
        private bool[] colorOnlyHasBaseLocalPositions = Array.Empty<bool>();
        private float missFeedbackEndsAt;
        private float missShakeStartedAt;
        private float missShakeEndsAt;
        private float missShakeDuration;
        private float missShakeMagnitude;
        private float missShakeFrequency;
        private Vector2 missShakeDirection = Vector2.right;

        public void SetThreatened(bool value)
        {
            threatened = value;
        }

        public void SetHacked(bool value)
        {
            hacked = value;
        }

        public void PlayMissFeedback(float colorSeconds, float shakeSeconds, float shakeAmplitude, float shakeFrequency)
        {
            float now = CurrentTime;
            missFeedbackEndsAt = now + Mathf.Max(0f, colorSeconds);
            missShakeStartedAt = now;
            missShakeDuration = Mathf.Max(0f, shakeSeconds);
            missShakeMagnitude = Mathf.Max(0f, shakeAmplitude);
            missShakeFrequency = Mathf.Max(0f, shakeFrequency);
            missShakeEndsAt = now + missShakeDuration;
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
            missShakeDirection = randomDirection.sqrMagnitude > 0.0001f ? randomDirection.normalized : Vector2.right;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SetRendererVisible(colorOnlyRenderers[i], value);
            }

            if (!value)
            {
                ResetColorOnlyMotion();
            }
        }

        private void Awake()
        {
            CacheBaseLocalPositions();
        }

        private void OnEnable()
        {
            CacheBaseLocalPositions();
            SetVisible(visible);
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float now = CurrentTime;
            bool useFeedbackColor = now < missFeedbackEndsAt;
            Vector3 shakeOffset = GetMissShakeOffset(now);

            Color targetColor = hacked
                ? new Color(1f, 0.12f, 0.08f, 1f)
                : useFeedbackColor
                ? missColorOnlyColor
                : (threatened ? colorOnlyThreatenedColor : colorOnlyIdleColor);
            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer != null)
                {
                    ApplyColorOnlyShake(i, renderer, shakeOffset);
                    renderer.color = MoveColor(renderer.color, targetColor, Mathf.Max(0f, colorSpeed) * deltaTime);
                }
            }
        }

        private void CacheBaseLocalPositions()
        {
            if (colorOnlyBaseLocalPositions.Length != colorOnlyRenderers.Length)
            {
                colorOnlyBaseLocalPositions = new Vector3[colorOnlyRenderers.Length];
                colorOnlyHasBaseLocalPositions = new bool[colorOnlyRenderers.Length];
            }

            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer == null || renderer.transform == transform)
                {
                    continue;
                }

                if (!colorOnlyHasBaseLocalPositions[i])
                {
                    colorOnlyBaseLocalPositions[i] = renderer.transform.localPosition;
                    colorOnlyHasBaseLocalPositions[i] = true;
                }
            }
        }

        private Vector3 GetMissShakeOffset(float now)
        {
            if (now >= missShakeEndsAt || missShakeDuration <= 0f || missShakeMagnitude <= 0f)
            {
                return Vector3.zero;
            }

            float elapsed = Mathf.Max(0f, now - missShakeStartedAt);
            float normalized = Mathf.Clamp01(elapsed / missShakeDuration);
            float damping = 1f - normalized;
            float wave = Mathf.Sin(elapsed * missShakeFrequency);
            return (Vector3)(missShakeDirection * (missShakeMagnitude * damping * wave));
        }

        private void ApplyColorOnlyShake(int index, SpriteRenderer renderer, Vector3 shakeOffset)
        {
            if (renderer.transform == transform)
            {
                return;
            }

            if (index < 0 || index >= colorOnlyBaseLocalPositions.Length || !colorOnlyHasBaseLocalPositions[index])
            {
                CacheBaseLocalPositions();
                if (index < 0 || index >= colorOnlyBaseLocalPositions.Length || !colorOnlyHasBaseLocalPositions[index])
                {
                    return;
                }
            }

            renderer.transform.localPosition = colorOnlyBaseLocalPositions[index] + shakeOffset;
        }

        private void ResetColorOnlyMotion()
        {
            CacheBaseLocalPositions();
            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer != null
                    && renderer.transform != transform
                    && i < colorOnlyBaseLocalPositions.Length
                    && colorOnlyHasBaseLocalPositions[i])
                {
                    renderer.transform.localPosition = colorOnlyBaseLocalPositions[i];
                }
            }
        }

        private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;

        private static void SetRendererVisible(SpriteRenderer renderer, bool value)
        {
            if (renderer != null)
            {
                renderer.enabled = value;
            }
        }

        private static Color MoveColor(Color current, Color target, float maxDelta)
        {
            return new Color(
                Mathf.MoveTowards(current.r, target.r, maxDelta),
                Mathf.MoveTowards(current.g, target.g, maxDelta),
                Mathf.MoveTowards(current.b, target.b, maxDelta),
                Mathf.MoveTowards(current.a, target.a, maxDelta));
        }
    }
}
