using System;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Conductor Double Parry Projectile")]
    public sealed class ConductorDoubleParryProjectile : EnemyProjectile, IHomingEnemyProjectile
    {
        [Serializable]
        private sealed class ParryStageVisualSettings
        {
            [SerializeField] private Sprite chargingSprite;
            [SerializeField] private Sprite notChargingSprite;
            [SerializeField] private Color chargeBlinkColor = Color.clear;

            public Sprite ResolveSprite(bool isCharging, Sprite fallback)
            {
                Sprite stateSprite = isCharging ? chargingSprite : notChargingSprite;
                return stateSprite != null ? stateSprite : fallback;
            }

            public bool TryGetBlinkColor(out Color color)
            {
                color = chargeBlinkColor;
                return color != Color.clear;
            }
        }

        [SerializeField, Min(0.01f)] private float defaultHomingSeconds = 10f;
        [SerializeField, Min(0.01f)] private float defaultTurnDegreesPerSecond = 540f;
        [SerializeField, Min(0.01f)] private float chargeBlinkMinRate = 2f;
        [SerializeField, Min(0.01f)] private float chargeBlinkMaxRate = 8f;
        [SerializeField, Range(0f, 1f)] private float chargeSolidColorRemainingRatio = 0.25f;

        [Header("First Parry")]
        [SerializeField] private ParryStageVisualSettings firstParry = new();

        [Header("Second Parry")]
        [SerializeField] private ParryStageVisualSettings secondParry = new();

        private bool homingActive;
        private int parryCount;
        private Color fallbackBlinkColor = Color.white;
        private Color stageBlinkColor = Color.white;
        private float homingBlinkPhase;
        private float homingTurnDegreesPerSecond;
        private float homingSeconds;
        private float homingEndsAt;
        private Sprite defaultSprite;
        private bool hasDefaultSprite;

        protected override bool IsHomingProjectile => homingActive;

        protected override void OnProjectileAwake()
        {
            CacheDefaultSprite();
            parryCount = 0;
            ApplyStageVisuals();
        }

        protected override void OnProjectileInitialized()
        {
            CacheDefaultSprite();
            parryCount = 0;
            ApplyStageVisuals();
        }

        protected override void OnProjectileLaunched()
        {
            CacheDefaultSprite();
            ApplyStageVisuals();
            ApplyStageColorForCurrentState();
        }

        protected override void OnProjectileTick()
        {
            if (IsLaunched)
            {
                TickLaunchedBlink();
            }
        }

        protected override void CopySpecialRuntimeStateTo(EnemyProjectile replacement)
        {
            base.CopySpecialRuntimeStateTo(replacement);
            if (replacement is not ConductorDoubleParryProjectile doubleParryReplacement)
            {
                return;
            }

            doubleParryReplacement.homingActive = homingActive;
            doubleParryReplacement.parryCount = parryCount;
            doubleParryReplacement.fallbackBlinkColor = fallbackBlinkColor;
            doubleParryReplacement.stageBlinkColor = stageBlinkColor;
            doubleParryReplacement.homingBlinkPhase = homingBlinkPhase;
            doubleParryReplacement.homingTurnDegreesPerSecond = homingTurnDegreesPerSecond;
            doubleParryReplacement.homingSeconds = homingSeconds;
            doubleParryReplacement.homingEndsAt = homingEndsAt;
            doubleParryReplacement.ConfigureStageBlinkColor();
            doubleParryReplacement.ApplyStageVisuals();
        }

        public override bool TryDestroyByInterceptShot(out bool parried)
        {
            if (!CanReceiveInterceptShot())
            {
                parried = false;
                return false;
            }

            parried = true;
            if (parryCount <= 0)
            {
                parryCount = 1;
                CompletePartialIntercept();
                ConfigureStageBlinkColor();
                ApplyStageVisuals();
                ApplyStageColorForCurrentState();
                return true;
            }

            CompleteInterceptAndDestroy();
            return true;
        }

        protected override void ConfigureSpecialStateColors(
            Color nextChargingColor,
            Color nextLaunchedColor,
            Color? nextHomingBlinkColor)
        {
            fallbackBlinkColor = nextHomingBlinkColor ?? nextLaunchedColor;
            ConfigureStageBlinkColor();
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
            ConfigureStageBlinkColor();
        }

        protected override void GetHomingSpawnConfig(out bool enabled, out float seconds, out float turnDegrees)
        {
            enabled = homingActive;
            seconds = homingSeconds;
            turnDegrees = homingTurnDegreesPerSecond;
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
            if (remainingRatio <= chargeSolidColorRemainingRatio)
            {
                ApplyProjectileColor(stageBlinkColor);
                return;
            }

            float blinkRate = Mathf.Lerp(chargeBlinkMaxRate, chargeBlinkMinRate, remainingRatio);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? stageBlinkColor
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

        private void ConfigureStageBlinkColor()
        {
            ParryStageVisualSettings visuals = GetCurrentVisuals();
            stageBlinkColor = visuals != null && visuals.TryGetBlinkColor(out Color color)
                ? color
                : fallbackBlinkColor;
            homingBlinkPhase = 0f;
        }

        private void CacheDefaultSprite()
        {
            if (hasDefaultSprite)
            {
                return;
            }

            defaultSprite = GetProjectileSprite();
            hasDefaultSprite = true;
        }

        private void ApplyStageVisuals()
        {
            ParryStageVisualSettings visuals = GetCurrentVisuals();
            Sprite nextSprite = visuals != null
                ? visuals.ResolveSprite(IsCharging, defaultSprite)
                : defaultSprite;
            ApplyProjectileSprite(nextSprite);
        }

        private void ApplyStageColorForCurrentState()
        {
            if (!IsLaunched)
            {
                return;
            }

            ApplyProjectileColor(stageBlinkColor);
        }

        private void TickLaunchedBlink()
        {
            float blinkRate = Mathf.Max(0.01f, chargeBlinkMinRate);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? stageBlinkColor
                : LaunchedColor;
            ApplyProjectileColor(nextColor);
        }

        private ParryStageVisualSettings GetCurrentVisuals()
        {
            return parryCount <= 0 ? firstParry : secondParry;
        }
    }
}
