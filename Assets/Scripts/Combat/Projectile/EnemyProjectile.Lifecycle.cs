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
            projectileLifetime = lifetime;
            projectileRadius = radius;
            projectileChargeSeconds = Mathf.Max(0f, chargeSeconds);
            projectileTrailSeconds = Mathf.Max(0.025f, trailSeconds);
            projectileTrailWidthMultiplier = Mathf.Max(0.1f, trailWidth);
            projectileColor = color;
            chargingColor = color;
            launchedColor = color;
            homingBlinkColor = color;
            indicatorColor = color;
            homingBlinkPhase = 0f;
            customTrailColorConfigured = false;
            customIndicatorColorConfigured = false;
            launchReplacementPrefab = null;
            homingEnabled = enableHoming;
            this.homingSeconds = homingEnabled
                ? Mathf.Max(0.01f, homingSeconds > 0f ? homingSeconds : DefaultHomingSeconds)
                : 0f;
            homingTurnDegreesPerSecond = homingEnabled
                ? Mathf.Max(0.01f, nextHomingTurnDegrees > 0f ? nextHomingTurnDegrees : DefaultHomingTurnDegreesPerSecond)
                : 0f;
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
            ResetClonedPathIndicators();
            launched = projectileChargeSeconds <= 0f;
            lastWallCheckPosition = transform.position;
            chargeEndsAt = Time.time + projectileChargeSeconds;
            float launchTime = launched ? Time.time : chargeEndsAt;
            homingEndsAt = launchTime + this.homingSeconds;
            destroyAt = launchTime + lifetime;

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
                body.linearVelocity = launched ? flightDirection * projectileSpeed : Vector2.zero;
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
                TickRadialSplitDelay();
                TickPathIndicator();
                lastWallCheckPosition = transform.position;
            }
            if (Time.time >= destroyAt)
            {
                DestroyProjectile(EnemyProjectileDestroyReason.Expired);
            }
        }
        protected virtual void OnProjectileAwake() { }
        protected virtual void OnProjectileInitialized() { }
        protected virtual void OnProjectileTick() { }
        protected virtual void OnProjectileChargeTick() { }
        protected virtual void OnProjectileLaunched() { }

    }
}
