using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist/Molotov Projectile")]
    public sealed class ArsonistMolotovProjectile : EnemyProjectile
    {
        private const int ImpactCircleSegments = 32;

        [SerializeField] private ArsonistBossAI owner;
        [SerializeField, Min(0f)] private float explosionSecondsMin = 1.2f;
        [SerializeField, Min(0f)] private float explosionSecondsMax = 2.4f;
        [SerializeField, Min(0.05f)] private float fireRadius = 0.75f;
        [SerializeField, Min(0.05f)] private float fireDuration = 1.35f;
        [SerializeField, Min(0f)] private float fireSpreadSeconds = 0.35f;
        [SerializeField] private Color fireColor = new(1f, 0.35f, 0.05f, 0.9f);
        [SerializeField, Min(0f)] private float lobHeight = 1.15f;
        [SerializeField, Min(0f)] private float lobScaleBonus = 0.35f;
        [SerializeField] private float lobSpinDegrees = 360f;

        private Transform indicatorRoot;
        private LineRenderer impactIndicatorCircle;
        private Vector2 lockedLinearImpactPoint;
        private float indicatorEndsAt;
        private bool hasResolvedExplosionSeconds;
        private float resolvedExplosionSeconds;
        private bool linearImpactPointLocked;
        private bool usesLobIndicator;
        private ArsonistMolotovLobMotion lobMotion;
        private static Material indicatorMaterial;

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
            if (lobMotion != null)
            {
                lobMotion.Cancel();
                lobMotion = null;
            }

            float duration = Mathf.Max(0.05f, hasResolvedExplosionSeconds ? resolvedExplosionSeconds : ProjectileLifetime);
            lockedLinearImpactPoint = landingPosition;
            linearImpactPointLocked = true;
            ConfigurePathIndicatorSuppressed(true);
            ConfigureImpactIndicator(landingPosition, duration);
            usesLobIndicator = true;
            OverrideProjectileLifetime(duration + 0.25f);
            if (ProjectileBody != null)
            {
                ProjectileBody.linearVelocity = Vector2.zero;
                ProjectileBody.angularVelocity = 0f;
            }

            lobMotion = gameObject.AddComponent<ArsonistMolotovLobMotion>();
            lobMotion.Initialize(
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

        protected override void OnProjectileInitialized()
        {
            ConfigurePathIndicatorSuppressed(true);
            if (IsLaunched)
            {
                BeginLobMotion(transform.position, GetLinearImpactPoint(transform.position));
                return;
            }

            RefreshLinearImpactIndicator();
        }

        protected override void OnProjectileChargeTick()
        {
            RefreshLinearImpactIndicator();
        }

        protected override void OnProjectileLaunched()
        {
            BeginLobMotion(transform.position, GetLinearImpactPoint(transform.position));
        }

        protected override void OnProjectileTick()
        {
            if (!usesLobIndicator)
            {
                RefreshLinearImpactIndicator();
                return;
            }

            if (indicatorEndsAt > 0f && Time.time >= indicatorEndsAt)
            {
                SetImpactIndicatorVisible(false);
            }
        }

        protected override void ExtendSpecialTimers(float pausedSeconds)
        {
            if (indicatorEndsAt > 0f)
            {
                indicatorEndsAt += pausedSeconds;
            }
        }

        protected override void OnProjectileDestroying(
            EnemyProjectileDestroyReason reason,
            Vector3 position)
        {
            SetImpactIndicatorVisible(false);
            if (reason == EnemyProjectileDestroyReason.OwnerDestroyed)
            {
                return;
            }

            if (reason == EnemyProjectileDestroyReason.Intercepted)
            {
                return;
            }

            ResolveOwner();
            Vector3 explosionPosition = reason == EnemyProjectileDestroyReason.Expired && linearImpactPointLocked
                ? lockedLinearImpactPoint
                : position;
            owner?.CreateFireArea(explosionPosition, fireRadius, fireDuration, fireColor, 0f, fireSpreadSeconds);
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

        private void RefreshLinearImpactIndicator()
        {
            if (usesLobIndicator || IsDestroying)
            {
                return;
            }

            if (IsLaunched)
            {
                LockLinearImpactPoint();
                ConfigureImpactIndicator(lockedLinearImpactPoint, Mathf.Max(0f, indicatorEndsAt - Time.time));
                return;
            }

            Vector2 start = transform.position;
            float duration = GetResolvedExplosionDuration();
            Vector2 impactPoint = GetLinearImpactPoint(start);
            ConfigureImpactIndicator(impactPoint, duration);
        }

        private void LockLinearImpactPoint()
        {
            if (linearImpactPointLocked)
            {
                return;
            }

            Vector2 start = transform.position;
            float duration = GetResolvedExplosionDuration();
            lockedLinearImpactPoint = GetLinearImpactPoint(start);
            indicatorEndsAt = Time.time + duration;
            linearImpactPointLocked = true;
        }

        private Vector2 GetLinearImpactPoint(Vector2 start)
        {
            Vector2 direction = FlightDirection.sqrMagnitude > 0.0001f ? FlightDirection.normalized : Vector2.left;
            float duration = GetResolvedExplosionDuration();
            return start + direction * (ProjectileSpeed * duration);
        }

        private float GetResolvedExplosionDuration()
        {
            return Mathf.Max(0.05f, hasResolvedExplosionSeconds ? resolvedExplosionSeconds : ProjectileLifetime);
        }

        private void ConfigureImpactIndicator(Vector2 impactPoint, float duration)
        {
            if (duration > 0f)
            {
                indicatorEndsAt = Time.time + duration;
            }

            DrawImpactCircle(impactPoint);
        }

        private void DrawImpactCircle(Vector2 center)
        {
            LineRenderer circle = EnsureImpactCircle();
            if (circle == null)
            {
                return;
            }

            Color color = GetIndicatorColor(0.9f);
            float radius = Mathf.Max(0.05f, fireRadius);
            float width = Mathf.Max(0.014f, ProjectileRadius * 0.14f);
            circle.enabled = true;
            circle.loop = true;
            circle.positionCount = ImpactCircleSegments;
            circle.startColor = color;
            circle.endColor = color;
            circle.startWidth = width;
            circle.endWidth = width;
            for (int i = 0; i < ImpactCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ImpactCircleSegments;
                circle.SetPosition(i, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        private Color GetIndicatorColor(float alpha)
        {
            Color color = IsLaunched ? LaunchedColor : ChargingColor;
            if (color == Color.clear)
            {
                color = fireColor;
            }

            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private LineRenderer EnsureImpactCircle()
        {
            EnsureIndicatorRoot();
            if (indicatorRoot == null)
            {
                return null;
            }

            if (impactIndicatorCircle != null)
            {
                return impactIndicatorCircle;
            }

            GameObject circleObject = new("MolotovImpactIndicator");
            circleObject.transform.SetParent(indicatorRoot, false);
            impactIndicatorCircle = circleObject.AddComponent<LineRenderer>();
            ConfigureIndicatorLine(impactIndicatorCircle, 20);
            return impactIndicatorCircle;
        }

        private void EnsureIndicatorRoot()
        {
            if (indicatorRoot != null)
            {
                return;
            }

            GameObject rootObject = new("MolotovPathIndicator");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            indicatorRoot = rootObject.transform;
        }

        private static void ConfigureIndicatorLine(LineRenderer line, int sortingOrder)
        {
            if (line == null)
            {
                return;
            }

            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            BossSorting.Apply(line);
            line.sortingOrder = sortingOrder;
            line.material = GetIndicatorMaterial();
        }

        private static Material GetIndicatorMaterial()
        {
            if (indicatorMaterial != null)
            {
                return indicatorMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            indicatorMaterial = shader != null ? new Material(shader) : null;
            return indicatorMaterial;
        }

        private void SetImpactIndicatorVisible(bool visible)
        {
            if (impactIndicatorCircle != null)
            {
                impactIndicatorCircle.enabled = visible;
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

        internal void Cancel()
        {
            completed = true;
            enabled = false;
        }

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
