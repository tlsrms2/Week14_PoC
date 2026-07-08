using System;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("Week14/Boss/Conductor Ordered Parry Projectile")]
    public sealed class ConductorOrderedParryProjectile : EnemyProjectile, IHomingEnemyProjectile
    {
        [Serializable]
        private sealed class SequenceVisualSettings
        {
            [SerializeField] private Sprite chargingSprite;
            [SerializeField] private Sprite notChargingSprite;
            [SerializeField] private Color chargeBlinkColor = Color.clear;

            public Sprite ResolveSprite(bool isCharging, Sprite defaultFallback)
            {
                Sprite stateSprite = isCharging ? chargingSprite : notChargingSprite;
                if (stateSprite != null)
                {
                    return stateSprite;
                }

                return defaultFallback;
            }

            public bool TryGetChargeBlinkColor(out Color color)
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

        [Header("Active Sequence")]
        [SerializeField] private SequenceVisualSettings activeSequence = new();

        [Header("Inactive Sequence")]
        [SerializeField] private SequenceVisualSettings inactiveSequence = new();

        private bool homingActive;
        private bool sequenceActive;
        private Color fallbackBlinkColor = Color.white;
        private Color sequenceBlinkColor = Color.white;
        private float homingBlinkPhase;
        private float homingTurnDegreesPerSecond;
        private float homingSeconds;
        private float homingEndsAt;
        private Sprite normalSequenceSprite;

        protected override bool IsHomingProjectile => homingActive;

        protected override void OnProjectileAwake()
        {
            normalSequenceSprite = GetProjectileSprite();
            SetSequenceActive(false);
        }

        protected override void OnProjectileInitialized()
        {
            normalSequenceSprite = GetProjectileSprite();
            SetSequenceActive(sequenceActive);
        }

        protected override void OnProjectileLaunched()
        {
            normalSequenceSprite = GetProjectileSprite();
            ApplySequenceSprite();
            ApplySequenceColorForCurrentState();
        }

        protected override void OnProjectileTick()
        {
            if (IsLaunched && !sequenceActive)
            {
                TickInactiveLaunchedBlink();
            }
        }

        protected override void CopySpecialRuntimeStateTo(EnemyProjectile replacement)
        {
            base.CopySpecialRuntimeStateTo(replacement);
            if (replacement is not ConductorOrderedParryProjectile orderedReplacement)
            {
                return;
            }

            orderedReplacement.sequenceActive = sequenceActive;
            orderedReplacement.homingActive = homingActive;
            orderedReplacement.fallbackBlinkColor = fallbackBlinkColor;
            orderedReplacement.sequenceBlinkColor = sequenceBlinkColor;
            orderedReplacement.homingBlinkPhase = homingBlinkPhase;
            orderedReplacement.homingTurnDegreesPerSecond = homingTurnDegreesPerSecond;
            orderedReplacement.homingSeconds = homingSeconds;
            orderedReplacement.homingEndsAt = homingEndsAt;
            orderedReplacement.normalSequenceSprite = normalSequenceSprite;
            orderedReplacement.SetSequenceActive(sequenceActive);
        }

        public void SetSequenceActive(bool active)
        {
            sequenceActive = active;
            ConfigureInterceptable(active);
            ConfigureSequenceBlinkColor();
            ApplySequenceSprite();
            ApplySequenceColorForCurrentState();
        }

        protected override void ConfigureSpecialStateColors(
            Color nextChargingColor,
            Color nextLaunchedColor,
            Color? nextHomingBlinkColor)
        {
            fallbackBlinkColor = nextHomingBlinkColor ?? nextLaunchedColor;
            ConfigureSequenceBlinkColor();
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
            ConfigureSequenceBlinkColor();
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
                ApplyProjectileColor(sequenceBlinkColor);
                return;
            }

            float blinkRate = Mathf.Lerp(chargeBlinkMaxRate, chargeBlinkMinRate, remainingRatio);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? sequenceBlinkColor
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

        private void ConfigureSequenceBlinkColor()
        {
            SequenceVisualSettings visuals = GetCurrentVisuals();
            sequenceBlinkColor = visuals != null && visuals.TryGetChargeBlinkColor(out Color color)
                ? color
                : fallbackBlinkColor;
            homingBlinkPhase = 0f;
        }

        private void ApplySequenceColorForCurrentState()
        {
            if (!IsLaunched)
            {
                return;
            }

            ApplyProjectileColor(sequenceActive ? LaunchedColor : sequenceBlinkColor);
        }

        private void TickInactiveLaunchedBlink()
        {
            float blinkRate = Mathf.Max(0.01f, chargeBlinkMinRate);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? sequenceBlinkColor
                : LaunchedColor;
            ApplyProjectileColor(nextColor);
        }

        private void ApplySequenceSprite()
        {
            SequenceVisualSettings visuals = GetCurrentVisuals();
            Sprite nextSprite = visuals != null
                ? visuals.ResolveSprite(IsCharging, normalSequenceSprite)
                : normalSequenceSprite;
            ApplyProjectileSprite(nextSprite);
        }

        private SequenceVisualSettings GetCurrentVisuals()
        {
            return sequenceActive ? activeSequence : inactiveSequence;
        }
    }
}
