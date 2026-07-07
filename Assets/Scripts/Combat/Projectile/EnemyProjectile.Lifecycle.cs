using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            ResolveParryLockOnIndicator();
            SetParryLockOnIndicatorVisible(false);
            OnProjectileAwake();
        }

        private void Initialize(
            Vector2 direction,
            int nextBulletDamage,
            BulletGauge nextOwnerBullets,
            float chargeSeconds,
            float speed, float lifetime, float radius, Color color,
            float trailSeconds, float trailWidth,
            bool enableHoming,
            float homingSeconds, float nextHomingTurnDegrees,
            bool nextSuppressPathIndicator,
            int existingInterceptGroupId)
        {
            projectileSpeed = speed;
            projectileLifetime = Mathf.Max(0f, ResolveProjectileLifetime(lifetime));
            projectileRadius = radius;
            projectileChargeSeconds = Mathf.Max(0f, chargeSeconds);
            projectileTrailSeconds = Mathf.Max(0.025f, trailSeconds);
            projectileTrailWidthMultiplier = Mathf.Max(0.1f, trailWidth);
            Color prefabIndicatorColor = indicatorColor;
            bool hasPrefabIndicatorColor = prefabIndicatorColor != Color.clear;
            Color resolvedColor = ResolveInitialProjectileColor(color);
            projectileColor = resolvedColor;
            chargingColor = resolvedColor;
            launchedColor = resolvedColor;
            indicatorColor = hasPrefabIndicatorColor ? prefabIndicatorColor : resolvedColor;
            ConfigureSpecialStateColors(resolvedColor, resolvedColor, null);
            customTrailColorConfigured = false;
            customIndicatorColorConfigured = hasPrefabIndicatorColor;
            launchReplacementPrefab = null;
            ownerBullets = nextOwnerBullets;
            ownerBoss = ownerBullets != null ? ownerBullets.GetComponentInParent<BossAI>() : null;
            ownerMinion = ownerBullets != null ? ownerBullets.GetComponentInParent<Minion>() : null;
            if (!activeProjectiles.Contains(this))
            {
                activeProjectiles.Add(this);
            }
            AssignInterceptGroup(existingInterceptGroupId);

            ownerBoss?.RegisterActiveProjectile(this);
            ownerMinion?.RegisterActiveProjectile(this);
            bulletDamage = nextBulletDamage;
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            baseLocalScale = transform.localScale;
            chargeGrowthStartScale = baseLocalScale;
            chargeGrowthEndScale = baseLocalScale;
            chargeAnchor = null;
            growScaleWhileCharging = false;
            playSmokeOnLaunch = false;
            aimAtPlayerOnLaunchSpreadDegrees = 0f;
            canBeIntercepted = true;
            interceptPending = false;
            SetParryLockOnIndicatorVisible(false);
            splitOnObstacle = false;
            splitRadiallyOnLaunch = false;
            splitRemaining = 0;
            radialSplitBulletCount = 0;
            radialSplitStartAngleDegrees = 0f;
            radialSplitDelaySeconds = 0f;
            radialSplitAt = 0f;
            suppressPathIndicator = nextSuppressPathIndicator;
            delayPathIndicatorUntilLaunch = false;
            ResetClonedPathIndicators();
            launched = projectileChargeSeconds <= 0f;
            lastWallCheckPosition = transform.position;
            chargeEndsAt = Time.time + projectileChargeSeconds;
            float launchTime = launched ? Time.time : chargeEndsAt;
            destroyAt = launchTime + projectileLifetime;
            ConfigureHoming(enableHoming, homingSeconds, nextHomingTurnDegrees, launchTime);

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            EnsureProjectileShape();
            if (body != null)
            {
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.linearVelocity = launched ? flightDirection * projectileSpeed * EnemyTimeScale.Current : Vector2.zero;
            }

            if (launched)
            {
                SetChargeVfxVisible(false);
                BeginPathIndicator();
            }
            else
            {
                UpdateChargeVfx();
                UpdatePathIndicatorPreview();
            }

            OnProjectileInitialized();
        }

        protected virtual void Update()
        {
            if (isDestroying)
            {
                return;
            }

            if (PlayerCombatController.IsExecutionCinematicActive)
            {
                PauseForExecution();
                return;
            }

            ResumeFromExecutionPause();
            ApplyEnemyTimeScaleDeadlineCompensation();
            TickParryLockOnIndicator();
            OnProjectileTick();
            if (isDestroying)
            {
                return;
            }

            if (!launched)
            {
                TickCharge();
                lastWallCheckPosition = transform.position;
            }
            else
            {
                if (TryDestroyIfCrossedWall())
                {
                    return;
                }

                TickHoming();
                if (body != null)
                {
                    body.linearVelocity = flightDirection * projectileSpeed * EnemyTimeScale.Current;
                }
                TickRadialSplitDelay();
                TickPathIndicator();
                lastWallCheckPosition = transform.position;
            }
            if (Time.time >= destroyAt)
            {
                DestroyProjectile(EnemyProjectileDestroyReason.Expired);
            }
        }

        private void ApplyEnemyTimeScaleDeadlineCompensation()
        {
            float timeDebt = EnemyTimeScale.DeltaTimeDebt;
            if (timeDebt <= 0f)
            {
                return;
            }

            destroyAt += timeDebt;
            chargeEndsAt += timeDebt;
            if (radialSplitAt > 0f)
            {
                radialSplitAt += timeDebt;
            }

            if (pathIndicatorEndsAt > 0f)
            {
                pathIndicatorEndsAt += timeDebt;
            }

            ExtendSpecialTimers(timeDebt);
        }
        protected virtual void OnProjectileAwake() { }
        protected virtual void OnProjectileInitialized() { }
        protected virtual void OnProjectileTick() { }
        protected virtual void OnProjectileChargeTick() { }
        protected virtual void OnProjectileLaunched() { }
        protected virtual void TickHoming() { }
        protected virtual void UpdateHomingChargeBlink() { }
        protected virtual bool ShouldAimAtPlayerOnLaunch()
        {
            return false;
        }
        protected virtual void ExtendSpecialTimers(float pausedSeconds) { }

        protected void InitializeStationaryParryTarget(float radius, Color color)
        {
            projectileSpeed = 0f;
            projectileLifetime = float.PositiveInfinity;
            projectileRadius = Mathf.Max(0.01f, radius);
            projectileColor = color != Color.clear ? color : Color.white;
            chargingColor = projectileColor;
            launchedColor = projectileColor;
            ownerBullets = null;
            ownerBoss = null;
            ownerMinion = null;
            bulletDamage = 0;
            flightDirection = Vector2.up;
            baseLocalScale = transform.localScale;
            chargeGrowthStartScale = baseLocalScale;
            chargeGrowthEndScale = baseLocalScale;
            chargeAnchor = null;
            growScaleWhileCharging = false;
            playSmokeOnLaunch = false;
            canBeIntercepted = true;
            interceptPending = false;
            resolved = false;
            isDestroying = false;
            launched = true;
            suppressPathIndicator = true;
            destroyAt = float.PositiveInfinity;
            chargeEndsAt = Time.time;
            lastWallCheckPosition = transform.position;

            if (!activeProjectiles.Contains(this))
            {
                activeProjectiles.Add(this);
            }

            UnregisterInterceptGroup();
            AssignInterceptGroup(0);

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            EnsureProjectileShape();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.linearVelocity = Vector2.zero;
            }

            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            SetParryLockOnIndicatorVisible(false);
            OnProjectileInitialized();
        }

    }
}
