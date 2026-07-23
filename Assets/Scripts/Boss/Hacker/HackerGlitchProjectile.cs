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

        [Header("Trail")]
        [SerializeField, Tooltip("글리치 투사체의 궤적 색상입니다.")]
        private Color trailColor = new(1f, 0.82f, 0.18f, 0.55f);

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

        [Header("Charge Gauge")]
        [SerializeField, Tooltip("대기 및 차징 진행도를 표시할 원형 게이지 스프라이트 렌더러입니다.")]
        private SpriteRenderer chargeGaugeRenderer;
        [SerializeField, Min(0.05f), Tooltip("Timed Charge Approach를 사용하지 않을 때 게이지가 차오르는 시간입니다.")]
        private float defaultWaitingGaugeFillSeconds = 1.5f;

        [Header("Stage Sprites")]
        [SerializeField] private Sprite initialFlightSprite;
        [SerializeField] private Sprite glitchApproachSprite;
        [SerializeField] private Sprite chargingSprite;
        [SerializeField] private Sprite rushSprite;

        [Header("Charge Blink")]
        [SerializeField] private Color chargeBlinkColor = new(0.3f, 0.85f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float chargeBlinkMinRate = 3f;
        [SerializeField, Min(0.01f)] private float chargeBlinkMaxRate = 10f;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        private SpriteRenderer[] spriteRenderers;
        private Color[] baseColors;
        private MaterialPropertyBlock chargeGaugePropertyBlock;
        private GlitchState state;
        private float stateStartedAt;
        private float launchedAt;
        private float chargeApproachArrivalSeconds = -1f;
        private float driftPhase;
        private Vector2 initialDirection;
        private Sprite defaultSprite;

        protected override Color? GetTrailColorOverride()
        {
            return trailColor;
        }

        protected override bool IgnoresWalls => true;

        protected override void OnProjectileAwake()
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            defaultSprite = GetProjectileSprite();
            ConfigureInterceptable(false);
        }

        protected override void OnProjectileInitialized()
        {
            ConfigureInterceptable(false);
            CaptureVisualState();
            state = GlitchState.InitialFlight;
            stateStartedAt = Time.time;
            launchedAt = Time.time;
            chargeApproachArrivalSeconds = -1f;
            driftPhase = Random.value * Mathf.PI * 2f;
            initialDirection = IncomingDirection.sqrMagnitude > 0.0001f ? IncomingDirection.normalized : Vector2.up;
            ApplyStageSprite(initialFlightSprite);
            SetVisualAlpha(1f);
            SetChargeGaugeVisible(true);
            SetChargeGaugeFill(0f);
            ConfigureExternalMotionDriven(true);
        }

        protected override void OnProjectileLaunched()
        {
            launchedAt = Time.time;
            ConfigureInterceptable(false);
            SetChargeGaugeVisible(true);
            SetChargeGaugeFill(0f);
        }

        internal void ConfigureTimedChargeApproach(float arrivalSeconds)
        {
            chargeApproachArrivalSeconds = arrivalSeconds >= 0f ? arrivalSeconds : -1f;
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
                    TickWaitingChargeGauge();
                    TickInitialFlight();
                    if (Time.time - stateStartedAt >= initialFlightSeconds)
                    {
                        state = GlitchState.GlitchApproach;
                        stateStartedAt = Time.time;
                        ApplyStageSprite(glitchApproachSprite);
                        SetVisualAlpha(glitchAlpha);
                        if (ShouldStartCharging(player))
                        {
                            BeginCharging();
                        }
                    }
                    break;

                case GlitchState.GlitchApproach:
                    TickWaitingChargeGauge();
                    FloatToward(player.transform.position);
                    if (ShouldStartCharging(player))
                    {
                        BeginCharging();
                    }
                    break;

                case GlitchState.Charging:
                    TickChargingGauge();
                    TickChargeBlink();
                    if (Time.time - stateStartedAt >= chargeSeconds)
                    {
                        SetChargeGaugeFill(0f);
                        SetChargeGaugeVisible(false);
                        state = GlitchState.Rush;
                        ApplyStageSprite(rushSprite);
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

        private bool ShouldStartCharging(PlayerCombatController player)
        {
            return player != null
                && !IsChargeApproachPending()
                && Vector2.Distance(transform.position, player.transform.position) <= revealDistance;
        }

        private void BeginCharging()
        {
            state = GlitchState.Charging;
            stateStartedAt = Time.time;
            ApplyStageSprite(chargingSprite);
            SetVisualAlpha(1f);
            SetChargeGaugeVisible(true);
            SetChargeGaugeFill(1f);
            ConfigureInterceptable(true);
        }

        private void TickWaitingChargeGauge()
        {
            float fillSeconds = chargeApproachArrivalSeconds >= 0f
                ? Mathf.Max(0.05f, chargeApproachArrivalSeconds)
                : Mathf.Max(0.05f, defaultWaitingGaugeFillSeconds);
            float progress = Mathf.Clamp01((Time.time - launchedAt) / fillSeconds);
            SetChargeGaugeFill(progress);
        }

        private void TickChargingGauge()
        {
            float progress = Mathf.Clamp01(
                (Time.time - stateStartedAt) / Mathf.Max(0.05f, chargeSeconds));
            SetChargeGaugeFill(1f - progress);
        }

        private void FloatToward(Vector3 targetPosition)
        {
            Vector2 toTarget = (Vector2)targetPosition - (Vector2)transform.position;
            float distanceToTarget = toTarget.magnitude;
            Vector2 directionToPlayer = distanceToTarget > 0.0001f
                ? toTarget / distanceToTarget
                : -initialDirection;
            bool avoidPlayer = IsChargeApproachPending() && distanceToTarget <= revealDistance;
            Vector2 movementDirection = avoidPlayer ? -directionToPlayer : directionToPlayer;
            Vector2 perpendicular = new Vector2(-directionToPlayer.y, directionToPlayer.x);
            float wave = Mathf.Sin(Time.time * 4f + driftPhase) * driftAmplitude;
            float approachSpeed = avoidPlayer
                ? Mathf.Max(driftSpeed, (revealDistance - distanceToTarget) * 4f)
                : GetApproachSpeed(distanceToTarget);
            transform.position += (Vector3)((movementDirection * approachSpeed + perpendicular * wave) * EnemyTimeScale.DeltaTime);
        }

        private bool IsChargeApproachPending()
        {
            return chargeApproachArrivalSeconds >= 0f
                && Time.time - launchedAt < chargeApproachArrivalSeconds;
        }

        private float GetApproachSpeed(float distanceToTarget)
        {
            if (chargeApproachArrivalSeconds < 0f)
            {
                return driftSpeed;
            }

            float remainingDistance = Mathf.Max(0f, distanceToTarget - revealDistance);
            float remainingSeconds = chargeApproachArrivalSeconds - (Time.time - launchedAt);
            if (remainingSeconds > 0.001f)
            {
                return remainingDistance / remainingSeconds;
            }

            return Mathf.Max(driftSpeed, remainingDistance * 4f);
        }

        private void RushToward(Vector3 targetPosition)
        {
            Vector2 toTarget = (Vector2)targetPosition - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 direction = toTarget.normalized;
            ApplyFlightDirection(direction, false);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            transform.position += (Vector3)(direction * rushSpeed * EnemyTimeScale.DeltaTime);
        }

        private void TickChargeBlink()
        {
            float progress = Mathf.Clamp01((Time.time - stateStartedAt) / chargeSeconds);
            float blinkRate = Mathf.Lerp(chargeBlinkMinRate, chargeBlinkMaxRate, progress);
            bool showBlinkColor = Mathf.Repeat((Time.time - stateStartedAt) * blinkRate, 1f) >= 0.5f;
            SetChargeBlinkColor(showBlinkColor);
        }

        private void CaptureVisualState()
        {
            if (defaultSprite == null)
            {
                defaultSprite = GetProjectileSprite();
            }

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

        private void ApplyStageSprite(Sprite stageSprite)
        {
            ApplyProjectileSprite(ResolveStageSprite(stageSprite));
        }

        private Sprite ResolveStageSprite(Sprite stageSprite)
        {
            return stageSprite != null ? stageSprite : defaultSprite;
        }

        private void SetChargeBlinkColor(bool useBlinkColor)
        {
            if (spriteRenderers == null)
            {
                return;
            }

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer renderer = spriteRenderers[i];
                if (renderer == null || renderer == chargeGaugeRenderer)
                {
                    continue;
                }

                Color baseColor = baseColors != null && i < baseColors.Length
                    ? baseColors[i]
                    : renderer.color;
                Color color = useBlinkColor ? chargeBlinkColor : baseColor;
                if (useBlinkColor)
                {
                    color.a *= baseColor.a;
                }

                renderer.color = color;
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
                if (renderer != null && renderer != chargeGaugeRenderer)
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

        private void SetChargeGaugeVisible(bool visible)
        {
            if (chargeGaugeRenderer != null)
            {
                chargeGaugeRenderer.enabled = visible;
            }
        }

        private void SetChargeGaugeFill(float fillRatio)
        {
            if (chargeGaugeRenderer == null)
            {
                return;
            }

            chargeGaugePropertyBlock ??= new MaterialPropertyBlock();
            chargeGaugeRenderer.GetPropertyBlock(chargeGaugePropertyBlock);
            chargeGaugePropertyBlock.SetFloat(FillAmountId, Mathf.Clamp01(fillRatio));
            chargeGaugeRenderer.SetPropertyBlock(chargeGaugePropertyBlock);
        }
    }
}
