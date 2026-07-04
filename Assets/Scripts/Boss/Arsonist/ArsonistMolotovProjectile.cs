using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist/Molotov Projectile")]
    public sealed class ArsonistMolotovProjectile : EnemyProjectile
    {
        [SerializeField] private ArsonistBossAI owner;
        [SerializeField, Min(0f)] private float explosionSecondsMin = 1.2f;
        [SerializeField, Min(0f)] private float explosionSecondsMax = 2.4f;
        [SerializeField, Min(0.05f)] private float fireRadius = 0.75f;
        [SerializeField, Min(0.05f)] private float fireDuration = 1.35f;
        [SerializeField] private Color fireColor = new(1f, 0.35f, 0.05f, 0.9f);
        [SerializeField, Min(0f)] private float parriedFireDamageDelay = 0.35f;
        [SerializeField, Min(0f)] private float lobHeight = 1.15f;
        [SerializeField, Min(0f)] private float lobScaleBonus = 0.35f;
        [SerializeField] private float lobSpinDegrees = 360f;

        private bool hasResolvedExplosionSeconds;
        private float resolvedExplosionSeconds;

        public void Initialize(ArsonistBossAI nextOwner)
        {
            owner = nextOwner != null ? nextOwner : owner;
        }

        protected override void OnProjectileAwake()
        {
            ResolveOwner();
        }

        internal void BeginLobMotion(Vector2 startPosition, Vector2 landingPosition)
        {
            float duration = Mathf.Max(0.05f, hasResolvedExplosionSeconds ? resolvedExplosionSeconds : ProjectileLifetime);
            ConfigurePathIndicatorSuppressed(true);
            OverrideProjectileLifetime(duration + 0.25f);
            gameObject.AddComponent<ArsonistMolotovLobMotion>().Initialize(
                this,
                startPosition,
                landingPosition,
                duration,
                lobHeight,
                lobScaleBonus,
                lobSpinDegrees);
        }

        internal void ExplodeFromLobMotion()
        {
            DestroyProjectile(EnemyProjectileDestroyReason.Expired);
        }

        protected override float ResolveProjectileLifetime(float configuredLifetime)
        {
            if (hasResolvedExplosionSeconds)
            {
                return resolvedExplosionSeconds;
            }

            float min = Mathf.Max(0f, explosionSecondsMin);
            float max = Mathf.Max(0f, explosionSecondsMax);
            float lower = Mathf.Min(min, max);
            float upper = Mathf.Max(min, max);
            resolvedExplosionSeconds = upper > 0f
                ? Random.Range(lower, upper)
                : Mathf.Max(0f, configuredLifetime);
            hasResolvedExplosionSeconds = true;
            return resolvedExplosionSeconds;
        }

        protected override void OnProjectileDestroying(
            EnemyProjectileDestroyReason reason,
            Vector3 position)
        {
            if (reason == EnemyProjectileDestroyReason.OwnerDestroyed)
            {
                return;
            }

            ResolveOwner();
            float playerDamageDelay = reason == EnemyProjectileDestroyReason.Intercepted
                ? parriedFireDamageDelay
                : 0f;
            if (reason == EnemyProjectileDestroyReason.Intercepted)
            {
                owner?.DelayFireDamageFor(PlayerCombatController.Active, playerDamageDelay);
            }

            owner?.CreateFireArea(position, fireRadius, fireDuration, fireColor, playerDamageDelay);
        }

        protected override void CopySpecialRuntimeStateTo(EnemyProjectile replacement)
        {
            if (replacement is not ArsonistMolotovProjectile molotov)
            {
                return;
            }

            molotov.owner = owner;
            molotov.hasResolvedExplosionSeconds = hasResolvedExplosionSeconds;
            molotov.resolvedExplosionSeconds = resolvedExplosionSeconds;
            if (hasResolvedExplosionSeconds)
            {
                molotov.OverrideProjectileLifetime(resolvedExplosionSeconds);
            }
        }

        private void ResolveOwner()
        {
            if (owner != null)
            {
                return;
            }

            owner = GetComponentInParent<ArsonistBossAI>();
            if (owner == null)
            {
                owner = FindFirstObjectByType<ArsonistBossAI>();
            }
        }
    }

    [AddComponentMenu("")]
    internal sealed class ArsonistMolotovLobMotion : MonoBehaviour
    {
        private ArsonistMolotovProjectile projectile;
        private Rigidbody2D body;
        private Vector2 startPosition;
        private Vector2 landingPosition;
        private Vector3 baseScale;
        private float durationSeconds;
        private float elapsedSeconds;
        private float height;
        private float scaleBonus;
        private float spinDegrees;
        private bool completed;

        public void Initialize(
            ArsonistMolotovProjectile nextProjectile,
            Vector2 nextStartPosition,
            Vector2 nextLandingPosition,
            float nextDurationSeconds,
            float nextHeight,
            float nextScaleBonus,
            float nextSpinDegrees)
        {
            projectile = nextProjectile;
            body = GetComponent<Rigidbody2D>();
            startPosition = nextStartPosition;
            landingPosition = nextLandingPosition;
            durationSeconds = Mathf.Max(0.05f, nextDurationSeconds);
            height = Mathf.Max(0f, nextHeight);
            scaleBonus = Mathf.Max(0f, nextScaleBonus);
            spinDegrees = nextSpinDegrees;
            baseScale = transform.localScale;
            MoveTo(startPosition, 0f);
            Tick(0f);
        }

        private void LateUpdate()
        {
            if (completed || projectile == null)
            {
                StopBody();
                return;
            }

            if (PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            elapsedSeconds += deltaTime;
            float t = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            float arc = 4f * t * (1f - t);
            Vector2 position = Vector2.Lerp(startPosition, landingPosition, t) + Vector2.up * (height * arc);
            MoveTo(position, deltaTime);
            transform.localScale = baseScale * (1f + scaleBonus * arc);
            transform.rotation = Quaternion.Euler(0f, 0f, spinDegrees * t);

            if (t < 1f)
            {
                return;
            }

            completed = true;
            MoveTo(landingPosition, deltaTime);
            transform.localScale = baseScale;
            projectile.ExplodeFromLobMotion();
        }

        private void MoveTo(Vector2 position, float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            transform.position = worldPosition;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }
}
