using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        protected virtual void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            projectileTrail = GetComponent<TrailRenderer>();
            if (projectileTrail != null)
            {
                prefabTrailStartColor = projectileTrail.startColor;
                prefabTrailEndColor = projectileTrail.endColor;
                prefabTrailColorGradient = CloneGradient(projectileTrail.colorGradient);
                hasPrefabTrailColors = true;
            }

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
            RestorePooledComponents();
            ResetPooledRuntimeState();
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
            ownerTransform = ownerBullets != null ? ownerBullets.transform : null;
            if (!activeProjectiles.Contains(this))
            {
                activeProjectiles.Add(this);
            }
            AssignInterceptGroup(existingInterceptGroupId);

            ownerBoss?.RegisterActiveProjectile(this);
            ownerMinion?.RegisterActiveProjectile(this);
            bulletDamage = nextBulletDamage;
            playerHitKnockbackSpeed = 0f;
            playerHitKnockbackStaggerSeconds = 0f;
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
            radialSplitPrefabOverride = null;
            radialSplitBaseRadiusOverride = 0f;
            suppressPathIndicator = nextSuppressPathIndicator;
            delayPathIndicatorUntilLaunch = false;
            preserveLaunchDirectionOnLaunch = false;
            ignorePlayerCollision = false;
            ignoresWallsOverride = false;
            externalMotionDriven = false;
            reflectedByPlayer = false;
            reflectedDamage = 0;
            reflectedTarget = null;
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

        private void ResetPooledRuntimeState()
        {
            resolved = false;
            isDestroying = false;
            ownerSlotReleased = false;
            pausedByExecution = false;
            executionPauseStartedAt = 0f;
            ResetCinematicRuntimeState();
            runtimeHomingActive = false;
            runtimeHomingEndsAt = 0f;
            runtimeHomingTurnDegreesPerSecond = 0f;
            runtimeFlightSpeedCurveActive = false;
            runtimeFlightSpeedCurve = null;
            runtimeFlightSpeedCurveBaseSpeed = 0f;
            runtimeFlightSpeedCurveSeconds = 0f;
            runtimeFlightSpeedCurveElapsed = 0f;
            radialSplitImminentFired = false;
            Launched = null;
            RadialSplit = null;
            RadialSplitImminent = null;
            Destroyed = null;
        }

        private void RestorePooledComponents()
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = true;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = true;
            }
        }

        protected virtual void Update()
        {
            if (isDestroying)
            {
                return;
            }

            if (PlayerCombatController.IsExecutionCinematicActive && !ignoresExecutionPause)
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

                if (reflectedByPlayer && TryResolveReflectedEnemyCollisionSweep())
                {
                    return;
                }

                if (!externalMotionDriven)
                {
                    TickRuntimeFlightSpeedCurve();
                    if (reflectedByPlayer)
                    {
                        RefreshReflectedDirection();
                    }
                    else
                    {
                        TickHoming();
                        TickRuntimeHoming();
                    }
                }

                bool heldForCinematicClearance = ApplyCinematicPlayerClearance();
                if (!externalMotionDriven
                    && !heldForCinematicClearance
                    && body != null)
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
            ExtendRuntimeHomingTimer(timeDebt);
        }
        protected virtual void OnProjectileAwake() { }
        protected virtual void OnProjectileInitialized() { }
        protected virtual void OnProjectileTick() { }
        protected virtual void OnProjectileChargeTick() { }
        protected virtual void OnProjectileLaunched() { }
        protected virtual void OnProjectileReflected() { }
        protected virtual void TickHoming() { }
        protected virtual void UpdateHomingChargeBlink() { }
        protected virtual bool ShouldAimAtPlayerOnLaunch()
        {
            return false;
        }
        protected virtual void ExtendSpecialTimers(float pausedSeconds) { }

        protected void InitializeStationaryParryTarget(float radius, Color color)
        {
            RestorePooledComponents();
            ResetPooledRuntimeState();
            projectileSpeed = 0f;
            projectileLifetime = float.PositiveInfinity;
            projectileRadius = Mathf.Max(0.01f, radius);
            projectileColor = color != Color.clear ? color : Color.white;
            chargingColor = projectileColor;
            launchedColor = projectileColor;
            ownerBullets = null;
            ownerBoss = null;
            ownerMinion = null;
            ownerTransform = null;
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
            preserveLaunchDirectionOnLaunch = false;
            ignorePlayerCollision = false;
            ignoresWallsOverride = false;
            externalMotionDriven = false;
            reflectedByPlayer = false;
            reflectedDamage = 0;
            reflectedTarget = null;
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
