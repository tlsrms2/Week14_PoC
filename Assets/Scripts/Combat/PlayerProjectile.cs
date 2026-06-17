using System;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerProjectile : MonoBehaviour
    {
        private const string BulletVisualName = "BulletVisual";

        private PlayerCombatController owner;
        private Rigidbody2D body;
        private float projectileSpeed;
        private int bulletDamage;
        private float collisionRadius;
        private float destroyAt;
        private Vector2 previousPosition;
        private EnemyProjectile forcedParryTarget;
        private float forcedParryResolveAt;
        private Vector2 flightDirection = Vector2.right;
        private Color projectileColor = Color.white;
        private bool canDamageHealth;
        private bool canClashWithEnemyProjectile;
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
            bool canClashWithEnemyProjectile = false)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            PlayerProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            projectile.Initialize(owner, fireDirection, speed, lifetime, radius, bulletDamage, color, canDamageHealth, canClashWithEnemyProjectile);
            PlayerCombatConfig config = owner != null ? owner.Config : null;
            if (config != null)
            {
                ProjectileVfx.ApplyVisibility(
                    projectile.gameObject,
                    color,
                    radius,
                    config.ProjectileTrailSeconds,
                    config.ProjectileTrailWidthMultiplier);
            }

            return projectile;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Initialize(
            PlayerCombatController nextOwner,
            Vector2 direction,
            float speed,
            float lifetime,
            float radius,
            int nextBulletDamage,
            Color color,
            bool nextCanDamageHealth,
            bool nextCanClashWithEnemyProjectile)
        {
            owner = nextOwner;
            projectileSpeed = speed;
            bulletDamage = nextBulletDamage;
            collisionRadius = radius;
            canDamageHealth = nextCanDamageHealth;
            canClashWithEnemyProjectile = nextCanClashWithEnemyProjectile;
            destroyAt = Time.time + lifetime;
            previousPosition = transform.position;
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            projectileColor = color;

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            EnsureProjectileShape(color, radius);
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = flightDirection * speed;
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
            SetForcedTarget(enemyProjectile);
        }

        private void SetForcedTarget(EnemyProjectile enemyProjectile)
        {
            forcedParryTarget = enemyProjectile;
            if (forcedParryTarget == null || projectileSpeed <= 0f)
            {
                forcedParryResolveAt = 0f;
                return;
            }

            float distance = Vector2.Distance(transform.position, forcedParryTarget.transform.position);
            forcedParryResolveAt = Time.time + Mathf.Max(0.02f, distance / projectileSpeed);
        }

        private bool TryResolveForcedParryTarget()
        {
            if (!canClashWithEnemyProjectile || forcedParryTarget == null || resolved || isDestroying)
            {
                return false;
            }

            Vector2 targetPosition = forcedParryTarget.transform.position;
            float hitRadius = Mathf.Max(0.12f, collisionRadius * 2.5f);
            bool closeEnough = Vector2.Distance(transform.position, targetPosition) <= hitRadius;
            bool reachedExpectedTime = forcedParryResolveAt > 0f && Time.time >= forcedParryResolveAt;
            bool crossedTarget = Vector2.Dot(flightDirection, targetPosition - (Vector2)transform.position) <= 0f;
            if (!closeEnough && !reachedExpectedTime && !crossedTarget)
            {
                return false;
            }

            transform.position = forcedParryTarget.transform.position;
            return TryDestroyByEnemyProjectileClash(forcedParryTarget);
        }

        private bool TryResolveCollision(Collider2D other)
        {
            if (other == null || isDestroying || other.transform.IsChildOf(transform))
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
            EnemyAI enemy = targetHealth.GetComponent<EnemyAI>()
                ?? targetHealth.GetComponentInParent<EnemyAI>();
            PlayerCombatConfig config = owner != null ? owner.Config : null;
            if (enemy != null)
            {
                enemy.ReceivePlayerHit(bulletDamage, true, transform.position, flightDirection, projectileColor);
                DestroyProjectile();
                return true;
            }

            BossAI boss = targetHealth.GetComponent<BossAI>()
                ?? targetHealth.GetComponentInParent<BossAI>();
            if (boss != null)
            {
                boss.ReceivePlayerHit(bulletDamage, true, transform.position, flightDirection, projectileColor);
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

        public bool TryDestroyByEnemyProjectileClash(EnemyProjectile enemyProjectile)
        {
            if (!canClashWithEnemyProjectile || resolved || isDestroying)
            {
                return false;
            }

            if (enemyProjectile == null)
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
            else
            {
                owner?.PlayDefenseImpact(impactPosition, incomingDirection);
            }

            DestroyByClash();
            return true;
        }

        public void DestroyAfterParryResolved()
        {
            if (isDestroying)
            {
                return;
            }

            DestroyByClash();
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

        private void EnsureProjectileShape(Color color, float radius)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            Transform visual = transform.Find(BulletVisualName);
            if (visual == null)
            {
                GameObject visualObject = new GameObject(BulletVisualName);
                visualObject.transform.SetParent(transform, false);
                visual = visualObject.transform;
            }

            SpriteRenderer visualRenderer = visual.GetComponent<SpriteRenderer>();
            if (visualRenderer == null)
            {
                visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
            }

            if (visualRenderer.sprite == null)
            {
                visualRenderer.sprite = CreateRuntimeSprite();
            }

            visualRenderer.color = color;
            visualRenderer.sortingOrder = 21;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(Mathf.Max(0.14f, radius * 6f), Mathf.Max(0.018f, radius * 0.75f), 1f);

            CircleCollider2D hitbox = GetComponent<CircleCollider2D>();
            if (hitbox == null)
            {
                hitbox = gameObject.AddComponent<CircleCollider2D>();
            }

            hitbox.isTrigger = true;
            hitbox.radius = radius;
            transform.localScale = Vector3.one;
        }

        private static Sprite CreateRuntimeSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
