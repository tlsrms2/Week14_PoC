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
        private EnemyProjectile launchReplacementPrefab;
        private bool customTrailColorConfigured;
        private bool customIndicatorColorConfigured;
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
        private int interceptGroupId;
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

        public void ConfigureIndicatorColor(Color nextIndicatorColor)
        {
            indicatorColor = nextIndicatorColor;
            customIndicatorColorConfigured = true;
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
            bool suppressPathIndicator = false,
            int existingInterceptGroupId = 0)
        {
            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            EnemyProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            if (homingEnabled && projectile is not HomingEnemyProjectile)
            {
                Debug.LogWarning($"{projectile.name} is configured as homing but does not inherit {nameof(HomingEnemyProjectile)}.", projectile);
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

    }
}
