using UnityEngine;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerProjectile : MonoBehaviour
    {
        private const string BulletVisualName = "BulletVisual";

        private PlayerCombatController owner;
        private Rigidbody2D body;
        private float damage;
        private float heat;
        private float maxDistance;
        private float destroyAt;
        private Vector2 spawnPosition;
        private Vector2 flightDirection = Vector2.right;
        private Color projectileColor = Color.white;
        private bool canDamageHealth;
        private bool resolved;

        public static PlayerProjectile Spawn(
            PlayerProjectile prefab,
            Vector3 position,
            Vector2 direction,
            PlayerCombatController owner,
            float speed,
            float lifetime,
            float radius,
            float maxDistance,
            float damage,
            float heat,
            Color color,
            bool canDamageHealth)
        {
            if (prefab == null)
            {
                return null;
            }

            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            PlayerProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            projectile.Initialize(owner, fireDirection, speed, lifetime, radius, maxDistance, damage, heat, color, canDamageHealth);
            PlayerCombatConfig config = owner != null ? owner.Config : null;
            if (config != null)
            {
                ProjectileVfx.ApplyVisibility(
                    projectile.gameObject,
                    color,
                    radius,
                    config.ProjectileTrailSeconds,
                    config.ProjectileTrailWidthMultiplier,
                    config.ProjectileGlowScale);
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
            float nextMaxDistance,
            float nextDamage,
            float nextHeat,
            Color color,
            bool nextCanDamageHealth)
        {
            owner = nextOwner;
            damage = nextDamage;
            heat = nextHeat;
            maxDistance = nextMaxDistance;
            canDamageHealth = nextCanDamageHealth;
            spawnPosition = transform.position;
            destroyAt = Time.time + lifetime;
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
            if (Time.time >= destroyAt || Vector2.Distance(spawnPosition, transform.position) >= maxDistance)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved || !canDamageHealth)
            {
                return;
            }

            Health targetHealth = other.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth == owner.Health)
            {
                return;
            }

            resolved = true;
            targetHealth.TakeDamage(damage);
            ProjectileVfx.PlayBulletImpact(transform.position, flightDirection, projectileColor);

            HeatGauge targetHeat = targetHealth.GetComponent<HeatGauge>();
            if (targetHeat != null)
            {
                targetHeat.AddHeat(heat);
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
