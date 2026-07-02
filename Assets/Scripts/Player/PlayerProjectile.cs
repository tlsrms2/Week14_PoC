using System;
using UnityEngine;
using Week14.Enemy;
using Week14.UI;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerProjectile : MonoBehaviour
    {
        private const string GroundLayerName = "Ground";

        internal static event Action<int> NormalAttackDamageDealt;

        private PlayerCombatController owner;
        private Rigidbody2D body;
        private float projectileSpeed;
        private int bulletDamage;
        private float collisionRadius;
        private float destroyAt;
        private Vector2 previousPosition;
        private EnemyProjectile forcedParryTarget;
        private int forcedParryTargetId;
        private float forcedParryResolveAt;
        private Vector2 flightDirection = Vector2.right;
        private Color projectileColor = Color.white;
        private bool canDamageHealth;
        private bool canClashWithEnemyProjectile;
        private bool isSkillShot;
        private bool resolved;
        private bool isDestroying;

        public static PlayerProjectile Spawn(
            PlayerProjectile prefab,
            Vector3 position,
            Vector2 direction,
            PlayerCombatController owner,
            float speed,
            float lifetime,
            float radius,
            int bulletDamage,
            Color color,
            bool canDamageHealth,
            bool canClashWithEnemyProjectile = false,
            bool isSkillShot = false)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            PlayerProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            if (!projectile.Initialize(
                    owner,
                    fireDirection,
                    speed,
                    lifetime,
                    radius,
                    bulletDamage,
                    color,
                    canDamageHealth,
                    canClashWithEnemyProjectile,
                    isSkillShot))
            {
                Destroy(projectile.gameObject);
                return null;
            }

            return projectile;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private bool Initialize(
            PlayerCombatController nextOwner,
            Vector2 direction,
            float speed,
            float lifetime,
            float radius,
            int nextBulletDamage,
            Color color,
            bool nextCanDamageHealth,
            bool nextCanClashWithEnemyProjectile,
            bool nextIsSkillShot)
        {
            owner = nextOwner;
            projectileSpeed = speed;
            bulletDamage = nextBulletDamage;
            collisionRadius = ResolveCollisionRadius(radius);
            canDamageHealth = nextCanDamageHealth;
            canClashWithEnemyProjectile = nextCanClashWithEnemyProjectile;
            isSkillShot = nextIsSkillShot;
            destroyAt = Time.time + lifetime;
            previousPosition = transform.position;
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            projectileColor = color;

            if (body == null)
            {
                Debug.LogWarning($"{nameof(PlayerProjectile)} prefab requires {nameof(Rigidbody2D)}.", this);
                return false;
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = flightDirection * speed;
            return true;
        }

        private void Update()
        {
            if (isDestroying)
            {
                return;
            }

            SweepForMissedCollisions();
            if (isDestroying)
            {
                return;
            }

            TryResolveForcedParryTarget();
            if (isDestroying)
            {
                return;
            }

            if (Time.time >= destroyAt)
            {
                DestroyProjectile();
                return;
            }

            previousPosition = transform.position;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryResolveCollision(other);
        }

        private void SweepForMissedCollisions()
        {
            Vector2 currentPosition = transform.position;
            Vector2 delta = currentPosition - previousPosition;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            RaycastHit2D[] hits = Physics2D.CircleCastAll(
                previousPosition,
                Mathf.Max(0.001f, collisionRadius),
                delta / distance,
                distance);

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (TryResolveCollision(hitCollider))
                {
                    return;
                }
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(currentPosition, Mathf.Max(0.001f, collisionRadius));
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (TryResolveCollision(overlaps[i]))
                {
                    return;
                }
            }
        }

        public void SetForcedParryTarget(EnemyProjectile enemyProjectile)
        {
            forcedParryTarget = enemyProjectile != null ? enemyProjectile.ResolveInterceptTarget() : null;
            forcedParryTargetId = forcedParryTarget != null ? forcedParryTarget.InterceptGroupId : 0;
            if (forcedParryTarget == null || projectileSpeed <= 0f)
            {
                forcedParryResolveAt = 0f;
                return;
            }

            float distance = Vector2.Distance(transform.position, forcedParryTarget.transform.position);
            forcedParryResolveAt = Time.time + Mathf.Max(0.02f, distance / projectileSpeed);
            destroyAt = Mathf.Max(destroyAt, forcedParryResolveAt + 0.1f);
        }

        private bool TryResolveForcedParryTarget()
        {
            if (!canClashWithEnemyProjectile || resolved || isDestroying)
            {
                return false;
            }

            EnemyProjectile target = ResolveForcedParryTarget();
            if (target == null)
            {
                DestroyProjectile();
                return true;
            }

            Vector2 targetPosition = target.transform.position;
            float hitRadius = Mathf.Max(0.12f, collisionRadius * 2.5f);
            bool closeEnough = Vector2.Distance(transform.position, targetPosition) <= hitRadius;
            bool reachedExpectedTime = forcedParryResolveAt > 0f && Time.time >= forcedParryResolveAt;
            bool crossedTarget = Vector2.Dot(flightDirection, targetPosition - (Vector2)transform.position) <= 0f;
            if (!closeEnough && !reachedExpectedTime && !crossedTarget)
            {
                return false;
            }

            transform.position = target.transform.position;
            return TryDestroyByEnemyProjectileClash(target);
        }

        private EnemyProjectile ResolveForcedParryTarget()
        {
            if (forcedParryTargetId > 0
                && EnemyProjectile.TryGetActiveInterceptTarget(forcedParryTargetId, out EnemyProjectile target))
            {
                forcedParryTarget = target;
                return target;
            }

            if (forcedParryTarget == null)
            {
                return null;
            }

            forcedParryTarget = forcedParryTarget.ResolveInterceptTarget();
            forcedParryTargetId = forcedParryTarget != null ? forcedParryTarget.InterceptGroupId : forcedParryTargetId;
            return forcedParryTarget;
        }

        private bool TryResolveCollision(Collider2D other)
        {
            if (other == null || isDestroying || other.transform.IsChildOf(transform))
            {
                return false;
            }

            if (IsGroundCollider(other))
            {
                return false;
            }

            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                return false;
            }

            EnemyProjectile enemyProjectile = other.GetComponentInParent<EnemyProjectile>();
            if (enemyProjectile != null)
            {
                if (canClashWithEnemyProjectile && TryDestroyByEnemyProjectileClash(enemyProjectile))
                {
                    return true;
                }

                return false;
            }

            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth == null)
            {
                if (forcedParryTarget != null)
                {
                    return false;
                }

                if (other.GetComponentInParent<PlayerProjectile>() != null
                    || other.GetComponentInParent<EnemyProjectile>() != null)
                {
                    return false;
                }

                DestroyProjectile();
                return true;
            }

            if (resolved || !canDamageHealth || (owner != null && targetHealth == owner.Health))
            {
                return false;
            }

            resolved = true;
            BossAI boss = targetHealth.GetComponent<BossAI>()
                ?? targetHealth.GetComponentInParent<BossAI>();
            if (boss != null)
            {
                int appliedDamage = boss is DronePilot dronePilot
                    ? dronePilot.GetBodySharedDamage(bulletDamage)
                    : bulletDamage;

                if (boss.ReceivePlayerHit(bulletDamage, true, transform.position, flightDirection, projectileColor))
                {
                    ShowFloatingDamage(targetHealth, appliedDamage);
                    NotifyNormalAttackDamage();
                }

                DestroyProjectile();
                return true;
            }

            Minion minion = targetHealth.GetComponent<Minion>()
                ?? targetHealth.GetComponentInParent<Minion>();
            if (minion != null)
            {
                int appliedDamage = bulletDamage;
                if (minion.Owner is DronePilot dronePilot
                    && dronePilot.TryGetMinionSharedDamage(minion, bulletDamage, out int sharedDamage))
                {
                    appliedDamage = sharedDamage;
                }

                if (minion.ReceivePlayerHit(bulletDamage, true, transform.position, flightDirection, projectileColor))
                {
                    ShowFloatingDamage(targetHealth, appliedDamage);
                    NotifyNormalAttackDamage();
                }

                DestroyProjectile();
                return true;
            }

            BulletGauge targetBullets = targetHealth.GetComponent<BulletGauge>();
            if (targetBullets != null)
            {
                if (targetBullets.IsEmpty)
                {
                    targetHealth.Kill();
                }
                else
                {
                    targetBullets.TrySpend(bulletDamage, BulletChangeSource.Hit);
                }
            }
            else
            {
                targetHealth.TakeDamage(bulletDamage);
            }

            ShowFloatingDamage(targetHealth, bulletDamage);
            NotifyNormalAttackDamage();
            Color impactColor = projectileColor;

            ProjectileVfx.PlayPlayerAttackImpact(
                transform.position,
                flightDirection,
                impactColor,
                10,
                0,
                0,
                0.45f);

            DestroyProjectile();
            return true;
        }

        private static bool IsGroundCollider(Collider2D collider)
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            return groundLayer >= 0
                && collider != null
                && collider.gameObject.layer == groundLayer;
        }

        private void NotifyNormalAttackDamage()
        {
            if (isSkillShot || bulletDamage <= 0)
            {
                return;
            }

            NormalAttackDamageDealt?.Invoke(bulletDamage);
        }

        private static void ShowFloatingDamage(Health targetHealth, int damage)
        {
            if (targetHealth == null || damage <= 0)
            {
                return;
            }

            FloatingDamageView view = targetHealth.GetComponentInParent<FloatingDamageView>();
            if (view == null)
            {
                view = targetHealth.gameObject.AddComponent<FloatingDamageView>();
            }

            view.Show(damage);
        }

        public bool TryDestroyByEnemyProjectileClash(EnemyProjectile enemyProjectile)
        {
            if (!canClashWithEnemyProjectile || resolved || isDestroying)
            {
                return false;
            }

            enemyProjectile = enemyProjectile != null ? enemyProjectile.ResolveInterceptTarget() : null;
            EnemyProjectile expectedTarget = ResolveForcedParryTarget();
            if (forcedParryTargetId > 0 && expectedTarget == null)
            {
                return false;
            }

            if (enemyProjectile == null || (expectedTarget != null && enemyProjectile != expectedTarget))
            {
                return false;
            }

            Vector3 impactPosition = enemyProjectile.transform.position;
            Vector2 incomingDirection = enemyProjectile.IncomingDirection;
            if (!enemyProjectile.TryDestroyByInterceptShot(out bool parried))
            {
                return false;
            }

            if (parried)
            {
                owner?.PlayParryImpact(impactPosition, incomingDirection);
            }

            DestroyByClash();
            return true;
        }

        private void DestroyByClash()
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

            if (!resolved && ResolveForcedParryTarget() != null)
            {
                forcedParryTarget.CancelInterceptReservation();
            }

            isDestroying = true;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

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

        private float ResolveCollisionRadius(float fallbackRadius)
        {
            float radius = 0f;
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D hitbox = colliders[i];
                if (hitbox == null || !hitbox.enabled)
                {
                    continue;
                }

                Bounds bounds = hitbox.bounds;
                radius = Mathf.Max(radius, bounds.extents.x, bounds.extents.y);
            }

            return Mathf.Max(0.001f, radius > 0f ? radius : fallbackRadius);
        }
    }
}
