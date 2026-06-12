using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyProjectile : MonoBehaviour, IAttackSource
    {
        private const string BulletVisualName = "BulletVisual";

        private float projectileSpeed;
        private float projectileLifetime;
        private float projectileRadius;
        private Color projectileColor;
        private Rigidbody2D body;
        private HeatGauge ownerHeat;
        private float parryHeat;
        private float parryHeatCoolingDelay;
        private float damage;
        private float destroyAt;
        private Vector2 flightDirection = Vector2.left;
        private bool resolved;

        public Transform SourceTransform => transform;


        /// <summary>EnemyData 기반 스폰 (신규 AI 시스템용)</summary>
        public static EnemyProjectile Spawn(
            EnemyProjectile prefab,
            EnemyData data,
            HeatGauge ownerHeat,
            Vector3 position,
            Vector2 direction,
            float damage)
        {
            if (prefab == null || data == null) return null;

            return SpawnInternal(prefab, ownerHeat, position, direction, damage,
                data.HeatPerShot, data.HeatCoolingDelayAfterShot,
                data.ProjectileSpeed, data.ProjectileLifetime, data.ProjectileRadius,
                data.ProjectileColor, data.ProjectileTrailSeconds,
                data.ProjectileTrailWidthMultiplier);
        }

        private static EnemyProjectile SpawnInternal(
            EnemyProjectile prefab,
            HeatGauge ownerHeat,
            Vector3 position,
            Vector2 direction,
            float damage,
            float parryHeat,
            float parryHeatCoolingDelay,
            float speed, float lifetime, float radius,
            Color color, float trailSeconds, float trailWidth)
        {
            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            EnemyProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            projectile.Initialize(direction, damage, ownerHeat, parryHeat, parryHeatCoolingDelay, speed, lifetime, radius, color);
            ProjectileVfx.ApplyVisibility(
                projectile.gameObject, color, radius, trailSeconds, trailWidth);
            return projectile;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Initialize(
            Vector2 direction,
            float nextDamage,
            HeatGauge nextOwnerHeat,
            float nextParryHeat,
            float nextParryHeatCoolingDelay,
            float speed, float lifetime, float radius, Color color)
        {
            projectileSpeed = speed;
            projectileLifetime = lifetime;
            projectileRadius = radius;
            projectileColor = color;
            ownerHeat = nextOwnerHeat;
            parryHeat = nextParryHeat;
            parryHeatCoolingDelay = nextParryHeatCoolingDelay;
            damage = nextDamage;
            destroyAt = Time.time + lifetime;
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            EnsureProjectileShape();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = flightDirection * projectileSpeed;
        }

        private void Update()
        {
            if (Time.time >= destroyAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved)
            {
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null)
            {
                return;
            }

            resolved = true;
            player.ReceiveAttack(damage);
            ProjectileVfx.PlayBulletImpact(transform.position, flightDirection, projectileColor);
            Destroy(gameObject);
        }

        public bool TryParry(PlayerCombatController player)
        {
            if (resolved)
            {
                return false;
            }

            resolved = true;
            AddOwnerHeatOnParry();
            player?.PlayParryImpact(transform.position, flightDirection);
            Destroy(gameObject);
            return true;
        }

        private void AddOwnerHeatOnParry()
        {
            if (ownerHeat == null)
            {
                return;
            }

            ownerHeat.AddHeat(parryHeat);
            ownerHeat.SuppressCooling(parryHeatCoolingDelay);
        }

        private void EnsureProjectileShape()
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

            visualRenderer.color = projectileColor;
            visualRenderer.sortingOrder = 20;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = new Vector3(Mathf.Max(0.14f, projectileRadius * 6f), Mathf.Max(0.018f, projectileRadius * 0.75f), 1f);

            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider == null)
            {
                circleCollider = gameObject.AddComponent<CircleCollider2D>();
            }

            circleCollider.isTrigger = true;
            circleCollider.radius = projectileRadius;
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
