using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class HackerGlitchProjectile : EnemyProjectile
    {
        private enum GlitchState
        {
            InitialFlight,
            GlitchApproach,
            Charging,
            Rush
        }

        [SerializeField, Min(0f)] private float launchFlashSeconds = 0.08f;
        [SerializeField, Min(0.05f)] private float initialFlightSeconds = 0.35f;
        [SerializeField, Min(0f)] private float initialFlightSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float initialFlightAlpha;
        [SerializeField, Min(0.05f)] private float revealDistance = 4f;
        [SerializeField, Min(0f)] private float driftSpeed = 1.2f;
        [SerializeField, Min(0f)] private float driftAmplitude = 0.5f;
        [SerializeField, Min(0.05f)] private float chargeSeconds = 0.65f;
        [SerializeField, Min(0f)] private float rushSpeed = 11f;
        [SerializeField, Range(0f, 1f)] private float glitchAlpha = 0.3f;
        [SerializeField] private Sprite chargeBlinkSprite;
        [SerializeField, Min(0.01f)] private float chargeBlinkMinRate = 3f;
        [SerializeField, Min(0.01f)] private float chargeBlinkMaxRate = 10f;

        private SpriteRenderer[] spriteRenderers;
        private Color[] baseColors;
        private GlitchState state;
        private float stateStartedAt;
        private float driftPhase;
        private Vector2 initialDirection;
        private Sprite defaultSprite;

        protected override void OnProjectileAwake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            ConfigureInterceptable(false);
        }

        protected override void OnProjectileInitialized()
        {
            ConfigureInterceptable(false);
            CaptureVisualState();
            state = GlitchState.InitialFlight;
            stateStartedAt = Time.time;
            driftPhase = Random.value * Mathf.PI * 2f;
            initialDirection = IncomingDirection.sqrMagnitude > 0.0001f ? IncomingDirection.normalized : Vector2.up;
            SetVisualAlpha(1f);
            ConfigureExternalMotionDriven(true);
        }

        protected override void OnProjectileLaunched()
        {
            ConfigureInterceptable(false);
        }

        protected override void OnProjectileTick()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null)
            {
                return;
            }

            switch (state)
            {
                case GlitchState.InitialFlight:
                    TickInitialFlight();
                    if (Time.time - stateStartedAt >= initialFlightSeconds)
                    {
                        state = GlitchState.GlitchApproach;
                        stateStartedAt = Time.time;
                        SetVisualAlpha(glitchAlpha);
                    }
                    break;

                case GlitchState.GlitchApproach:
                    FloatToward(player.transform.position);
                    if (Vector2.Distance(transform.position, player.transform.position) <= revealDistance)
                    {
                        state = GlitchState.Charging;
                        stateStartedAt = Time.time;
                        SetVisualAlpha(1f);
                        ConfigureInterceptable(true);
                    }
                    break;

                case GlitchState.Charging:
                    TickChargeBlink();
                    if (Time.time - stateStartedAt >= chargeSeconds)
                    {
                        state = GlitchState.Rush;
                        ApplyProjectileSprite(defaultSprite);
                        SetVisualAlpha(1f);
                    }
                    break;

                case GlitchState.Rush:
                    RushToward(player.transform.position);
                    break;
            }
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return state == GlitchState.Rush && base.CanHitPlayer(player);
        }

        private void TickInitialFlight()
        {
            transform.position += (Vector3)(initialDirection * initialFlightSpeed * EnemyTimeScale.DeltaTime);
            if (Time.time - stateStartedAt >= launchFlashSeconds)
            {
                SetVisualAlpha(initialFlightAlpha);
            }
        }

        private void FloatToward(Vector3 targetPosition)
        {
            Vector2 direction = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float wave = Mathf.Sin(Time.time * 4f + driftPhase) * driftAmplitude;
            transform.position += (Vector3)((direction * driftSpeed + perpendicular * wave) * EnemyTimeScale.DeltaTime);
        }

        private void RushToward(Vector3 targetPosition)
        {
            Vector2 direction = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(direction * rushSpeed * EnemyTimeScale.DeltaTime);
        }

        private void TickChargeBlink()
        {
            float progress = Mathf.Clamp01((Time.time - stateStartedAt) / chargeSeconds);
            float blinkRate = Mathf.Lerp(chargeBlinkMinRate, chargeBlinkMaxRate, progress);
            bool showBlinkSprite = chargeBlinkSprite != null
                && Mathf.Repeat((Time.time - stateStartedAt) * blinkRate, 1f) >= 0.5f;
            ApplyProjectileSprite(showBlinkSprite ? chargeBlinkSprite : defaultSprite);
        }

        private void CaptureVisualState()
        {
            defaultSprite = GetProjectileSprite();
            if (spriteRenderers == null)
            {
                return;
            }

            baseColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    baseColors[i] = spriteRenderers[i].color;
                }
            }
        }

        private void SetVisualAlpha(float alpha)
        {
            if (spriteRenderers == null)
            {
                return;
            }

            float clampedAlpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];
                if (renderer != null)
                {
                    Color color = baseColors != null && i < baseColors.Length
                        ? baseColors[i]
                        : renderer.color;
                    color.a *= clampedAlpha;
                    renderer.color = color;
                    renderer.enabled = clampedAlpha > 0f;
                }
            }
        }
    }
}
