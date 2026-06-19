using UnityEngine;

namespace Week14.Combat
{
    public sealed class EnrageBurstVfx : MonoBehaviour
    {
        private const float StartScale = 0.05f;

        private SpriteRenderer spriteRenderer;
        private Transform followTarget;
        private float targetScale;
        private float growSeconds;
        private float holdSeconds;
        private float fadeSeconds;
        private float elapsed;
        private Color baseColor;

        public void Play(
            Sprite sprite,
            Vector3 position,
            Transform nextFollowTarget,
            float nextTargetScale,
            float nextGrowSeconds,
            float nextHoldSeconds,
            float nextFadeSeconds,
            Color color)
        {
            transform.position = position;
            transform.localScale = Vector3.one * StartScale;
            followTarget = nextFollowTarget;
            targetScale = Mathf.Max(StartScale, nextTargetScale);
            growSeconds = Mathf.Max(0.01f, nextGrowSeconds);
            holdSeconds = Mathf.Max(0f, nextHoldSeconds);
            fadeSeconds = Mathf.Max(0.01f, nextFadeSeconds);
            baseColor = color;
            elapsed = 0f;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = baseColor;
            spriteRenderer.sortingOrder = 80;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            if (followTarget != null)
            {
                transform.position = followTarget.position;
            }

            if (elapsed <= growSeconds)
            {
                float growT = elapsed / growSeconds;
                float eased = 1f - Mathf.Pow(1f - growT, 3f);
                transform.localScale = Vector3.one * Mathf.Lerp(StartScale, targetScale, eased);
                return;
            }

            transform.localScale = Vector3.one * targetScale;

            float fadeElapsed = elapsed - growSeconds - holdSeconds;
            if (fadeElapsed <= 0f)
            {
                return;
            }

            float fadeT = Mathf.Clamp01(fadeElapsed / fadeSeconds);
            Color faded = baseColor;
            faded.a = baseColor.a * (1f - fadeT);
            spriteRenderer.color = faded;

            if (fadeT >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
