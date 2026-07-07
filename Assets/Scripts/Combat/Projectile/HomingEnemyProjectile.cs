using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Homing Enemy Projectile")]
    public class HomingEnemyProjectile : EnemyProjectile
    {
        [SerializeField, Min(0.01f), Tooltip("호밍이 유지되는 기본 시간입니다.")]
        private float defaultHomingSeconds = 10f;
        [SerializeField, Min(0.01f), Tooltip("초당 회전 가능한 기본 각도입니다.")]
        private float defaultTurnDegreesPerSecond = 540f;
        [SerializeField, Tooltip("충전 중 점멸 색입니다. 투명색이면 프리팹 색을 사용합니다.")]
        private Color chargeBlinkColor = Color.clear;
        [SerializeField, Min(0.01f), Tooltip("충전 점멸의 최소 속도입니다.")]
        private float chargeBlinkMinRate = 2f;
        [SerializeField, Min(0.01f), Tooltip("충전 점멸의 최대 속도입니다.")]
        private float chargeBlinkMaxRate = 8f;
        [SerializeField, Range(0f, 1f), Tooltip("충전 종료 직전 점멸을 멈추고 색을 고정할 비율입니다.")]
        private float chargeSolidColorRemainingRatio = 0.25f;
        [SerializeField, Tooltip("충전 중 표시할 스프라이트입니다. 비워두면 프리팹 기본 스프라이트를 사용합니다.")]
        private Sprite chargingSprite;
        [SerializeField, Tooltip("발사 후 표시할 스프라이트입니다. 비워두면 프리팹 기본 스프라이트를 사용합니다.")]
        private Sprite launchedSprite;
        [SerializeField, Tooltip("남은 충전시간 비율을 표시할 원형 게이지 스프라이트 렌더러입니다. 비워두면 게이지를 표시하지 않습니다.")]
        private SpriteRenderer chargeGaugeRenderer;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        private bool homingActive;
        private MaterialPropertyBlock chargeGaugePropertyBlock;
        private Color homingBlinkColor;
        private float homingBlinkPhase;
        private float homingTurnDegreesPerSecond;
        private float homingSeconds;
        private float homingEndsAt;
        private Sprite fallbackSprite;

        protected override bool IsHomingProjectile => homingActive;

        protected override void OnProjectileAwake()
        {
            fallbackSprite = GetProjectileSprite();
        }

        protected override void OnProjectileInitialized()
        {
            ApplyHomingStateSprite();
            SetChargeGaugeVisible(homingActive && IsCharging);
        }

        protected override void OnProjectileLaunched()
        {
            ApplyHomingLaunchedSprite();
            SetChargeGaugeVisible(false);
        }

        protected override void ConfigureHoming(bool enabled, float seconds, float turnDegrees, float launchTime)
        {
            homingActive = enabled;
            homingSeconds = homingActive
                ? Mathf.Max(0.01f, seconds > 0f ? seconds : defaultHomingSeconds)
                : 0f;
            homingTurnDegreesPerSecond = homingActive
                ? Mathf.Max(0.01f, turnDegrees > 0f ? turnDegrees : defaultTurnDegreesPerSecond)
                : 0f;
            homingEndsAt = launchTime + homingSeconds;
            homingBlinkPhase = 0f;
        }

        private void ApplyHomingStateSprite()
        {
            if (IsCharging)
            {
                ApplyHomingChargingSprite();
                return;
            }

            ApplyHomingLaunchedSprite();
        }

        private void ApplyHomingChargingSprite()
        {
            ApplyProjectileSprite(chargingSprite != null ? chargingSprite : fallbackSprite);
        }

        private void ApplyHomingLaunchedSprite()
        {
            ApplyProjectileSprite(launchedSprite != null ? launchedSprite : fallbackSprite);
        }

        protected override void ConfigureSpecialStateColors(
            Color nextChargingColor,
            Color nextLaunchedColor,
            Color? nextHomingBlinkColor)
        {
            homingBlinkColor = chargeBlinkColor != Color.clear
                ? chargeBlinkColor
                : nextHomingBlinkColor ?? nextLaunchedColor;
            homingBlinkPhase = 0f;
        }

        protected override void GetHomingSpawnConfig(out bool enabled, out float seconds, out float turnDegrees)
        {
            enabled = homingActive;
            seconds = homingSeconds;
            turnDegrees = homingTurnDegreesPerSecond;
        }

        protected override void CopySpecialRuntimeStateTo(EnemyProjectile replacement)
        {
            if (replacement is not HomingEnemyProjectile homingReplacement)
            {
                return;
            }

            homingReplacement.homingActive = homingActive;
            homingReplacement.homingBlinkColor = homingBlinkColor;
            homingReplacement.homingBlinkPhase = homingBlinkPhase;
            homingReplacement.homingTurnDegreesPerSecond = homingTurnDegreesPerSecond;
            homingReplacement.homingSeconds = homingSeconds;
            homingReplacement.homingEndsAt = homingEndsAt;
        }

        protected override bool ShouldAimAtPlayerOnLaunch()
        {
            return homingActive;
        }

        protected override void UpdateHomingChargeBlink()
        {
            if (!homingActive || ProjectileChargeSeconds <= 0f)
            {
                return;
            }

            float remainingRatio = Mathf.Clamp01((ChargeEndsAt - Time.time) / ProjectileChargeSeconds);
            SetChargeGaugeFill(remainingRatio);
            if (remainingRatio <= chargeSolidColorRemainingRatio)
            {
                ApplyProjectileColor(homingBlinkColor);
                return;
            }

            float blinkRate = Mathf.Lerp(chargeBlinkMaxRate, chargeBlinkMinRate, remainingRatio);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? homingBlinkColor
                : ChargingColor;
            ApplyProjectileColor(nextColor);
        }

        protected override void TickHoming()
        {
            if (!homingActive
                || IsResolved
                || IsDestroying
                || Time.time >= homingEndsAt
                || homingTurnDegreesPerSecond <= 0f
                || ProjectileSpeed <= 0f)
            {
                return;
            }

            PlayerCombatController target = PlayerCombatController.Active;
            if (target == null || target.Health == null || target.Health.IsDead)
            {
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float maxRadians = homingTurnDegreesPerSecond * Mathf.Deg2Rad * EnemyTimeScale.DeltaTime;
            Vector3 nextDirection = Vector3.RotateTowards(FlightDirection, toTarget.normalized, maxRadians, 0f);
            ApplyFlightDirection(nextDirection);
            if (ProjectileBody != null)
            {
                ProjectileBody.linearVelocity = FlightDirection * ProjectileSpeed;
            }
        }

        protected override void ExtendSpecialTimers(float pausedSeconds)
        {
            homingEndsAt += Mathf.Max(0f, pausedSeconds);
        }

        private void SetChargeGaugeVisible(bool visible)
        {
            if (chargeGaugeRenderer != null)
            {
                chargeGaugeRenderer.enabled = visible;
            }
        }

        private void SetChargeGaugeFill(float remainingRatio)
        {
            if (chargeGaugeRenderer == null)
            {
                return;
            }

            chargeGaugePropertyBlock ??= new MaterialPropertyBlock();
            chargeGaugeRenderer.GetPropertyBlock(chargeGaugePropertyBlock);
            chargeGaugePropertyBlock.SetFloat(FillAmountId, remainingRatio);
            chargeGaugeRenderer.SetPropertyBlock(chargeGaugePropertyBlock);
        }
    }
}
