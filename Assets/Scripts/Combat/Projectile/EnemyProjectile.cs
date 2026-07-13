using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public enum EnemyProjectileDestroyReason
    {
        Unknown,
        Expired,
        Intercepted,
        PlayerHit,
        OwnerDestroyed
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public partial class EnemyProjectile : MonoBehaviour
    {
        private const string BulletVisualName = "BulletVisual";
        private const string ChargeVfxName = "ChargeVfx";
        private const string PathIndicatorName = "PathIndicator";
        private const string HomingAimReticleName = "HomingAimReticle";
        private const string ParryLockOnIndicatorName = "ParryLockOnIndicator";
        private const string LegacyParryLockOnIndicatorName = "ProjectileLockOnIndicator";
        private const string WallLayerName = "Wall";
        private const string GroundLayerName = "Ground";
        private const int MaxPathDashCount = 160;
        private const int HomingAimReticleLineCount = 3;
        private const int HomingAimReticleCircleSegments = 40;
        private const int RadialSplitIndicatorLineCount = 2;
        private const float PathDashLength = 0.2f;
        private const float PathDashGap = 0.14f;
        private float projectileSpeed;
        private float projectileLifetime;
        private float projectileRadius;
        private float projectileChargeSeconds;
        private float projectileTrailSeconds;
        private float projectileTrailWidthMultiplier;
        private Color projectileColor;
        private Color chargingColor;
        private Color launchedColor;
        [SerializeField, Tooltip("경로/호밍 조준 인디케이터 색입니다. 투명색이면 투사체 색을 사용합니다.")]
        private Color indicatorColor = Color.clear;
        private float chargeDriftSpeed;
        private float chargeEndsAt;
        private Rigidbody2D body;
        private LineRenderer chargeVfx;
        private TrailRenderer projectileTrail;
        private Color prefabTrailStartColor;
        private Color prefabTrailEndColor;
        private Gradient prefabTrailColorGradient;
        private EnemyProjectile launchReplacementPrefab;
        private bool customTrailColorConfigured;
        private bool customIndicatorColorConfigured;
        private bool hasPrefabTrailColors;
        [SerializeField] private GameObject parryLockOnIndicatorRoot;
        [SerializeField] private MouseParryReticle parryLockOnReticle;
        [SerializeField] private Transform parryLockOnRotatingRoot;
        [SerializeField] private float parryLockOnRotationSpeedDegrees = 180f;
        private readonly List<LineRenderer> pathIndicatorDashes = new();
        private readonly List<LineRenderer> homingAimReticleLines = new();
        private readonly List<LineRenderer> radialSplitIndicatorLines = new();
        private Transform pathIndicatorRoot;
        private BulletGauge ownerBullets;
        private BossAI ownerBoss;
        private Minion ownerMinion;
        private Transform ownerTransform;
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
        private bool delayPathIndicatorUntilLaunch;
        private bool preserveLaunchDirectionOnLaunch;
        private bool ignorePlayerCollision;
        private bool externalMotionDriven;
        private bool runtimeHomingActive;
        private float runtimeHomingEndsAt;
        private float runtimeHomingTurnDegreesPerSecond;
        private bool parryLockOnIndicatorVisible;
        private int interceptGroupId;
        private EnemyProjectile poolPrefabSource;
        private bool pooledByProjectilePool;
        private Vector2 pathIndicatorStart;
        private Vector2 pathIndicatorDirection = Vector2.left;
        private Vector2 pathIndicatorRadialSplitPoint;
        private float pathIndicatorLength;
        private float pathIndicatorEndsAt;
        private bool pathIndicatorHasRadialSplitPoint;
        private Vector2 lastWallCheckPosition;
        private static readonly List<EnemyProjectile> activeProjectiles = new();
        private float executionPauseStartedAt;
        private bool pausedByExecution;
        private static Material chargeVfxMaterial;
        private static int nextInterceptGroupId = 1;
        private static readonly Dictionary<int, EnemyProjectile> activeProjectileByInterceptGroup = new();

        public event System.Action<EnemyProjectile> Launched;
        public event System.Action<EnemyProjectile> RadialSplit;
        public event System.Action<EnemyProjectile> RadialSplitImminent;
        public event System.Action<EnemyProjectile, EnemyProjectileDestroyReason, Vector3> Destroyed;

        public Vector2 IncomingDirection => flightDirection;
        public bool IsCharging => !resolved && !isDestroying && !launched;
        public bool CanBeIntercepted => !resolved && !isDestroying && canBeIntercepted && !interceptPending;
        public int InterceptGroupId => interceptGroupId;
        public float LockOnRadius => Mathf.Max(0.24f, projectileRadius * 2.6f);
        public BossAI OwnerBoss => ownerBoss;
        protected Rigidbody2D ProjectileBody => body;
        protected int BulletDamage => bulletDamage;
        protected float ProjectileSpeed => projectileSpeed;
        protected float ProjectileLifetime => projectileLifetime;
        protected float ProjectileRadius => projectileRadius;
        protected Color ProjectileColor => projectileColor;
        protected bool IsLaunched => launched;
        protected bool IsResolved => resolved;
        protected bool IsDestroying => isDestroying;
        protected float ProjectileChargeSeconds => projectileChargeSeconds;
        protected float ChargeEndsAt => chargeEndsAt;
        protected Color ChargingColor => chargingColor;
        protected Color LaunchedColor => launchedColor;
        protected bool WillSplitRadiallyOnLaunch => splitRadiallyOnLaunch && radialSplitBulletCount > 0;
        protected virtual bool UsesProjectileVisibility => true;
        protected virtual bool ShowsPathIndicator => true;
        protected Vector2 FlightDirection
        {
            get => flightDirection;
            set => ApplyFlightDirection(value);
        }
        protected virtual bool IsHomingProjectile => false;
        public float ChargeProgress01 => projectileChargeSeconds > 0f
            ? 1f - Mathf.Clamp01((chargeEndsAt - Time.time) / projectileChargeSeconds)
            : 1f;
        public static IReadOnlyList<EnemyProjectile> ActiveProjectiles => activeProjectiles;

        protected void ApplyFlightDirection(Vector2 direction, bool rotateToDirection = true)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            flightDirection = direction.normalized;
            if (!rotateToDirection)
            {
                return;
            }

            float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public static bool TryGetActiveInterceptTarget(int groupId, out EnemyProjectile projectile)
        {
            if (groupId > 0
                && activeProjectileByInterceptGroup.TryGetValue(groupId, out projectile)
                && projectile != null
                && !projectile.resolved
                && !projectile.isDestroying)
            {
                return true;
            }

            projectile = null;
            return false;
        }

        public EnemyProjectile ResolveInterceptTarget()
        {
            return TryGetActiveInterceptTarget(interceptGroupId, out EnemyProjectile projectile)
                ? projectile
                : this;
        }

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
            ConfigureSpecialStateColors(nextChargingColor, nextLaunchedColor, nextHomingBlinkColor);
            ApplyProjectileColor(launched ? launchedColor : chargingColor);
        }

        protected virtual void ConfigureSpecialStateColors(Color nextChargingColor, Color nextLaunchedColor, Color? nextHomingBlinkColor) { }

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

        public void RestorePrefabTrailColor()
        {
            if (!hasPrefabTrailColors)
            {
                return;
            }

            if (projectileTrail == null)
            {
                projectileTrail = GetComponent<TrailRenderer>();
            }

            if (projectileTrail == null)
            {
                return;
            }

            projectileTrail.startColor = prefabTrailStartColor;
            projectileTrail.endColor = prefabTrailEndColor;
            if (prefabTrailColorGradient != null)
            {
                projectileTrail.colorGradient = CloneGradient(prefabTrailColorGradient);
            }

            customTrailColorConfigured = true;
        }

        private static Gradient CloneGradient(Gradient source)
        {
            if (source == null)
            {
                return null;
            }

            Gradient clone = new();
            clone.mode = source.mode;
            clone.SetKeys(source.colorKeys, source.alphaKeys);
            return clone;
        }

        public void ConfigureIndicatorColor(Color nextIndicatorColor)
        {
            indicatorColor = nextIndicatorColor;
            customIndicatorColorConfigured = true;
        }

        public void ConfigurePathIndicatorSuppressed(bool suppressed)
        {
            suppressPathIndicator = suppressed;
            RefreshPathIndicator();
        }

        public void ConfigurePathIndicatorDelayedUntilLaunch(bool delayed)
        {
            delayPathIndicatorUntilLaunch = delayed;
            RefreshPathIndicator();
        }

        private void AssignInterceptGroup(int existingGroupId)
        {
            interceptGroupId = existingGroupId > 0 ? existingGroupId : nextInterceptGroupId++;
            activeProjectileByInterceptGroup[interceptGroupId] = this;
        }

        private void UnregisterInterceptGroup()
        {
            if (interceptGroupId <= 0)
            {
                return;
            }

            if (activeProjectileByInterceptGroup.TryGetValue(interceptGroupId, out EnemyProjectile projectile)
                && projectile == this)
            {
                activeProjectileByInterceptGroup.Remove(interceptGroupId);
            }
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

        public void ConfigureSpeedMultiplier(float speedMultiplier)
        {
            projectileSpeed *= Mathf.Max(0.01f, speedMultiplier);
            RefreshRuntimeVelocity();
            RefreshPathIndicator();
        }

        public void ConfigureHomingOverride(float seconds, float turnDegreesPerSecond)
        {
            float launchTime = launched ? Time.time : chargeEndsAt;
            if (this is IHomingEnemyProjectile)
            {
                ConfigureHoming(true, seconds, turnDegreesPerSecond, launchTime);
                RefreshPathIndicator();
                return;
            }

            runtimeHomingActive = true;
            runtimeHomingEndsAt = launchTime + Mathf.Max(0.01f, seconds);
            runtimeHomingTurnDegreesPerSecond = Mathf.Max(0.01f, turnDegreesPerSecond);
        }

        private void TickRuntimeHoming()
        {
            if (!runtimeHomingActive || IsResolved || IsDestroying)
            {
                return;
            }

            if (Time.time >= runtimeHomingEndsAt)
            {
                runtimeHomingActive = false;
                return;
            }

            if (runtimeHomingTurnDegreesPerSecond <= 0f || ProjectileSpeed <= 0f)
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

            float maxRadians = runtimeHomingTurnDegreesPerSecond * Mathf.Deg2Rad * EnemyTimeScale.DeltaTime;
            Vector3 nextDirection = Vector3.RotateTowards(FlightDirection, toTarget.normalized, maxRadians, 0f);
            ApplyFlightDirection(nextDirection);
        }

        private void ExtendRuntimeHomingTimer(float seconds)
        {
            if (runtimeHomingActive)
            {
                runtimeHomingEndsAt += Mathf.Max(0f, seconds);
            }
        }

        public void ConfigurePreserveLaunchDirectionOnLaunch(bool preserve)
        {
            preserveLaunchDirectionOnLaunch = preserve;
        }

        public void ConfigurePlayerCollisionIgnored(bool ignored)
        {
            ignorePlayerCollision = ignored;
        }

        public void ConfigureExternalMotionDriven(bool driven)
        {
            externalMotionDriven = driven;
            if (externalMotionDriven && body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        public void ConfigurePersistentLifetime()
        {
            if (!resolved && !isDestroying)
            {
                destroyAt = float.PositiveInfinity;
            }
        }

        public void HoldChargeUntilForcedLaunch()
        {
            if (resolved || isDestroying)
            {
                return;
            }

            bool wasLaunched = launched;
            launched = false;
            chargeAnchor = null;
            chargeEndsAt = float.PositiveInfinity;
            destroyAt = float.PositiveInfinity;
            radialSplitAt = 0f;
            ApplyProjectileColor(chargingColor);
            SetPathIndicatorVisible(false);
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            if (wasLaunched)
            {
                OnProjectileInitialized();
            }
        }

        public void ForceLaunchStateForExternalMotion()
        {
            if (resolved || isDestroying)
            {
                return;
            }

            if (launched)
            {
                RefreshRuntimeVelocity();
                return;
            }

            if (TryReplaceWithLaunchPrefab())
            {
                return;
            }

            launched = true;
            chargeAnchor = null;
            chargeEndsAt = Time.time;
            destroyAt = Time.time + projectileLifetime;
            GetHomingSpawnConfig(
                out bool homingEnabled,
                out float homingSeconds,
                out float homingTurnDegrees);
            ConfigureHoming(homingEnabled, homingSeconds, homingTurnDegrees, Time.time);
            ApplyProjectileColor(launchedColor);
            if (growScaleWhileCharging)
            {
                transform.localScale = chargeGrowthEndScale;
                baseLocalScale = transform.localScale;
            }

            if (ShouldTurnTowardPlayerOnLaunch())
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
                body.linearVelocity = flightDirection * projectileSpeed * EnemyTimeScale.Current;
            }

            Launched?.Invoke(this);
            OnProjectileLaunched();
        }

        public void ConfigureChargeAnchor(Transform anchor)
        {
            chargeAnchor = anchor;
            if (IsCharging)
            {
                SnapToChargeAnchor();
            }
        }

        private bool ShouldTurnTowardPlayerOnLaunch()
        {
            return !preserveLaunchDirectionOnLaunch && ShouldAimAtPlayerOnLaunch();
        }

        private void RefreshRuntimeVelocity()
        {
            if (!launched || body == null || isDestroying)
            {
                return;
            }

            body.linearVelocity = flightDirection * projectileSpeed * EnemyTimeScale.Current;
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
            RefreshPathIndicator();

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

        protected void OverrideProjectileLifetime(float lifetime)
        {
            projectileLifetime = Mathf.Max(0f, lifetime);
            float lifetimeStart = launched ? Time.time : chargeEndsAt;
            destroyAt = lifetimeStart + projectileLifetime;
            RefreshPathIndicator();
        }

        public void EnsureProjectileLifetime(float minimumLifetime)
        {
            float lifetimeStart = launched ? Time.time : chargeEndsAt;
            float nextDestroyAt = lifetimeStart + Mathf.Max(0f, minimumLifetime);
            if (nextDestroyAt <= destroyAt)
            {
                return;
            }

            projectileLifetime = nextDestroyAt - lifetimeStart;
            destroyAt = nextDestroyAt;
            RefreshPathIndicator();
        }

        protected virtual float ResolveProjectileLifetime(float configuredLifetime)
        {
            return configuredLifetime;
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
            bool suppressPathIndicator = false,
            int existingInterceptGroupId = 0)
        {
            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            EnemyProjectile projectile = ProjectilePool.Get(prefab, position, Quaternion.Euler(0f, 0f, angle));
            projectile.poolPrefabSource = prefab;
            projectile.MarkPooledByProjectilePool();
            if (homingEnabled && projectile is not IHomingEnemyProjectile)
            {
                Debug.LogWarning($"{projectile.name} is configured as homing but does not implement {nameof(IHomingEnemyProjectile)}.", projectile);
            }

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
                suppressPathIndicator,
                existingInterceptGroupId);
            if (projectile.UsesProjectileVisibility)
            {
                ProjectileVfx.ApplyVisibility(
                    projectile.gameObject, projectile.projectileColor, radius, trailSeconds, trailWidth);
                projectile.BeginTrail();
            }

            return projectile;
        }

        protected virtual void ConfigureHoming(bool enabled, float seconds, float turnDegrees, float launchTime) { }

        protected virtual void GetHomingSpawnConfig(out bool enabled, out float seconds, out float turnDegrees)
        {
            enabled = false;
            seconds = 0f;
            turnDegrees = 0f;
        }

        protected virtual void CopySpecialRuntimeStateTo(EnemyProjectile replacement) { }

        internal void MarkPooledByProjectilePool()
        {
            pooledByProjectilePool = true;
        }

    }

    internal sealed class ProjectilePool : MonoBehaviour
    {
        private const string PoolRootName = "Pool";

        private static readonly Dictionary<int, PoolEntry> entriesByPrefabId = new();
        private static readonly Dictionary<string, PoolEntry> generatedEntriesByKey = new(StringComparer.Ordinal);
        private static readonly Dictionary<int, PoolEntry> activeEntriesByInstanceId = new();

        private static ProjectilePool instance;
        private static Transform root;

        public static void EnsureScenePool()
        {
            EnsureInstance();
        }

        public static T Get<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            if (prefab == null)
            {
                return null;
            }

            PoolEntry entry = GetOrCreateEntry(prefab);
            T instanceComponent = TakeInactive<T>(entry);
            if (instanceComponent == null)
            {
                instanceComponent = Instantiate(prefab, entry.Root);
                instanceComponent.name = prefab.name;
                entry.CreatedCount++;
            }

            Transform instanceTransform = instanceComponent.transform;
            instanceTransform.SetParent(entry.Root, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = prefab.transform.localScale;

            activeEntriesByInstanceId[instanceComponent.GetInstanceID()] = entry;
            entry.ActiveCount++;
            entry.PeakActiveCount = Mathf.Max(entry.PeakActiveCount, entry.ActiveCount);
            instanceComponent.gameObject.SetActive(true);
            return instanceComponent;
        }

        public static T GetGenerated<T>(
            string key,
            string poolName,
            Func<T> create,
            Vector3 position,
            Quaternion rotation) where T : Component
        {
            if (string.IsNullOrWhiteSpace(key) || create == null)
            {
                return null;
            }

            PoolEntry entry = GetOrCreateGeneratedEntry(key, poolName);
            T instanceComponent = TakeInactive<T>(entry);
            if (instanceComponent == null)
            {
                instanceComponent = create();
                if (instanceComponent == null)
                {
                    return null;
                }

                instanceComponent.name = string.IsNullOrWhiteSpace(poolName) ? typeof(T).Name : poolName.Replace(" Pool", string.Empty);
                entry.CreatedCount++;
            }

            Transform instanceTransform = instanceComponent.transform;
            instanceTransform.SetParent(entry.Root, false);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instanceTransform.localScale = Vector3.one;

            activeEntriesByInstanceId[instanceComponent.GetInstanceID()] = entry;
            entry.ActiveCount++;
            entry.PeakActiveCount = Mathf.Max(entry.PeakActiveCount, entry.ActiveCount);
            instanceComponent.gameObject.SetActive(true);

            if (instanceComponent is EnemyProjectile enemyProjectile)
            {
                enemyProjectile.MarkPooledByProjectilePool();
            }

            return instanceComponent;
        }

        public static void Release(Component instanceComponent, float delaySeconds = 0f)
        {
            if (instanceComponent == null)
            {
                return;
            }

            int instanceId = instanceComponent.GetInstanceID();
            if (!activeEntriesByInstanceId.TryGetValue(instanceId, out PoolEntry entry))
            {
                return;
            }

            activeEntriesByInstanceId.Remove(instanceId);
            entry.ActiveCount = Mathf.Max(0, entry.ActiveCount - 1);

            if (delaySeconds > 0f && EnsureInstance() != null && instanceComponent.gameObject.activeInHierarchy)
            {
                instance.StartCoroutine(instance.ReleaseAfterDelay(entry, instanceComponent, delaySeconds));
                return;
            }

            ReturnNow(entry, instanceComponent);
        }

        private static ProjectilePool EnsureInstance()
        {
            if (instance != null && root != null)
            {
                return instance;
            }

            entriesByPrefabId.Clear();
            generatedEntriesByKey.Clear();
            activeEntriesByInstanceId.Clear();

            GameObject rootObject = GameObject.Find(PoolRootName);
            if (rootObject == null)
            {
                rootObject = new GameObject(PoolRootName);
            }

            root = rootObject.transform;
            instance = rootObject.GetComponent<ProjectilePool>();
            if (instance == null)
            {
                instance = rootObject.AddComponent<ProjectilePool>();
            }

            return instance;
        }

        private static PoolEntry GetOrCreateEntry(Component prefab)
        {
            EnsureInstance();

            int prefabId = prefab.GetInstanceID();
            if (entriesByPrefabId.TryGetValue(prefabId, out PoolEntry entry) && entry.Root != null)
            {
                return entry;
            }

            GameObject entryObject = new($"{prefab.name} Pool");
            entryObject.transform.SetParent(root, false);
            entry = new PoolEntry(entryObject.transform);
            entriesByPrefabId[prefabId] = entry;
            return entry;
        }

        private static PoolEntry GetOrCreateGeneratedEntry(string key, string poolName)
        {
            EnsureInstance();

            if (generatedEntriesByKey.TryGetValue(key, out PoolEntry entry) && entry.Root != null)
            {
                return entry;
            }

            string resolvedPoolName = string.IsNullOrWhiteSpace(poolName) ? $"{key} Pool" : poolName;
            GameObject entryObject = new(resolvedPoolName);
            entryObject.transform.SetParent(root, false);
            entry = new PoolEntry(entryObject.transform);
            generatedEntriesByKey[key] = entry;
            return entry;
        }

        private static T TakeInactive<T>(PoolEntry entry) where T : Component
        {
            while (entry.Inactive.Count > 0)
            {
                Component candidate = entry.Inactive.Pop();
                if (candidate != null)
                {
                    return candidate as T;
                }
            }

            return null;
        }

        private static void ReturnNow(PoolEntry entry, Component instanceComponent)
        {
            if (entry == null || instanceComponent == null)
            {
                return;
            }

            instanceComponent.gameObject.SetActive(false);
            if (entry.Root != null)
            {
                instanceComponent.transform.SetParent(entry.Root, false);
            }

            entry.Inactive.Push(instanceComponent);
        }

        private IEnumerator ReleaseAfterDelay(PoolEntry entry, Component instanceComponent, float delaySeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));

            if (instanceComponent == null)
            {
                yield break;
            }

            ReturnNow(entry, instanceComponent);
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            instance = null;
            root = null;
            entriesByPrefabId.Clear();
            generatedEntriesByKey.Clear();
            activeEntriesByInstanceId.Clear();
        }

        private sealed class PoolEntry
        {
            public PoolEntry(Transform root)
            {
                Root = root;
            }

            public Transform Root { get; }
            public Stack<Component> Inactive { get; } = new();
            public int CreatedCount { get; set; }
            public int ActiveCount { get; set; }
            public int PeakActiveCount { get; set; }
        }
    }
}
