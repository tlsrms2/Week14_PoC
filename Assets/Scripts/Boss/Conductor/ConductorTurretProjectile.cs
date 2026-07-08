using UnityEngine;
using Week14.Combat;

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
        private BossAI turretOwner;
        private Vector2 deployStartPosition;
        private Vector2 deployTargetPosition;
        private float deployStartedAt;
        private float nextFireAt = float.PositiveInfinity;
        private int firedCycles;
        private bool turretConfigured;
        private bool deployed;

        public bool IsAliveTurret => !IsDestroying && (health == null || !health.IsDead);
        public bool IsPlayerTargetable => deployed && IsAliveTurret;

        protected override bool ShowsPathIndicator => false;

        protected override void OnProjectileAwake()
        {
            health = GetComponent<Health>();
            if (health == null)
            {
                health = gameObject.AddComponent<Health>();
            }

            health.Died += OnHealthDied;
        }

        protected override void OnProjectileInitialized()
        {
            health?.Revive();
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
            deployStartedAt = Time.time;
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
            nextFireAt = float.PositiveInfinity;
            ForceZeroRotation();
            OverrideProjectileLifetime(lifetimeSeconds > 0f ? lifetimeSeconds : float.PositiveInfinity);

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

            if (Time.time < nextFireAt)
            {
                return;
            }

            FireCrossVolley();
            firedCycles++;
            nextFireAt += fireInterval;
        }

        protected override void OnDestroy()
        {
            if (health != null)
            {
                health.Died -= OnHealthDied;
            }

            base.OnDestroy();
        }

        private void FireCrossVolley()
        {
            for (int i = 0; i < CrossDirectionCount; i++)
            {
                float angle = angleOffsetDegrees + i * 90f;
                Vector2 direction = BossActionContext.AngleToDirection(angle);
                Vector3 origin = transform.position + (Vector3)(direction * muzzleOffset);
                turretOwner.FireGraphProjectile(
                    crossFireProjectile,
                    origin,
                    direction,
                    muzzleFlashScale,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false);
            }
        }

        private void TickDeploy()
        {
            ForceZeroRotation();
            float t = deploySeconds > 0f
                ? Mathf.Clamp01((Time.time - deployStartedAt) / deploySeconds)
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

            nextFireAt = turretConfigured ? Time.time + firstFireDelay : float.PositiveInfinity;
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
