using System;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public interface IConductorMultiStepOrderedProjectile
    {
        event Action<EnemyProjectile> SequenceStepCompleted;

        int RequiredSequenceSteps { get; }
        int CompletedSequenceSteps { get; }

        void SetSequenceActive(bool active);
    }

    [AddComponentMenu("Week14/Boss/Conductor Ordered Double Parry Projectile")]
    public sealed class ConductorOrderedDoubleParryProjectile : EnemyProjectile, IHomingEnemyProjectile, IConductorMultiStepOrderedProjectile
    {
        [Serializable]
        private sealed class SequenceVisualSettings
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

        [Serializable]
        private sealed class CycleVisualSettings
        {
            [SerializeField] private SequenceVisualSettings activeSequence = new();
            [SerializeField] private SequenceVisualSettings inactiveSequence = new();

            public SequenceVisualSettings Resolve(bool sequenceActive)
            {
                return sequenceActive ? activeSequence : inactiveSequence;
            }
        }

        [SerializeField, Min(0.01f)] private float defaultHomingSeconds = 10f;
        [SerializeField, Min(0.01f)] private float defaultTurnDegreesPerSecond = 540f;
        [SerializeField, Min(0.01f)] private float chargeBlinkMinRate = 2f;
        [SerializeField, Min(0.01f)] private float chargeBlinkMaxRate = 8f;
        [SerializeField, Range(0f, 1f)] private float chargeSolidColorRemainingRatio = 0.25f;

        [Header("First Cycle")]
        [SerializeField] private CycleVisualSettings firstCycle = new();

        [Header("Second Cycle")]
        [SerializeField] private CycleVisualSettings secondCycle = new();

        private const int RequiredParryCycles = 2;

        private bool homingActive;
        private bool sequenceActive;
        private int completedSequenceSteps;
        private Color fallbackBlinkColor = Color.white;
        private Color sequenceBlinkColor = Color.white;
        private float homingBlinkPhase;
        private float homingTurnDegreesPerSecond;
        private float homingSeconds;
        private float homingEndsAt;
        private Sprite defaultSprite;
        private bool hasDefaultSprite;

        public event Action<EnemyProjectile> SequenceStepCompleted;

        public int RequiredSequenceSteps => RequiredParryCycles;
        public int CompletedSequenceSteps => Mathf.Clamp(completedSequenceSteps, 0, RequiredParryCycles);

        protected override bool IsHomingProjectile => homingActive;

        protected override void OnProjectileAwake()
        {
            CacheDefaultSprite();
            completedSequenceSteps = 0;
            SetSequenceActive(false);
        }

        protected override void OnProjectileInitialized()
        {
            CacheDefaultSprite();
            completedSequenceSteps = 0;
            SetSequenceActive(sequenceActive);
        }

        protected override void OnProjectileLaunched()
        {
            CacheDefaultSprite();
            RefreshSequenceVisualState();
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
            if (replacement is not ConductorOrderedDoubleParryProjectile orderedDoubleReplacement)
            {
                return;
            }

            orderedDoubleReplacement.homingActive = homingActive;
            orderedDoubleReplacement.sequenceActive = sequenceActive;
            orderedDoubleReplacement.completedSequenceSteps = completedSequenceSteps;
            orderedDoubleReplacement.fallbackBlinkColor = fallbackBlinkColor;
            orderedDoubleReplacement.sequenceBlinkColor = sequenceBlinkColor;
            orderedDoubleReplacement.homingBlinkPhase = homingBlinkPhase;
            orderedDoubleReplacement.homingTurnDegreesPerSecond = homingTurnDegreesPerSecond;
            orderedDoubleReplacement.homingSeconds = homingSeconds;
            orderedDoubleReplacement.homingEndsAt = homingEndsAt;
            orderedDoubleReplacement.SetSequenceActive(sequenceActive);
        }

        protected override void OnProjectileReturnedToPool()
        {
            base.OnProjectileReturnedToPool();
            SequenceStepCompleted = null;
        }

        public void SetSequenceActive(bool active)
        {
            sequenceActive = active;
            ConfigureInterceptable(active);
            RefreshSequenceVisualState();
        }

        public override bool TryDestroyByInterceptShot(out bool parried)
        {
            if (!CanReceiveInterceptShot())
            {
                parried = false;
                return false;
            }

            parried = true;
            if (completedSequenceSteps <= 0)
            {
                completedSequenceSteps = 1;
                CompletePartialIntercept();
                SetSequenceActive(false);
                SequenceStepCompleted?.Invoke(this);
                return true;
            }

            completedSequenceSteps = RequiredParryCycles;
            CompleteInterceptAndDestroy();
            return true;
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
            sequenceBlinkColor = visuals != null && visuals.TryGetBlinkColor(out Color color)
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

        private void ApplySequenceVisuals()
        {
            SequenceVisualSettings visuals = GetCurrentVisuals();
            Sprite nextSprite = visuals != null
                ? visuals.ResolveSprite(IsCharging, defaultSprite)
                : defaultSprite;
            ApplyProjectileSprite(nextSprite);
        }

        private void RefreshSequenceVisualState()
        {
            ConfigureSequenceBlinkColor();
            ApplySequenceVisuals();
            ApplySequenceColorForCurrentState();
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

        private SequenceVisualSettings GetCurrentVisuals()
        {
            CycleVisualSettings cycleVisuals = completedSequenceSteps <= 0 ? firstCycle : secondCycle;
            return cycleVisuals?.Resolve(sequenceActive);
        }
    }
}
