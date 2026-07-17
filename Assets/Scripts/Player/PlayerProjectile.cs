using System;
using UnityEngine;
using Week14.Enemy;
using Week14.Tutorial;
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
        private Vector2 flightDirection = Vector2.right;
        private Color projectileColor = Color.white;
        private bool canDamageHealth;
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
            bool nextIsSkillShot)
        {
            RestorePooledComponents();
            resolved = false;
            isDestroying = false;
            owner = nextOwner;
            projectileSpeed = speed;
            bulletDamage = nextBulletDamage;
            collisionRadius = ResolveCollisionRadius(radius);
            canDamageHealth = nextCanDamageHealth;
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

        private bool TryResolveCollision(Collider2D other)
        {
            if (other == null || isDestroying || other.transform.IsChildOf(transform))
            {
                return false;
            }

            ConductorTurretProjectile turretProjectile = other.GetComponentInParent<ConductorTurretProjectile>();
            if (turretProjectile != null)
            {
                return TryResolveTurretHit(turretProjectile);
            }

            if (IsGroundCollider(other))
            {
                return false;
            }

            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                return false;
            }

            if (other.GetComponentInParent<EnemyProjectile>() != null)
            {
                return false;
            }

            Minion minion = other.GetComponentInParent<Minion>();
            if (minion != null)
            {
                if (minion.BlocksPlayerProjectiles)
                {
                    DestroyProjectile();
                    return true;
                }

                if (!minion.IsPlayerTargetable)
                {
                    return false;
                }
            }

            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth == null)
            {
                if (other.isTrigger)
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
            TryApplyDamageToHealth(
                targetHealth,
                bulletDamage,
                isSkillShot,
                transform.position,
                flightDirection,
                projectileColor,
                owner?.Config?.EnemyHitVfxPrefab);
            DestroyProjectile();
            return true;
        }

        // 보스/미니언(+컨덕터 공유 데미지)/튜토리얼 훈련 적/일반 Health 순으로 타입별 피해 적용 + 플로팅 데미지 숫자를
        // 담당하는 공용 로직입니다. PlayerProjectile 인스턴스 상태에 의존하지 않아서 총검(근접) 같은 비-투사체
        // 공격도 동일하게 재사용할 수 있습니다.
        internal static bool TryApplyDamageToHealth(
            Health targetHealth,
            int bulletDamage,
            bool isSkillShot,
            Vector3 hitPosition,
            Vector2 hitDirection,
            Color hitColor,
            GameObject enemyHitVfxPrefab = null)
        {
            if (targetHealth == null)
            {
                return false;
            }

            BossAI boss = targetHealth.GetComponent<BossAI>()
                ?? targetHealth.GetComponentInParent<BossAI>();
            if (boss != null)
            {
                int appliedDamage = boss is Conductor conductor
                    ? conductor.GetBodySharedDamage(bulletDamage)
                    : bulletDamage;

                if (!boss.ReceivePlayerHit(bulletDamage, true, hitPosition, hitDirection, hitColor))
                {
                    return false;
                }

                ShowFloatingDamage(targetHealth, appliedDamage);
                NotifyNormalAttackDamageStatic(bulletDamage, isSkillShot);
                return true;
            }

            Minion minion = targetHealth.GetComponent<Minion>()
                ?? targetHealth.GetComponentInParent<Minion>();
            if (minion != null)
            {
                if (!minion.IsPlayerTargetable)
                {
                    return false;
                }

                int appliedDamage = bulletDamage;
                if (minion.Owner is Conductor minionConductor
                    && minionConductor.TryGetMinionSharedDamage(minion, bulletDamage, out int sharedDamage))
                {
                    appliedDamage = sharedDamage;
                }

                if (!minion.ReceivePlayerHit(bulletDamage, true, hitPosition, hitDirection, hitColor))
                {
                    return false;
                }

                ShowFloatingDamage(targetHealth, appliedDamage);
                NotifyNormalAttackDamageStatic(bulletDamage, isSkillShot);
                return true;
            }

            TutorialTrainingEnemy tutorialEnemy = targetHealth.GetComponent<TutorialTrainingEnemy>()
                ?? targetHealth.GetComponentInParent<TutorialTrainingEnemy>();
            if (tutorialEnemy != null)
            {
                if (!tutorialEnemy.ReceivePlayerHit(bulletDamage, hitPosition, hitDirection))
                {
                    return false;
                }

                ShowFloatingDamage(targetHealth, bulletDamage);
                NotifyNormalAttackDamageStatic(bulletDamage, isSkillShot);
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
            NotifyNormalAttackDamageStatic(bulletDamage, isSkillShot);
            ProjectileVfx.PlayPrefab(
                enemyHitVfxPrefab,
                hitPosition,
                hitDirection,
                targetHealth.transform,
                followRotation: false);
            return true;
        }

        internal bool TryResolveTurretHit(ConductorTurretProjectile turret)
        {
            if (turret == null || !turret.IsAliveTurret || resolved)
            {
                return false;
            }

            resolved = true;
            if (canDamageHealth && turret.ReceivePlayerHit(bulletDamage, transform.position, flightDirection))
            {
                NotifyNormalAttackDamage();
            }

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
            NotifyNormalAttackDamageStatic(bulletDamage, isSkillShot);
        }

        private static void NotifyNormalAttackDamageStatic(int bulletDamage, bool isSkillShot)
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
