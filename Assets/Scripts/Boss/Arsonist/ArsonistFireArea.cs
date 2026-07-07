using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistFireArea : MonoBehaviour
    {
        private const float FadeOutSeconds = 0.35f;

        private readonly Dictionary<PlayerCombatController, float> nextDamageAtByPlayer = new();
        private ArsonistBossAI owner;
        private CircleCollider2D circleCollider;
        private float radius;
        private float targetRadius;
        private float spreadStartsAt;
        private float spreadSeconds;
        private float expiresAt;
        private float destroyAt;
        private float playerDamageStartsAt;
        private Color fireColor;
        private bool initialized;
        private bool spreadCompleted;
        private bool fading;

        public Color FireColor => fireColor;

        public void Initialize(
            ArsonistBossAI nextOwner,
            float nextRadius,
            float duration,
            Color fireColor,
            float playerDamageDelay = 0f,
            float nextSpreadSeconds = 0f)
        {
            owner = nextOwner;
            circleCollider = GetComponent<CircleCollider2D>();
            targetRadius = Mathf.Max(0.05f, nextRadius);
            spreadSeconds = Mathf.Max(0f, nextSpreadSeconds);
            spreadStartsAt = Time.time;
            radius = spreadSeconds > 0f ? 0.05f : targetRadius;
            float durationSeconds = Mathf.Max(0.05f, duration);
            expiresAt = Time.time + durationSeconds;
            destroyAt = expiresAt + FadeOutSeconds;
            playerDamageStartsAt = Time.time + Mathf.Max(0f, playerDamageDelay);
            spreadCompleted = spreadSeconds <= 0f;
            fading = false;
            initialized = true;
            this.fireColor = fireColor;
            ApplyRadius(radius);
        }

        internal void HoldUntilReleased()
        {
            if (!initialized)
            {
                return;
            }

            expiresAt = float.PositiveInfinity;
            destroyAt = float.PositiveInfinity;
            fading = false;
            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }
        }

        internal void FadeOutNow()
        {
            if (!initialized)
            {
                return;
            }

            expiresAt = Time.time;
            destroyAt = expiresAt + FadeOutSeconds;
            fading = false;
        }

        public bool CanIgniteOilAt(Vector3 position, float oilRadius)
        {
            if (!initialized || Time.time >= expiresAt)
            {
                return false;
            }

            float maxDistance = radius + Mathf.Max(0f, oilRadius);
            return Vector2.SqrMagnitude((Vector2)transform.position - (Vector2)position) <= maxDistance * maxDistance;
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
                return;
            }

            if (!spreadCompleted)
            {
                float t = Mathf.Clamp01((Time.time - spreadStartsAt) / Mathf.Max(0.0001f, spreadSeconds));
                ApplyRadius(Mathf.Lerp(0.05f, targetRadius, t));
                spreadCompleted = t >= 1f;
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
            Color fadedColor = fireColor;
            fadedColor.a *= 1f - fadeT;
            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, fadedColor, true);
        }

        private void ApplyRadius(float nextRadius)
        {
            radius = Mathf.Clamp(nextRadius, 0.05f, targetRadius);
            if (circleCollider != null)
            {
                circleCollider.radius = radius;
            }

            ArsonistHazardVisual.ConfigureCircle(gameObject, radius, fireColor, true);
            owner?.TryIgniteOilAt(transform.position, radius, fireColor);
            owner?.TryRemoveWaterAt(transform.position, radius);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandleContact(other);
        }

        private void OnDestroy()
        {
            owner?.UnregisterFireArea(this);
        }

        private void HandleContact(Collider2D other)
        {
            if (owner == null || other == null || Time.time >= expiresAt)
            {
                return;
            }

            ArsonistOilPatch oilPatch = other.GetComponentInParent<ArsonistOilPatch>();
            if (oilPatch != null)
            {
                owner.IgniteOilNetwork(oilPatch, fireColor);
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player != null)
            {
                if (Time.time < playerDamageStartsAt)
                {
                    return;
                }

                owner.ApplyFireContact(player, transform.position, nextDamageAtByPlayer);
            }
        }
    }
}
