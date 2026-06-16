using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private const int DefaultCounteredProjectileBulletDamage = 34;
        private const string BulletVisualName = "BulletVisual";
        private const string ChargeVfxName = "ChargeVfx";

        private float projectileSpeed;
        private float projectileLifetime;
        private float projectileRadius;
        private float projectileChargeSeconds;
        private Color projectileColor;
        private float homingTurnDegreesPerSecond;
        private float homingSeconds;
        private float homingEndsAt;
        private float chargeEndsAt;
        private Rigidbody2D body;
        private LineRenderer chargeVfx;
        private BulletGauge ownerBullets;
        private EnemyAI ownerEnemy;
        private int counterBulletDamage;
        private int bulletDamage;
        private float destroyAt;
        private Vector2 flightDirection = Vector2.left;
        private bool resolved;
        private bool isDestroying;
        private bool launched;
        private bool ownerSlotReleased;
        private static Material chargeVfxMaterial;

        public Vector2 IncomingDirection => flightDirection;
        public bool IsCharging => !resolved && !isDestroying && !launched;
        public bool CanBeIntercepted => !resolved && !isDestroying;
        public float LockOnRadius => Mathf.Max(0.24f, projectileRadius * 2.6f);


        /// <summary>EnemyData 기반 스폰 (신규 AI 시스템용)</summary>
        public static EnemyProjectile Spawn(
            EnemyProjectile prefab,
            EnemyData data,
            BulletGauge ownerBullets,
            Vector3 position,
            Vector2 direction)
        {
            if (prefab == null || data == null) return null;

            return SpawnInternal(prefab, ownerBullets, position, direction, data.ProjectileBulletDamage,
                GetCounteredProjectileBulletDamage(),
                data.ProjectileChargeSeconds, data.ProjectileSpeed, data.ProjectileLifetime, data.ProjectileRadius,
                data.ProjectileColor, data.ProjectileTrailSeconds,
                data.ProjectileTrailWidthMultiplier,
                data.ProjectileHomingSeconds,
                data.ProjectileHomingTurnDegreesPerSecond);
        }

        private static int GetCounteredProjectileBulletDamage()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            PlayerCombatConfig config = player != null ? player.Config : null;
            return config != null ? config.CounteredProjectileBulletDamage : DefaultCounteredProjectileBulletDamage;
        }

        private static EnemyProjectile SpawnInternal(
            EnemyProjectile prefab,
            BulletGauge ownerBullets,
            Vector3 position,
            Vector2 direction,
            int bulletDamage,
            int counterBulletDamage,
            float chargeSeconds,
            float speed, float lifetime, float radius,
            Color color, float trailSeconds, float trailWidth,
            float homingSeconds, float homingTurnDegrees)
        {
            Vector2 fireDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            float angle = Mathf.Atan2(fireDirection.y, fireDirection.x) * Mathf.Rad2Deg;
            EnemyProjectile projectile = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, angle));
            projectile.Initialize(direction, bulletDamage, ownerBullets, counterBulletDamage, chargeSeconds, speed, lifetime, radius, color, homingSeconds, homingTurnDegrees);
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
            int nextBulletDamage,
            BulletGauge nextOwnerBullets,
            int nextCounterBulletDamage,
            float chargeSeconds,
            float speed, float lifetime, float radius, Color color,
            float homingSeconds, float nextHomingTurnDegrees)
        {
            projectileSpeed = speed;
            projectileLifetime = lifetime;
            projectileRadius = radius;
            projectileChargeSeconds = Mathf.Max(0f, chargeSeconds);
            projectileColor = color;
            this.homingSeconds = Mathf.Max(0f, homingSeconds);
            homingTurnDegreesPerSecond = Mathf.Max(0f, nextHomingTurnDegrees);
            ownerBullets = nextOwnerBullets;
            ownerEnemy = ownerBullets != null ? ownerBullets.GetComponentInParent<EnemyAI>() : null;
            ownerEnemy?.RegisterActiveProjectile(this);
            counterBulletDamage = nextCounterBulletDamage;
            bulletDamage = nextBulletDamage;
            flightDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
            launched = projectileChargeSeconds <= 0f;
            chargeEndsAt = Time.time + projectileChargeSeconds;
            float launchTime = launched ? Time.time : chargeEndsAt;
            homingEndsAt = launchTime + this.homingSeconds;
            destroyAt = launchTime + lifetime;

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            EnsureProjectileShape();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.linearVelocity = launched ? flightDirection * projectileSpeed : Vector2.zero;

            if (launched)
            {
                SetChargeVfxVisible(false);
            }
            else
            {
                UpdateChargeVfx();
            }
        }

        private void Update()
        {
            if (isDestroying)
            {
                return;
            }

            if (!launched)
            {
                TickCharge();
            }
            else
            {
                TickHoming();
            }

            if (Time.time >= destroyAt)
            {
                DestroyProjectile();
            }
        }

        private void TickCharge()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            AimAtPlayerWhileCharging();
            UpdateChargeVfx();
            if (Time.time < chargeEndsAt)
            {
                return;
            }

            launched = true;
            SetChargeVfxVisible(false);
            if (body != null)
            {
                body.linearVelocity = flightDirection * projectileSpeed;
            }
        }

        private void AimAtPlayerWhileCharging()
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
            float angle = Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void TickHoming()
        {
            if (resolved || isDestroying || Time.time >= homingEndsAt || homingTurnDegreesPerSecond <= 0f || projectileSpeed <= 0f)
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerProjectile playerProjectile = other.GetComponentInParent<PlayerProjectile>();
            if (playerProjectile != null)
            {
                playerProjectile.TryDestroyByEnemyProjectileClash(this);
                return;
            }

            if (IsCharging)
            {
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null)
            {
                EnemyAI hitEnemy = other.GetComponentInParent<EnemyAI>();
                if (hitEnemy != null)
                {
                    if (hitEnemy == ownerEnemy)
                    {
                        return;
                    }

                    DestroyProjectile();
                    return;
                }

                if (other.GetComponentInParent<EnemyProjectile>() == null)
                {
                    DestroyProjectile();
                }

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

        public bool TryDestroyByInterceptShot(out bool parried)
        {
            if (resolved || isDestroying)
            {
                parried = false;
                return false;
            }

            parried = IsCharging;
            resolved = true;
            SpendOwnerBulletsOnCounter(parried ? BulletChangeSource.Parry : BulletChangeSource.Defense);

            DestroyProjectile();
            return true;
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
            ReleaseOwnerProjectileSlot();
        }

        private void ReleaseOwnerProjectileSlot()
        {
            if (ownerSlotReleased)
            {
                return;
            }

            ownerSlotReleased = true;
            ownerEnemy?.UnregisterActiveProjectile(this);
        }

        private void SpendOwnerBulletsOnCounter(BulletChangeSource source)
        {
            if (ownerBullets == null)
            {
                return;
            }

            ownerBullets.TrySpend(counterBulletDamage, source);
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

        private void UpdateChargeVfx()
        {
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
            line.startWidth = Mathf.Max(0.012f, projectileRadius * 0.22f);
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
            GameObject vfxObject = existing != null ? existing.gameObject : new GameObject(ChargeVfxName);
            vfxObject.transform.SetParent(transform, false);
            vfxObject.transform.localPosition = Vector3.zero;
            vfxObject.transform.localRotation = Quaternion.identity;
            vfxObject.transform.localScale = Vector3.one;

            chargeVfx = vfxObject.GetComponent<LineRenderer>();
            if (chargeVfx == null)
            {
                chargeVfx = vfxObject.AddComponent<LineRenderer>();
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

        private static Sprite CreateRuntimeSprite()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
