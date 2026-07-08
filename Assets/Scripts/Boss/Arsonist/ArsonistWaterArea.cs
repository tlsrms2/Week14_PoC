using UnityEngine;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistWaterArea : MonoBehaviour
    {
        private const float FadeOutSeconds = 0.2f;

        private ArsonistBossAI owner;
        private CircleCollider2D circleCollider;
        private float radius;
        private float expiresAt;
        private float destroyAt;
        private Color waterColor;
        private bool initialized;
        private bool fading;

        public void Initialize(ArsonistBossAI nextOwner, float nextRadius, float duration, Color nextWaterColor)
        {
            owner = nextOwner;
            circleCollider = GetComponent<CircleCollider2D>();
            radius = Mathf.Max(0.05f, nextRadius);
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            destroyAt = expiresAt + FadeOutSeconds;
            waterColor = nextWaterColor;
            initialized = true;
            fading = false;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, waterColor, true);
        }

        public bool CanBeRemovedByHazardAt(Vector3 position, float hazardRadius)
        {
            if (!initialized || Time.time >= expiresAt)
            {
                return false;
            }

            float maxDistance = radius + Mathf.Max(0f, hazardRadius);
            return Vector2.SqrMagnitude((Vector2)transform.position - (Vector2)position) <= maxDistance * maxDistance;
        }

        internal void FadeOutNow()
        {
            if (!initialized || fading)
            {
                return;
            }

            expiresAt = Time.time;
            destroyAt = expiresAt + FadeOutSeconds;
            fading = false;
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
                return;
            }

            if (Time.time >= expiresAt)
            {
                TickFadeOut();
            }
        }

        private void TickFadeOut()
        {
            if (!fading)
            {
                fading = true;
                if (circleCollider != null)
                {
                    circleCollider.enabled = false;
                }
            }

            float fadeT = FadeOutSeconds > 0f
                ? Mathf.Clamp01((Time.time - expiresAt) / FadeOutSeconds)
                : 1f;
            Color fadedColor = waterColor;
            fadedColor.a *= 1f - fadeT;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, fadedColor, true);
        }

        private void OnDestroy()
        {
            owner?.UnregisterWaterArea(this);
        }
    }
}
