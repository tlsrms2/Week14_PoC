using System.Collections.Generic;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private const string BulletVisualName = "BulletVisual";
        private const string ChargeVfxName = "ChargeVfx";
        private const string PathIndicatorName = "PathIndicator";
        private const string HomingAimReticleName = "HomingAimReticle";
        private const string ParryLockOnIndicatorName = "ParryLockOnIndicator";
        private const string LegacyParryLockOnIndicatorName = "ProjectileLockOnIndicator";
        private const string WallLayerName = "Wall";
        private const int MaxPathDashCount = 160;
        private const int HomingAimReticleLineCount = 3;
        private const int HomingAimReticleCircleSegments = 40;
        private const float PathDashLength = 0.2f;
        private const float PathDashGap = 0.14f;
        private const float DefaultHomingSeconds = 0.8f;
        private const float DefaultHomingTurnDegreesPerSecond = 540f;
        private const float HomingChargeBlinkMinRate = 2f;
        private const float HomingChargeBlinkMaxRate = 8f;
        private const float HomingChargeSolidColorRemainingRatio = 0.25f;
        private float projectileSpeed;
        private float projectileLifetime;
        private float projectileRadius;
        private float projectileChargeSeconds;
        private float projectileTrailSeconds;
        private float projectileTrailWidthMultiplier;
        private Color projectileColor;
        private Color chargingColor;
        private Color launchedColor;
        private Color homingBlinkColor;
        private Color indicatorColor;
        private float homingBlinkPhase;
        private bool homingEnabled;
        private float homingTurnDegreesPerSecond;
        private float homingSeconds;
        private float chargeDriftSpeed;
        private float homingEndsAt;
        private float chargeEndsAt;
        private Rigidbody2D body;
        private LineRenderer chargeVfx;
        private TrailRenderer projectileTrail;
        private EnemyProjectile launchReplacementPrefab;
        private bool customTrailColorConfigured;
        private bool customIndicatorColorConfigured;
        [SerializeField] private GameObject parryLockOnIndicatorRoot;
        [SerializeField] private MouseParryReticle parryLockOnReticle;
        [SerializeField] private Transform parryLockOnRotatingRoot;
        [SerializeField] private float parryLockOnRotationSpeedDegrees = 180f;
        private readonly List<LineRenderer> pathIndicatorDashes = new();
        private readonly List<LineRenderer> homingAimReticleLines = new();
        private Transform pathIndicatorRoot;
        private BulletGauge ownerBullets;
        private BossAI ownerBoss;
        private Drone ownerDrone;
        private int bulletDamage;
        private float destroyAt;
        private Vector2 flightDirection = Vector2.left;
        private Vector3 baseLocalScale = Vector3.one;
        private Vector3 chargeGrowthStartScale;
        private Vector3 chargeGrowthEndScale;
        private Color launchSmokeColor;
        private float launchSmokeScale = 1f;
        private Transform chargeAnchor;
        private bool aimAtPlayerWhileCharging = true;
        private bool aimAtPlayerOnLaunch;
        private float aimAtPlayerOnLaunchSpreadDegrees;
        private bool growScaleWhileCharging;
        private bool playSmokeOnLaunch;
        private bool canBeIntercepted = true;
        private bool splitOnObstacle;
        private bool splitRadiallyOnLaunch;
        private int splitRemaining;
        private float splitAngleDegrees = 45f;
        private int radialSplitBulletCount;
        private float radialSplitStartAngleDegrees;
        private float radialSplitDelaySeconds;
        private float radialSplitAt;
        private float radialSplitSfxLeadSeconds;
        private bool radialSplitImminentFired;
        private float splitSpeedMultiplier = 1f;
        private float splitRadiusMultiplier = 0.6f;
        private float splitLifetimeMultiplier = 0.85f;
        private bool resolved;
        private bool isDestroying;
        private bool launched;
        private bool interceptPending;
        private bool ownerSlotReleased;
        private bool pathIndicatorActive;
        private bool suppressPathIndicator;
        private bool parryLockOnIndicatorVisible;
        private Vector2 pathIndicatorStart;
        private Vector2 pathIndicatorDirection = Vector2.left;
        private float pathIndicatorLength;
        private float pathIndicatorEndsAt;
        private Vector2 lastWallCheckPosition;
        private static readonly List<EnemyProjectile> activeProjectiles = new();
        private float executionPauseStartedAt;
        private bool pausedByExecution;
        private static Material chargeVfxMaterial;

        public event System.Action<EnemyProjectile> Launched;
        public event System.Action<EnemyProjectile> RadialSplit;
        public event System.Action<EnemyProjectile> RadialSplitImminent;

        public Vector2 IncomingDirection => flightDirection;
        public bool IsCharging => !resolved && !isDestroying && !launched;
        public bool CanBeIntercepted => !resolved && !isDestroying && canBeIntercepted && !interceptPending;
        public float LockOnRadius => Mathf.Max(0.24f, projectileRadius * 2.6f);
        public static IReadOnlyList<EnemyProjectile> ActiveProjectiles => activeProjectiles;

        public void SetParryLockOnIndicatorVisible(bool visible)
        {
            ResolveParryLockOnIndicator();
            bool nextVisible = visible && CanBeIntercepted;
            parryLockOnIndicatorVisible = nextVisible;

            if (nextVisible && parryLockOnIndicatorRoot != null && parryLockOnIndicatorRoot != gameObject)
            {
                parryLockOnIndicatorRoot.SetActive(true);
            }

            if (parryLockOnReticle != null)
            {
                parryLockOnReticle.SetForceOscillationWhileThreatened(nextVisible);
                parryLockOnReticle.SetVisible(nextVisible);
                parryLockOnReticle.SetThreatened(nextVisible);
            }

            if (!nextVisible && parryLockOnIndicatorRoot != null && parryLockOnIndicatorRoot != gameObject)
            {
                parryLockOnIndicatorRoot.SetActive(false);
            }
        }

        public static void DestroyAllActive()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile != null)
                {
                    projectile.DestroyFromOwner();
                }
            }

            activeProjectiles.Clear();
        }

        public static EnemyProjectile Spawn(
            EnemyProjectile prefab,
            BulletGauge ownerBullets,
            Vector3 position,
            Vector2 direction,
            int bulletDamage,
            float chargeSeconds,
            float speed,
            float lifetime,
            float radius,
            Color color,
            float trailSeconds,
            float trailWidth,
            bool homingEnabled,
            float homingSeconds,
            float homingTurnDegrees)
        {
            if (prefab == null)
            {
                return null;
            }

            return SpawnInternal(
                prefab,
                ownerBullets,
                position,
                direction,
                bulletDamage,
                chargeSeconds,
                speed,
                lifetime,
                radius,
                color,
                trailSeconds,
                trailWidth,
                homingEnabled,
                homingSeconds,
                homingTurnDegrees);
        }

        public void ConfigureStateColors(Color nextChargingColor, Color nextLaunchedColor, Color? nextHomingBlinkColor = null)
        {
            chargingColor = nextChargingColor;
            launchedColor = nextLaunchedColor;
            homingBlinkColor = nextHomingBlinkColor ?? nextLaunchedColor;
            homingBlinkPhase = 0f;
            ApplyProjectileColor(launched ? launchedColor : chargingColor);
        }

        public void ConfigureTrailColor(Color nextTrailColor)
        {
            if (projectileTrail == null)
            {
                projectileTrail = GetComponent<TrailRenderer>();
            }

            if (projectileTrail == null)
            {
                return;
            }

            Color endColor = nextTrailColor;
            endColor.a = 0f;
            projectileTrail.startColor = nextTrailColor;
            projectileTrail.endColor = endColor;
            customTrailColorConfigured = true;
        }

        public void ConfigureIndicatorColor(Color nextIndicatorColor)
        {
            indicatorColor = nextIndicatorColor;
            customIndicatorColorConfigured = true;
        }

        public void ConfigureLaunchReplacementPrefab(EnemyProjectile nextLaunchPrefab)
        {
            launchReplacementPrefab = nextLaunchPrefab != null && nextLaunchPrefab != this
                ? nextLaunchPrefab
                : null;
        }

        public void ConfigureChargeMotion(float driftSpeed, bool aimAtPlayer)
        {
            chargeDriftSpeed = Mathf.Max(0f, driftSpeed);
            aimAtPlayerWhileCharging = aimAtPlayer;
            aimAtPlayerOnLaunch = false;
            aimAtPlayerOnLaunchSpreadDegrees = 0f;
        }

        public void ConfigureChargeMotion(float driftSpeed, bool aimAtPlayer, bool aimAtLaunch)
        {
            ConfigureChargeMotion(driftSpeed, aimAtPlayer, aimAtLaunch, 0f);
        }

        public void ConfigureChargeMotion(float driftSpeed, bool aimAtPlayer, bool aimAtLaunch, float launchSpreadDegrees)
        {
            chargeDriftSpeed = Mathf.Max(0f, driftSpeed);
            aimAtPlayerWhileCharging = aimAtPlayer;
            aimAtPlayerOnLaunch = aimAtLaunch;
            aimAtPlayerOnLaunchSpreadDegrees = Mathf.Max(0f, launchSpreadDegrees);
        }

        public void ConfigureChargeAnchor(Transform anchor)
        {
            chargeAnchor = anchor;
            if (IsCharging)
            {
                SnapToChargeAnchor();
            }
        }

        public void ConfigureObstacleSplit(
            int splitCount,
            float angleDegrees,
            float speedMultiplier,
            float radiusMultiplier,
            float lifetimeMultiplier)
        {
            splitOnObstacle = splitCount > 0;
            splitRemaining = Mathf.Max(0, splitCount);
            splitAngleDegrees = Mathf.Max(0f, angleDegrees);
            splitSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
            splitRadiusMultiplier = Mathf.Clamp(radiusMultiplier, 0.05f, 1f);
            splitLifetimeMultiplier = Mathf.Clamp(lifetimeMultiplier, 0.05f, 1f);

        }

        public void ConfigureRadialSplitOnLaunch(
            int bulletCount,
            float startAngleDegrees,
            float delaySeconds,
            float speedMultiplier,
            float radiusMultiplier,
            float lifetimeMultiplier)
        {
            splitRadiallyOnLaunch = bulletCount > 0;
            radialSplitBulletCount = Mathf.Max(1, bulletCount);
            radialSplitStartAngleDegrees = startAngleDegrees;
            radialSplitDelaySeconds = Mathf.Max(0f, delaySeconds);
            radialSplitAt = launched ? Time.time + radialSplitDelaySeconds : 0f;
            splitSpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
            splitRadiusMultiplier = Mathf.Clamp(radiusMultiplier, 0.05f, 1f);
            splitLifetimeMultiplier = Mathf.Clamp(lifetimeMultiplier, 0.05f, 1f);

            if (splitRadiallyOnLaunch && launched && radialSplitDelaySeconds <= 0f && !resolved && !isDestroying)
            {
                SplitRadiallyOnLaunch();
            }
        }

        public void ConfigureRadialSplitSfxLead(float leadSeconds)
        {
            radialSplitSfxLeadSeconds = Mathf.Max(0f, leadSeconds);
        }

        public void ConfigureProjectileSize(float radius)
        {
            projectileRadius = Mathf.Max(0.01f, radius);

            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.radius = projectileRadius;
            }

            TrailRenderer trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startWidth = Mathf.Max(trail.startWidth, projectileRadius * 0.35f);
            }
        }

        public void MultiplyProjectileScale(float scaleMultiplier)
        {
            float multiplier = Mathf.Max(0.01f, scaleMultiplier);
            transform.localScale *= multiplier;
            baseLocalScale = transform.localScale;
        }

        public void ConfigureChargeGrowth(float startScaleMultiplier, float endScaleMultiplier)
        {
            chargeGrowthStartScale = baseLocalScale * Mathf.Max(0.01f, startScaleMultiplier);
            chargeGrowthEndScale = baseLocalScale * Mathf.Max(0.01f, endScaleMultiplier);
            growScaleWhileCharging = true;
            playSmokeOnLaunch = false;

            if (IsCharging)
            {
                transform.localScale = chargeGrowthStartScale;
            }
        }

        public void ConfigureChargeGrowth(float startScaleMultiplier, float endScaleMultiplier, Color smokeColor, float smokeScale)
        {
            chargeGrowthStartScale = baseLocalScale * Mathf.Max(0.01f, startScaleMultiplier);
            chargeGrowthEndScale = baseLocalScale * Mathf.Max(0.01f, endScaleMultiplier);
            launchSmokeColor = smokeColor;
            launchSmokeScale = Mathf.Max(0.1f, smokeScale);
            growScaleWhileCharging = true;
            playSmokeOnLaunch = true;

            if (IsCharging)
            {
                transform.localScale = chargeGrowthStartScale;
            }
        }

        public void ConfigureInterceptable(bool interceptable)
        {
            canBeIntercepted = interceptable;
            if (!canBeIntercepted)
            {
                SetParryLockOnIndicatorVisible(false);
            }
        }

        private static EnemyProjectile SpawnInternal(
            EnemyProjectile prefab,
            BulletGauge ownerBullets,
            Vector3 position,
            Vector2 direction,
            int bulletDamage,
            float chargeSeconds,
            float speed, float lifetime, float radius,
            Color color, float trailSeconds, float trailWidth,
            bool homingEnabled,
            float homingSeconds,
            float homingTurnDegrees,
            bool suppressPathIndicator = false)
        {
            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            EnemyProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            projectile.Initialize(
                direction,
                bulletDamage,
                ownerBullets,
                chargeSeconds,
                speed,
                lifetime,
                radius,
                color,
                trailSeconds,
                trailWidth,
                homingEnabled,
                homingSeconds,
                homingTurnDegrees,
                suppressPathIndicator);
            ProjectileVfx.ApplyVisibility(
                projectile.gameObject, color, radius, trailSeconds, trailWidth);
            projectile.BeginTrail();
            return projectile;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            ResolveParryLockOnIndicator();
            SetParryLockOnIndicatorVisible(false);
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
            bool nextSuppressPathIndicator)
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
            ownerDrone = ownerBullets != null ? ownerBullets.GetComponentInParent<Drone>() : null;
            if (!activeProjectiles.Contains(this))
            {
                activeProjectiles.Add(this);
            }

            ownerBoss?.RegisterActiveProjectile(this);
            ownerDrone?.RegisterActiveProjectile(this);
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
        }

        private void Update()
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
                DestroyProjectile();
            }
        }

        private void TickCharge()
        {
            SnapToChargeAnchor();

            if (aimAtPlayerWhileCharging)
            {
                AimAtPlayerWhileCharging();
            }

            if (body != null)
            {
                body.linearVelocity = flightDirection * chargeDriftSpeed;
            }

            UpdateChargeGrowth();
            UpdateHomingChargeBlink();
            UpdateChargeTrail();
            UpdateChargeVfx();
            UpdatePathIndicatorPreview();
            if (Time.time < chargeEndsAt)
            {
                return;
            }

            SnapToChargeAnchor();
            if (TryReplaceWithLaunchPrefab())
            {
                return;
            }

            launched = true;
            chargeAnchor = null;
            ApplyProjectileColor(launchedColor);
            if (growScaleWhileCharging)
            {
                transform.localScale = chargeGrowthEndScale;
                baseLocalScale = transform.localScale;
            }

            if (homingEnabled)
            {
                AimAtPlayerWhileCharging();
            }
            else if (aimAtPlayerOnLaunch)
            {
                AimAtPlayerWhileCharging(aimAtPlayerOnLaunchSpreadDegrees);
            }

            if (playSmokeOnLaunch)
            {
                ProjectileVfx.PlayHogSmokeBurst(transform.position, launchSmokeColor, launchSmokeScale, 18);
            }

            radialSplitAt = splitRadiallyOnLaunch ? Time.time + radialSplitDelaySeconds : 0f;

            SetChargeVfxVisible(false);
            BeginPathIndicator();
            if (body != null)
            {
                body.linearVelocity = flightDirection * projectileSpeed;
            }

            Launched?.Invoke(this);
        }

        private bool TryReplaceWithLaunchPrefab()
        {
            if (launchReplacementPrefab == null)
            {
                return false;
            }

            if (homingEnabled)
            {
                AimAtPlayerWhileCharging();
            }
            else if (aimAtPlayerOnLaunch)
            {
                AimAtPlayerWhileCharging(aimAtPlayerOnLaunchSpreadDegrees);
            }

            EnemyProjectile replacement = SpawnInternal(
                launchReplacementPrefab,
                ownerBullets,
                transform.position,
                flightDirection,
                bulletDamage,
                0f,
                projectileSpeed,
                projectileLifetime,
                projectileRadius,
                launchedColor,
                projectileTrailSeconds,
                projectileTrailWidthMultiplier,
                homingEnabled,
                homingSeconds,
                homingTurnDegreesPerSecond,
                suppressPathIndicator);

            if (replacement == null)
            {
                return false;
            }

            CopyLaunchRuntimeStateTo(replacement);
            if (growScaleWhileCharging)
            {
                replacement.transform.localScale = chargeGrowthEndScale;
                replacement.baseLocalScale = replacement.transform.localScale;
            }

            if (playSmokeOnLaunch)
            {
                ProjectileVfx.PlayHogSmokeBurst(transform.position, launchSmokeColor, launchSmokeScale, 18);
            }

            replacement.radialSplitAt = replacement.splitRadiallyOnLaunch
                ? Time.time + replacement.radialSplitDelaySeconds
                : 0f;
            replacement.SetChargeVfxVisible(false);
            replacement.BeginPathIndicator();
            if (replacement.body != null)
            {
                replacement.body.linearVelocity = replacement.flightDirection * replacement.projectileSpeed;
            }

            replacement.Launched?.Invoke(replacement);
            RetireAfterLaunchReplacement();
            return true;
        }

        private void CopyLaunchRuntimeStateTo(EnemyProjectile replacement)
        {
            replacement.ConfigureStateColors(chargingColor, launchedColor, homingBlinkColor);
            if (customTrailColorConfigured && projectileTrail != null)
            {
                replacement.ConfigureTrailColor(projectileTrail.startColor);
            }

            if (customIndicatorColorConfigured)
            {
                replacement.ConfigureIndicatorColor(indicatorColor);
            }

            replacement.ConfigureChargeMotion(
                chargeDriftSpeed,
                aimAtPlayerWhileCharging,
                aimAtPlayerOnLaunch,
                aimAtPlayerOnLaunchSpreadDegrees);
            replacement.canBeIntercepted = canBeIntercepted;
            replacement.splitOnObstacle = splitOnObstacle;
            replacement.splitRadiallyOnLaunch = splitRadiallyOnLaunch;
            replacement.splitRemaining = splitRemaining;
            replacement.splitAngleDegrees = splitAngleDegrees;
            replacement.radialSplitBulletCount = radialSplitBulletCount;
            replacement.radialSplitStartAngleDegrees = radialSplitStartAngleDegrees;
            replacement.radialSplitDelaySeconds = radialSplitDelaySeconds;
            replacement.radialSplitSfxLeadSeconds = radialSplitSfxLeadSeconds;
            replacement.splitSpeedMultiplier = splitSpeedMultiplier;
            replacement.splitRadiusMultiplier = splitRadiusMultiplier;
            replacement.splitLifetimeMultiplier = splitLifetimeMultiplier;
            replacement.Launched = Launched;
            replacement.RadialSplit = RadialSplit;
            replacement.RadialSplitImminent = RadialSplitImminent;
            replacement.SetParryLockOnIndicatorVisible(parryLockOnIndicatorVisible);
        }

        private void RetireAfterLaunchReplacement()
        {
            isDestroying = true;
            activeProjectiles.Remove(this);
            ReleaseOwnerProjectileSlot();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            SetParryLockOnIndicatorVisible(false);

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (projectileTrail != null && renderers[i] == projectileTrail)
                {
                    continue;
                }

                renderers[i].enabled = false;
            }

            float destroyDelay = 0f;
            if (projectileTrail != null)
            {
                projectileTrail.emitting = false;
                destroyDelay = Mathf.Max(0.01f, projectileTrail.time);
            }

            Destroy(gameObject, destroyDelay);
        }

        private void SnapToChargeAnchor()
        {
            if (chargeAnchor == null)
            {
                return;
            }

            Vector3 position = chargeAnchor.position;
            position.z = transform.position.z;
            transform.position = position;
            if (body != null)
            {
                body.position = position;
            }
        }

        private void UpdateChargeGrowth()
        {
            if (!growScaleWhileCharging || projectileChargeSeconds <= 0f)
            {
                return;
            }

            float t = 1f - Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds);
            t = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(chargeGrowthStartScale, chargeGrowthEndScale, t);
        }

        private void UpdateHomingChargeBlink()
        {
            if (!homingEnabled || projectileChargeSeconds <= 0f)
            {
                return;
            }

            float remainingRatio = Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds);
            if (remainingRatio <= HomingChargeSolidColorRemainingRatio)
            {
                ApplyProjectileColor(homingBlinkColor);
                return;
            }

            float blinkRate = Mathf.Lerp(HomingChargeBlinkMaxRate, HomingChargeBlinkMinRate, remainingRatio);
            homingBlinkPhase += Time.deltaTime * blinkRate;
            Color nextColor = Mathf.Repeat(homingBlinkPhase, 1f) >= 0.5f
                ? homingBlinkColor
                : chargingColor;
            ApplyProjectileColor(nextColor);
        }

        private void UpdateChargeTrail()
        {
            if (projectileTrail == null)
            {
                projectileTrail = GetComponent<TrailRenderer>();
            }

            if (projectileTrail == null)
            {
                return;
            }

            projectileTrail.emitting = true;
            projectileTrail.AddPosition(transform.position);
        }

        private void BeginTrail()
        {
            projectileTrail = GetComponent<TrailRenderer>();
            if (projectileTrail == null)
            {
                return;
            }

            projectileTrail.Clear();
            projectileTrail.emitting = true;
            projectileTrail.AddPosition(transform.position);
        }

        private void AimAtPlayerWhileCharging(float spreadDegrees = 0f)
        {
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

            flightDirection = toTarget.normalized;
            if (spreadDegrees > 0f)
            {
                flightDirection = RotateDirection(flightDirection, Random.Range(-spreadDegrees * 0.5f, spreadDegrees * 0.5f)).normalized;
            }

            float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void TickHoming()
        {
            if (!homingEnabled || resolved || isDestroying || Time.time >= homingEndsAt || homingTurnDegreesPerSecond <= 0f || projectileSpeed <= 0f)
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

            float maxRadians = homingTurnDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime;
            Vector3 nextDirection = Vector3.RotateTowards(flightDirection, toTarget.normalized, maxRadians, 0f);
            flightDirection = ((Vector2)nextDirection).normalized;
            body.linearVelocity = flightDirection * projectileSpeed;
            float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void TickRadialSplitDelay()
        {
            if (!splitRadiallyOnLaunch || resolved || isDestroying || radialSplitAt <= 0f)
            {
                return;
            }

            if (!radialSplitImminentFired && Time.time >= radialSplitAt - radialSplitSfxLeadSeconds)
            {
                radialSplitImminentFired = true;
                RadialSplitImminent?.Invoke(this);
            }

            if (Time.time < radialSplitAt)
            {
                return;
            }

            SplitRadiallyOnLaunch();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerProjectile playerProjectile = other.GetComponentInParent<PlayerProjectile>();
            if (playerProjectile != null)
            {
                playerProjectile.TryDestroyByEnemyProjectileClash(this);
                return;
            }

            if (interceptPending && other.GetComponentInParent<PlayerCombatController>() != null)
            {
                return;
            }

            if (IsCharging)
            {
                return;
            }

            if (IsWallCollider(other))
            {
                DestroyProjectile();
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null)
            {
                BossAI hitBoss = other.GetComponentInParent<BossAI>();
                if (hitBoss != null)
                {
                    if (hitBoss == ownerBoss)
                    {
                        return;
                    }

                    DestroyProjectile();
                    return;
                }

                Drone hitDrone = other.GetComponentInParent<Drone>();
                if (hitDrone != null)
                {
                    if (hitDrone == ownerDrone)
                    {
                        return;
                    }

                    DestroyProjectile();
                    return;
                }

                if (other.GetComponentInParent<EnemyProjectile>() != null)
                {
                    return;
                }

                if (TrySplitOnObstacle(other))
                {
                    return;
                }

                DestroyProjectile();
                return;
            }

            if (resolved)
            {
                return;
            }

            resolved = true;
            player.ReceiveAttack(bulletDamage, transform.position, flightDirection);
            DestroyProjectile();
        }

        private bool TrySplitOnObstacle(Collider2D obstacle)
        {
            if (!splitOnObstacle || splitRemaining <= 0 || obstacle == null)
            {
                return false;
            }

            Vector2 normal = GetObstacleNormal(obstacle);
            Vector2 reflected = Vector2.Reflect(flightDirection, normal);
            if (reflected.sqrMagnitude <= 0.0001f)
            {
                reflected = -flightDirection;
            }

            ProjectileVfx.PlayHogSmokeBurst(transform.position, projectileColor, Mathf.Max(1f, projectileRadius * 2.6f), 20);
            SpawnSplitChild(RotateDirection(reflected, -splitAngleDegrees * 0.5f));
            SpawnSplitChild(RotateDirection(reflected, splitAngleDegrees * 0.5f));
            resolved = true;
            DestroyProjectile();
            return true;
        }

        private void SplitRadiallyOnLaunch()
        {
            int count = Mathf.Max(1, radialSplitBulletCount);
            float step = 360f / count;
            ProjectileVfx.PlayHogSmokeBurst(transform.position, projectileColor, Mathf.Max(1f, projectileRadius * 2.6f), count);
            RadialSplit?.Invoke(this);

            for (int i = 0; i < count; i++)
            {
                SpawnSplitChild(AngleToDirection(radialSplitStartAngleDegrees + step * i));
            }

            resolved = true;
            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            DestroyProjectile();
        }

        private Vector2 GetObstacleNormal(Collider2D obstacle)
        {
            Vector2 position = transform.position;
            Vector2 closest = obstacle.ClosestPoint(position);
            Vector2 normal = position - closest;
            if (normal.sqrMagnitude <= 0.0001f)
            {
                normal = -flightDirection;
            }

            return normal.normalized;
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void SpawnSplitChild(Vector2 direction)
        {
            EnemyProjectile child = SpawnInternal(
                this,
                ownerBullets,
                transform.position + (Vector3)(direction.normalized * Mathf.Max(0.08f, projectileRadius)),
                direction,
                bulletDamage,
                0f,
                projectileSpeed * splitSpeedMultiplier,
                projectileLifetime * splitLifetimeMultiplier,
                projectileRadius * splitRadiusMultiplier,
                projectileColor,
                0.08f,
                3f,
                homingEnabled,
                homingSeconds,
                homingTurnDegreesPerSecond,
                suppressPathIndicator: true);

            if (child == null)
            {
                return;
            }

            child.ConfigureObstacleSplit(
                splitRemaining - 1,
                splitAngleDegrees,
                splitSpeedMultiplier,
                splitRadiusMultiplier,
                splitLifetimeMultiplier);
            child.ConfigureProjectileSize(projectileRadius * splitRadiusMultiplier);
            child.MultiplyProjectileScale(splitRadiusMultiplier);
        }

        private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector2.left;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 normalized = direction.normalized;
            return new Vector2(
                normalized.x * cos - normalized.y * sin,
                normalized.x * sin + normalized.y * cos);
        }

        public bool TryDestroyByInterceptShot(out bool parried)
        {
            if (resolved || isDestroying || !canBeIntercepted)
            {
                parried = false;
                return false;
            }

            parried = true;
            resolved = true;

            DestroyProjectile();
            return true;
        }

        public bool TryReserveIntercept()
        {
            if (!CanBeIntercepted)
            {
                return false;
            }

            interceptPending = true;
            SetParryLockOnIndicatorVisible(false);
            return true;
        }

        public void CancelInterceptReservation()
        {
            if (resolved || isDestroying)
            {
                return;
            }

            interceptPending = false;
        }

        public void DestroyFromOwner()
        {
            resolved = true;
            DestroyProjectile();
        }

        private void DestroyProjectile()
        {
            if (isDestroying)
            {
                return;
            }

            isDestroying = true;
            ReleaseOwnerProjectileSlot();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            SetParryLockOnIndicatorVisible(false);

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            activeProjectiles.Remove(this);
            ReleaseOwnerProjectileSlot();
        }

        private void ReleaseOwnerProjectileSlot()
        {
            if (ownerSlotReleased)
            {
                return;
            }

            ownerSlotReleased = true;
            ownerBoss?.UnregisterActiveProjectile(this);
            ownerDrone?.UnregisterActiveProjectile(this);
        }

        private void EnsureProjectileShape()
        {
            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            if (visualRenderer != null)
            {
                visualRenderer.color = projectileColor;
                visualRenderer.sortingOrder = 20;
            }

            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.isTrigger = true;
                circleCollider.radius = projectileRadius;
                return;
            }

            Collider2D projectileCollider = GetComponent<Collider2D>();
            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
            }
        }

        private void ApplyProjectileColor(Color color)
        {
            projectileColor = color;

            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            if (visualRenderer != null)
            {
                visualRenderer.color = projectileColor;
            }

            if (!customTrailColorConfigured)
            {
                ConfigureTrailColor(projectileColor);
                customTrailColorConfigured = false;
            }
        }

        private SpriteRenderer GetProjectileVisualRenderer()
        {
            Transform visual = transform.Find(BulletVisualName);
            SpriteRenderer visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            return visualRenderer != null ? visualRenderer : GetComponent<SpriteRenderer>();
        }

        private void ResolveParryLockOnIndicator()
        {
            if (parryLockOnReticle == null && parryLockOnIndicatorRoot != null)
            {
                parryLockOnReticle = parryLockOnIndicatorRoot.GetComponentInChildren<MouseParryReticle>(true);
            }

            if (parryLockOnReticle == null)
            {
                parryLockOnReticle = GetComponentInChildren<MouseParryReticle>(true);
            }

            if (parryLockOnIndicatorRoot != null)
            {
                return;
            }

            Transform authoredRoot = transform.Find(ParryLockOnIndicatorName);
            if (authoredRoot == null)
            {
                authoredRoot = transform.Find(LegacyParryLockOnIndicatorName);
            }

            if (authoredRoot != null)
            {
                parryLockOnIndicatorRoot = authoredRoot.gameObject;
                return;
            }

            if (parryLockOnReticle != null && parryLockOnReticle.gameObject != gameObject)
            {
                parryLockOnIndicatorRoot = parryLockOnReticle.gameObject;
            }
        }

        private void TickParryLockOnIndicator()
        {
            if (!parryLockOnIndicatorVisible || Mathf.Approximately(parryLockOnRotationSpeedDegrees, 0f))
            {
                return;
            }

            Transform targetRoot = GetParryLockOnRotationRoot();
            if (targetRoot != null)
            {
                targetRoot.Rotate(0f, 0f, parryLockOnRotationSpeedDegrees * Time.deltaTime, Space.Self);
            }
        }

        private Transform GetParryLockOnRotationRoot()
        {
            if (parryLockOnRotatingRoot != null)
            {
                return parryLockOnRotatingRoot;
            }

            if (parryLockOnIndicatorRoot != null && parryLockOnIndicatorRoot != gameObject)
            {
                return parryLockOnIndicatorRoot.transform;
            }

            return parryLockOnReticle != null && parryLockOnReticle.gameObject != gameObject
                ? parryLockOnReticle.transform
                : null;
        }

        private bool ShouldShowPathIndicator()
        {
            return !suppressPathIndicator
                && projectileSpeed > 0f
                && projectileLifetime > 0f;
        }

        private void ResetClonedPathIndicators()
        {
            pathIndicatorActive = false;
            pathIndicatorLength = 0f;
            pathIndicatorEndsAt = 0f;
            pathIndicatorDashes.Clear();
            homingAimReticleLines.Clear();
            pathIndicatorRoot = null;

            Transform existing = transform.Find(PathIndicatorName);
            if (existing == null)
            {
                return;
            }

            existing.gameObject.SetActive(false);
            existing.SetParent(null, false);
            Destroy(existing.gameObject);
        }

        private bool IsHomingProjectile()
        {
            return homingEnabled;
        }

        private void UpdatePathIndicatorPreview()
        {
            if (!ShouldShowPathIndicator())
            {
                SetPathIndicatorVisible(false);
                return;
            }

            if (IsHomingProjectile())
            {
                DrawHomingPathIndicator();
                return;
            }

            DrawPathIndicator(transform.position, flightDirection, GetPathIndicatorLength(transform.position, flightDirection, projectileLifetime), 0f);
        }

        private void BeginPathIndicator()
        {
            if (!ShouldShowPathIndicator())
            {
                SetPathIndicatorVisible(false);
                return;
            }

            pathIndicatorStart = transform.position;
            pathIndicatorDirection = flightDirection.sqrMagnitude > 0.0001f ? flightDirection.normalized : Vector2.left;
            float visibleSeconds = Mathf.Max(0f, destroyAt - Time.time);
            pathIndicatorEndsAt = Time.time + visibleSeconds;
            pathIndicatorLength = GetPathIndicatorLength(pathIndicatorStart, pathIndicatorDirection, visibleSeconds);
            pathIndicatorActive = true;

            if (IsHomingProjectile())
            {
                DrawHomingPathIndicator();
                return;
            }

            DrawPathIndicator(pathIndicatorStart, pathIndicatorDirection, pathIndicatorLength, 0f);
        }

        private void TickPathIndicator()
        {
            if (!pathIndicatorActive || pathIndicatorLength <= 0f)
            {
                return;
            }

            if (Time.time >= pathIndicatorEndsAt)
            {
                SetPathIndicatorVisible(false);
                return;
            }

            if (IsHomingProjectile())
            {
                DrawHomingPathIndicator();
                return;
            }

            float travelled = Vector2.Dot((Vector2)transform.position - pathIndicatorStart, pathIndicatorDirection);
            DrawPathIndicator(pathIndicatorStart, pathIndicatorDirection, pathIndicatorLength, Mathf.Max(0f, travelled));
        }

        private float GetPathIndicatorLength(Vector2 start, Vector2 direction, float seconds)
        {
            float length = projectileSpeed * Mathf.Max(0f, seconds);
            return GetWallClippedLength(start, direction, length);
        }

        private float GetWallClippedLength(Vector2 start, Vector2 direction, float length)
        {
            return TryGetWallHit(start, direction, length, out RaycastHit2D hit)
                ? Mathf.Max(0f, hit.distance)
                : length;
        }

        private bool TryDestroyIfCrossedWall()
        {
            Vector2 currentPosition = transform.position;
            Vector2 delta = currentPosition - lastWallCheckPosition;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            if (!TryGetWallHit(lastWallCheckPosition, delta.normalized, delta.magnitude + projectileRadius, out _))
            {
                return false;
            }

            DestroyProjectile();
            return true;
        }

        private bool TryGetWallHit(Vector2 start, Vector2 direction, float distance, out RaycastHit2D hit)
        {
            hit = default;
            int wallMask = GetWallMask();
            if (wallMask == 0 || distance <= 0.001f || direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float castRadius = Mathf.Max(0.001f, projectileRadius);
            hit = Physics2D.CircleCast(start, castRadius, direction.normalized, distance, wallMask);
            return hit.collider != null;
        }

        private static bool IsWallCollider(Collider2D collider)
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            return wallLayer >= 0
                && collider != null
                && collider.gameObject.layer == wallLayer;
        }

        private static int GetWallMask()
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            return wallLayer >= 0 ? 1 << wallLayer : 0;
        }

        private void DrawHomingPathIndicator()
        {
            if (!TryGetHomingIndicatorTarget(out Vector2 start, out Vector2 direction, out Vector2 aimPoint))
            {
                SetPathIndicatorVisible(false);
                return;
            }

            float length = Vector2.Distance(start, aimPoint);
            bool blockedByWall = TryGetWallHit(start, direction, length, out RaycastHit2D wallHit);
            if (blockedByWall)
            {
                length = wallHit.distance;
            }

            if (length > 0.01f)
            {
                DrawPathIndicator(start, direction, length, 0f, true);
            }
            else
            {
                SetPathDashesVisible(false);
            }

            if (blockedByWall)
            {
                SetHomingAimReticleVisible(false);
            }
            else
            {
                DrawHomingAimReticle(aimPoint, direction);
            }
            pathIndicatorActive = true;
        }

        private bool TryGetHomingIndicatorTarget(out Vector2 start, out Vector2 direction, out Vector2 aimPoint)
        {
            start = transform.position;
            direction = flightDirection.sqrMagnitude > 0.0001f ? flightDirection.normalized : Vector2.left;
            aimPoint = start;

            PlayerCombatController target = PlayerCombatController.Active;
            if (target == null || target.Health == null || target.Health.IsDead)
            {
                return false;
            }

            Vector2 targetCenter = target.transform.position;
            Vector2 toTarget = targetCenter - start;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction = toTarget.normalized;
            aimPoint = GetHomingAimPoint(target, start, direction);
            return true;
        }

        private Vector2 GetHomingAimPoint(PlayerCombatController target, Vector2 start, Vector2 direction)
        {
            Vector2 fallback = (Vector2)target.transform.position - direction * GetPlayerContactRadius(target);
            Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>();
            if (colliders == null || colliders.Length == 0)
            {
                return fallback;
            }

            if (TryGetClosestPlayerColliderPoint(colliders, start, direction, false, out Vector2 solidPoint))
            {
                return solidPoint;
            }

            return TryGetClosestPlayerColliderPoint(colliders, start, direction, true, out Vector2 triggerPoint)
                ? triggerPoint
                : fallback;
        }

        private static float GetPlayerContactRadius(PlayerCombatController target)
        {
            PlayerCombatConfig config = target != null ? target.Config : null;
            return config != null ? Mathf.Max(0.05f, config.PlayerBodyAimRadius) : 0.35f;
        }

        private static bool TryGetClosestPlayerColliderPoint(
            Collider2D[] colliders,
            Vector2 start,
            Vector2 direction,
            bool includeTriggers,
            out Vector2 point)
        {
            point = start;
            float bestDistanceSqr = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || (!includeTriggers && collider.isTrigger))
                {
                    continue;
                }

                Vector2 closest = collider.ClosestPoint(start);
                Vector2 delta = closest - start;
                if (Vector2.Dot(direction, delta) < -0.001f)
                {
                    continue;
                }

                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = distanceSqr;
                point = closest;
                found = true;
            }

            return found;
        }

        private void DrawPathIndicator(Vector2 start, Vector2 direction, float length, float travelled)
        {
            DrawPathIndicator(start, direction, length, travelled, false);
        }

        private void DrawPathIndicator(Vector2 start, Vector2 direction, float length, float travelled, bool keepHomingReticle)
        {
            if (!keepHomingReticle)
            {
                SetHomingAimReticleVisible(false);
            }

            if (length <= 0.01f)
            {
                SetPathIndicatorVisible(false);
                return;
            }

            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            int dashCount = Mathf.Min(MaxPathDashCount, Mathf.CeilToInt(length / (PathDashLength + PathDashGap)));
            Color color = GetProjectileIndicatorColor(keepHomingReticle ? 0.86f : 0.58f);
            float width = Mathf.Max(keepHomingReticle ? 0.018f : 0.013f, projectileRadius * (keepHomingReticle ? 0.2f : 0.14f));
            int visibleCount = 0;

            for (int i = 0; i < dashCount; i++)
            {
                float segmentStart = i * (PathDashLength + PathDashGap);
                float segmentEnd = Mathf.Min(segmentStart + PathDashLength, length);
                if (segmentEnd <= travelled)
                {
                    SetPathDashVisible(i, false);
                    continue;
                }

                segmentStart = Mathf.Max(segmentStart, travelled);
                LineRenderer dash = EnsurePathDash(i);
                if (dash == null)
                {
                    continue;
                }

                dash.enabled = true;
                dash.startColor = color;
                dash.endColor = color;
                dash.startWidth = width;
                dash.endWidth = width;
                dash.SetPosition(0, start + normalized * segmentStart);
                dash.SetPosition(1, start + normalized * segmentEnd);
                visibleCount++;
            }

            for (int i = dashCount; i < pathIndicatorDashes.Count; i++)
            {
                SetPathDashVisible(i, false);
            }

            pathIndicatorActive = visibleCount > 0;
        }

        private void DrawHomingAimReticle(Vector2 center, Vector2 direction)
        {
            Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            Vector2 side = new(-forward.y, forward.x);
            float radius = Mathf.Max(0.2f, projectileRadius * 2.1f);
            float crossRadius = radius * 0.72f;
            Color color = GetProjectileIndicatorColor(0.95f);
            float width = Mathf.Max(0.03f, projectileRadius * 0.3f);

            SetHomingAimReticleCircle(0, center, radius, color, width);
            SetHomingAimReticleSegment(1, center - side * crossRadius, center + side * crossRadius, color, width);
            SetHomingAimReticleSegment(2, center - forward * crossRadius, center + forward * crossRadius, color, width);

            for (int i = HomingAimReticleLineCount; i < homingAimReticleLines.Count; i++)
            {
                if (homingAimReticleLines[i] != null)
                {
                    homingAimReticleLines[i].enabled = false;
                }
            }
        }

        private Color GetProjectileIndicatorColor(float alpha)
        {
            Color color = customIndicatorColorConfigured
                ? indicatorColor
                : launched ? launchedColor : chargingColor;
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private void PauseForExecution()
        {
            if (!pausedByExecution)
            {
                pausedByExecution = true;
                executionPauseStartedAt = Time.time;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void ResumeFromExecutionPause()
        {
            if (!pausedByExecution)
            {
                return;
            }

            float pausedSeconds = Mathf.Max(0f, Time.time - executionPauseStartedAt);
            chargeEndsAt += pausedSeconds;
            homingEndsAt += pausedSeconds;
            destroyAt += pausedSeconds;
            if (radialSplitAt > 0f)
            {
                radialSplitAt += pausedSeconds;
            }

            if (pathIndicatorEndsAt > 0f)
            {
                pathIndicatorEndsAt += pausedSeconds;
            }

            pausedByExecution = false;
            executionPauseStartedAt = 0f;
            if (launched && body != null)
            {
                body.linearVelocity = flightDirection * projectileSpeed;
            }
        }

        private void SetHomingAimReticleCircle(int index, Vector2 center, float radius, Color color, float width)
        {
            LineRenderer line = EnsureHomingAimReticleLine(index);
            if (line == null)
            {
                return;
            }

            line.enabled = true;
            line.loop = true;
            line.positionCount = HomingAimReticleCircleSegments;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;

            for (int i = 0; i < HomingAimReticleCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / HomingAimReticleCircleSegments;
                line.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private void SetHomingAimReticleSegment(int index, Vector2 start, Vector2 end, Color color, float width)
        {
            LineRenderer line = EnsureHomingAimReticleLine(index);
            if (line == null)
            {
                return;
            }

            line.enabled = true;
            line.loop = false;
            line.positionCount = 2;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private LineRenderer EnsurePathDash(int index)
        {
            EnsurePathIndicatorRoot();
            if (pathIndicatorRoot == null)
            {
                return null;
            }

            while (pathIndicatorDashes.Count <= index)
            {
                GameObject dashObject = new($"{PathIndicatorName}_{pathIndicatorDashes.Count:00}");
                dashObject.transform.SetParent(pathIndicatorRoot, false);
                LineRenderer dash = dashObject.AddComponent<LineRenderer>();
                dash.useWorldSpace = true;
                dash.loop = false;
                dash.positionCount = 2;
                dash.numCornerVertices = 0;
                dash.numCapVertices = 1;
                dash.sortingOrder = 17;
                dash.material = GetChargeVfxMaterial();
                pathIndicatorDashes.Add(dash);
            }

            return pathIndicatorDashes[index];
        }

        private LineRenderer EnsureHomingAimReticleLine(int index)
        {
            EnsurePathIndicatorRoot();
            if (pathIndicatorRoot == null)
            {
                return null;
            }

            while (homingAimReticleLines.Count <= index)
            {
                GameObject lineObject = new($"{HomingAimReticleName}_{homingAimReticleLines.Count:00}");
                lineObject.transform.SetParent(pathIndicatorRoot, false);
                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = false;
                line.positionCount = 2;
                line.numCornerVertices = 0;
                line.numCapVertices = 1;
                line.sortingOrder = 19;
                line.material = GetChargeVfxMaterial();
                homingAimReticleLines.Add(line);
            }

            return homingAimReticleLines[index];
        }

        private void EnsurePathIndicatorRoot()
        {
            if (pathIndicatorRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(PathIndicatorName);
            GameObject rootObject = existing != null ? existing.gameObject : new GameObject(PathIndicatorName);
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            pathIndicatorRoot = rootObject.transform;
        }

        private void SetPathDashVisible(int index, bool visible)
        {
            if (index < 0 || index >= pathIndicatorDashes.Count || pathIndicatorDashes[index] == null)
            {
                return;
            }

            pathIndicatorDashes[index].enabled = visible;
        }

        private void SetPathIndicatorVisible(bool visible)
        {
            pathIndicatorActive = visible && pathIndicatorActive;
            SetPathDashesVisible(visible);
            SetHomingAimReticleVisible(visible);
        }

        private void SetPathDashesVisible(bool visible)
        {
            for (int i = 0; i < pathIndicatorDashes.Count; i++)
            {
                if (pathIndicatorDashes[i] != null)
                {
                    pathIndicatorDashes[i].enabled = visible;
                }
            }
        }

        private void SetHomingAimReticleVisible(bool visible)
        {
            for (int i = 0; i < homingAimReticleLines.Count; i++)
            {
                if (homingAimReticleLines[i] != null)
                {
                    homingAimReticleLines[i].enabled = visible;
                }
            }
        }

        private void UpdateChargeVfx()
        {
            if (!canBeIntercepted)
            {
                SetChargeVfxVisible(false);
                return;
            }

            LineRenderer line = EnsureChargeVfx();
            if (line == null)
            {
                return;
            }

            float t = projectileChargeSeconds <= 0f
                ? 1f
                : 1f - Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 24f);
            float radius = Mathf.Lerp(projectileRadius * 1.9f, projectileRadius * 3.1f, pulse);
            Color chargeColor = Color.Lerp(projectileColor, Color.white, 0.55f + 0.25f * pulse);
            chargeColor.a = Mathf.Lerp(0.35f, 0.95f, t);

            line.enabled = true;
            line.startColor = chargeColor;
            line.endColor = chargeColor;
            line.startWidth = Mathf.Max(0.018f, projectileRadius * 0.32f);
            line.endWidth = line.startWidth;
            line.positionCount = 5;
            line.SetPosition(0, new Vector3(0f, radius, 0f));
            line.SetPosition(1, new Vector3(radius, 0f, 0f));
            line.SetPosition(2, new Vector3(0f, -radius, 0f));
            line.SetPosition(3, new Vector3(-radius, 0f, 0f));
            line.SetPosition(4, new Vector3(0f, radius, 0f));
        }

        private LineRenderer EnsureChargeVfx()
        {
            if (chargeVfx != null)
            {
                return chargeVfx;
            }

            Transform existing = transform.Find(ChargeVfxName);
            if (existing == null)
            {
                return null;
            }

            chargeVfx = existing.GetComponent<LineRenderer>();
            if (chargeVfx == null)
            {
                return null;
            }

            chargeVfx.useWorldSpace = false;
            chargeVfx.loop = false;
            chargeVfx.numCornerVertices = 2;
            chargeVfx.numCapVertices = 2;
            chargeVfx.sortingOrder = 24;
            chargeVfx.material = GetChargeVfxMaterial();
            return chargeVfx;
        }

        private static Material GetChargeVfxMaterial()
        {
            if (chargeVfxMaterial != null)
            {
                return chargeVfxMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            chargeVfxMaterial = shader != null ? new Material(shader) : null;
            return chargeVfxMaterial;
        }

        private void SetChargeVfxVisible(bool visible)
        {
            if (chargeVfx != null)
            {
                chargeVfx.enabled = visible;
            }
        }

    }
}
