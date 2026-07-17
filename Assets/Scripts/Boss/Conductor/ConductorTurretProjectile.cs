using UnityEngine;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    public sealed class ConductorTurretProjectile : EnemyProjectile
    {
        private const int CrossDirectionCount = 4;

        [Header("Cross Fire Projectile")]
        [SerializeField] private BossProjectileSettings crossFireProjectile = new();

        [Header("Deploy")]
        [SerializeField, Min(0f)] private float firstFireDelay = 0.35f;
        [SerializeField, Min(0f)] private float deploySeconds = 0.55f;

        [Header("Fire")]
        [SerializeField, Min(0.05f)] private float fireInterval = 1.25f;
        [SerializeField, Min(0)] private int fireCycles;
        [SerializeField] private float angleOffsetDegrees;
        [SerializeField, Min(0f)] private float muzzleOffset = 0.18f;
        [SerializeField, Min(0f)] private float muzzleFlashScale = 0.55f;

        private Health health;
        private EnemyStatusView statusView;
        private BossAI turretOwner;
        private Vector2 deployStartPosition;
        private Vector2 deployTargetPosition;
        private float deployElapsed;
        private float fireCooldown = float.PositiveInfinity;
        private int firedCycles;
        private bool turretConfigured;
        private bool deployed;

        public bool IsAliveTurret => !IsDestroying && (health == null || !health.IsDead);
        public bool IsPlayerTargetable => deployed && IsAliveTurret;
        public Color LockOnIndicatorColor => turretOwner != null ? turretOwner.LockOnIndicatorColor : Color.white;
        public Health Health => health;

        protected override bool ShowsPathIndicator => false;

        protected override void OnProjectileAwake()
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
            }

            health.Died += OnHealthDied;
            EnsureStatusView();
        }

        protected override void OnProjectileInitialized()
        {
            health?.Revive();
            EnsureStatusView();
            ConfigureInterceptable(false);
            ConfigurePathIndicatorSuppressed(true);
            ConfigureExternalMotionDriven(true);
            ForceZeroRotation();

            Rigidbody2D body = ProjectileBody;
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        public void ConfigureTurret(
            BossAI owner,
            Vector2 targetPosition,
            float nextDeploySeconds,
            float nextFirstFireDelay,
            float nextFireInterval,
            int nextFireCycles,
            float nextAngleOffsetDegrees,
            float nextMuzzleOffset,
            float nextMuzzleFlashScale,
            float lifetimeSeconds)
        {
            turretOwner = owner != null ? owner : OwnerBoss;
            deployStartPosition = transform.position;
            deployTargetPosition = targetPosition;
            deployElapsed = 0f;
            deploySeconds = Mathf.Max(0f, nextDeploySeconds);
            firstFireDelay = Mathf.Max(0f, nextFirstFireDelay);
            fireInterval = Mathf.Max(0.05f, nextFireInterval);
            fireCycles = Mathf.Max(0, nextFireCycles);
            angleOffsetDegrees = nextAngleOffsetDegrees;
            muzzleOffset = Mathf.Max(0f, nextMuzzleOffset);
            muzzleFlashScale = Mathf.Max(0f, nextMuzzleFlashScale);
            firedCycles = 0;
            turretConfigured = turretOwner != null && crossFireProjectile != null && crossFireProjectile.Prefab != null;
            deployed = false;
            fireCooldown = float.PositiveInfinity;
            ForceZeroRotation();
            OverrideProjectileLifetime(lifetimeSeconds > 0f ? lifetimeSeconds : float.PositiveInfinity);
            EnsureStatusView();

            if (deploySeconds <= 0f || Vector2.Distance(deployStartPosition, deployTargetPosition) <= 0.01f)
            {
                CompleteDeploy();
            }
        }

        public override bool TryDestroyByInterceptShot(out bool parried)
        {
            parried = false;
            return false;
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return false;
        }

        public bool ReceivePlayerHit(int bulletDamage, Vector3 hitPosition, Vector2 hitDirection)
        {
            if (health == null || health.IsDead || !IsAliveTurret || bulletDamage <= 0)
            {
                return false;
            }

            bool damaged = health.TakeDamage(bulletDamage);
            if (!damaged)
            {
                return false;
            }

            PlayEnemyHitVfx(hitPosition, hitDirection);
            BossAI.PlayEnemyHitCameraImpactForSequence(hitDirection, 0.08f, 0.12f, 0.05f);
            return true;
        }

        protected override void OnProjectileTick()
        {
            ForceZeroRotation();
            Rigidbody2D body = ProjectileBody;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (!deployed)
            {
                TickDeploy();
                return;
            }

            if (!turretConfigured)
            {
                return;
            }

            if (fireCycles > 0 && firedCycles >= fireCycles)
            {
                return;
            }

            fireCooldown -= EnemyTimeScale.DeltaTime;
            if (fireCooldown > 0f)
            {
                return;
            }

            FireCrossVolley();
            firedCycles++;
            fireCooldown = Mathf.Max(0f, fireCooldown + fireInterval);
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (TryResolvePlayerProjectile(other))
            {
                return;
            }

            base.OnTriggerEnter2D(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryResolvePlayerProjectile(collision.collider);
        }

        protected override void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnHealthDied;
            }

            base.OnDestroy();
        }

        private bool TryResolvePlayerProjectile(Collider2D other)
        {
            PlayerProjectile playerProjectile = other != null ? other.GetComponentInParent<PlayerProjectile>() : null;
            return playerProjectile != null && playerProjectile.TryResolveTurretHit(this);
        }

        private void EnsureStatusView()
        {
            if (statusView == null)
            {
                statusView = GetComponent<EnemyStatusView>() ?? gameObject.AddComponent<EnemyStatusView>();
            }

            statusView.SetWorldTarget(transform);
            statusView.SetSuppressed(false);
            statusView.Configure(this);
            statusView.SetTarget(health);
        }

        private void PlayEnemyHitVfx(Vector3 hitPosition, Vector2 hitDirection)
        {
            ProjectileVfx.PlayPrefab(
                turretOwner?.EnemyHitVfxPrefab,
                hitPosition,
                hitDirection,
                transform,
                followRotation: false);
        }

        private void FireCrossVolley()
        {
            for (int i = 0; i < CrossDirectionCount; i++)
            {
                float angle = angleOffsetDegrees + i * 90f;
                Vector2 direction = BossActionContext.AngleToDirection(angle);
                Vector3 origin = transform.position + (Vector3)(direction * muzzleOffset);
                EnemyProjectile projectile = turretOwner.FireGraphProjectile(
                    crossFireProjectile,
                    origin,
                    direction,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false);
                if (projectile != null)
                {
                    ProjectileVfx.PlayPrefab(
                        turretOwner.EnemyMuzzleFlashVfxPrefab,
                        projectile.transform.position,
                        direction,
                        transform,
                        muzzleFlashScale);
                }
            }
        }

        private void TickDeploy()
        {
            ForceZeroRotation();
            deployElapsed += EnemyTimeScale.DeltaTime;
            float t = deploySeconds > 0f
                ? Mathf.Clamp01(deployElapsed / deploySeconds)
                : 1f;
            Vector2 nextPosition = Vector2.Lerp(deployStartPosition, deployTargetPosition, t);
            transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);

            Rigidbody2D body = ProjectileBody;
            if (body != null)
            {
                body.position = nextPosition;
            }

            if (t >= 1f)
            {
                CompleteDeploy();
            }
        }

        private void CompleteDeploy()
        {
            deployed = true;
            ForceZeroRotation();
            transform.position = new Vector3(deployTargetPosition.x, deployTargetPosition.y, transform.position.z);
            Rigidbody2D body = ProjectileBody;
            if (body != null)
            {
                body.position = deployTargetPosition;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            fireCooldown = turretConfigured ? firstFireDelay : float.PositiveInfinity;
        }

        private void ForceZeroRotation()
        {
            transform.rotation = Quaternion.identity;
            Rigidbody2D body = ProjectileBody;
            if (body != null)
            {
                body.rotation = 0f;
            }
        }

        private void OnHealthDied(Health _)
        {
            if (!IsDestroying)
            {
                DestroyProjectile(EnemyProjectileDestroyReason.Intercepted);
            }
        }
    }
}
