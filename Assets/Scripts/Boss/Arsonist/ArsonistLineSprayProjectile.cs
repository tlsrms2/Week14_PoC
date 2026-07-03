using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public abstract class ArsonistLineSprayProjectile : EnemyProjectile
    {
        private const string WallLayerName = "Wall";

        [SerializeField] private ArsonistBossAI owner;
        [SerializeField, Min(0.05f)] private float patchRadius = 0.35f;
        [SerializeField, Min(0.05f)] private float patchDuration = 4f;
        [SerializeField, Min(0.05f)] private float paintSpacing = 0.28f;
        [SerializeField] private bool destroyOnWall = true;

        private Vector2 previousPosition;
        private float distanceSinceLastPaint;
        private bool paintStarted;
        private bool paintedInitial;

        protected ArsonistBossAI Owner => owner;
        protected float PatchRadius => patchRadius;
        protected float PatchDuration => patchDuration;
        protected override bool UsesProjectileVisibility => false;
        protected override bool ShowsPathIndicator => true;

        public void Launch(ArsonistBossAI nextOwner, Vector2 launchDirection)
        {
            owner = nextOwner != null ? nextOwner : ResolveOwner();
            FlightDirection = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : transform.right;
        }

        protected abstract void PlaceHazard(Vector3 position, float radius, float duration);

        protected override void OnProjectileInitialized()
        {
            ResolveOwner();
            ConfigureInterceptable(false);
            DisableCarrierVisuals();
            ResetPaintState();
            if (IsLaunched)
            {
                BeginPaint();
            }
        }

        protected override void OnProjectileLaunched()
        {
            ConfigureInterceptable(false);
            DisableCarrierVisuals();
            ResetPaintState();
            BeginPaint();
        }

        protected override void OnProjectileTick()
        {
            if (!IsLaunched || IsDestroying)
            {
                return;
            }

            if (!paintStarted)
            {
                BeginPaint();
            }

            Vector2 currentPosition = transform.position;
            PaintSegment(previousPosition, currentPosition);
            previousPosition = currentPosition;
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            PlayerProjectile playerProjectile = other.GetComponentInParent<PlayerProjectile>();
            if (playerProjectile != null)
            {
                playerProjectile.TryDestroyByEnemyProjectileClash(this);
                return;
            }

            if (destroyOnWall && IsWallCollider(other))
            {
                DestroyProjectile();
            }
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return false;
        }

        private void DisableCarrierVisuals()
        {
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].enabled = false;
                }
            }

            TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                if (trailRenderers[i] != null)
                {
                    trailRenderers[i].enabled = false;
                }
            }
        }

        private void ResetPaintState()
        {
            previousPosition = transform.position;
            distanceSinceLastPaint = 0f;
            paintStarted = false;
            paintedInitial = false;
        }

        private void BeginPaint()
        {
            paintStarted = true;
            previousPosition = transform.position;
            PaintSegment(previousPosition, previousPosition);
        }

        private ArsonistBossAI ResolveOwner()
        {
            if (owner != null)
            {
                return owner;
            }

            owner = OwnerBoss as ArsonistBossAI;
            if (owner != null)
            {
                return owner;
            }

            owner = GetComponentInParent<ArsonistBossAI>();
            if (owner == null)
            {
                owner = FindFirstObjectByType<ArsonistBossAI>();
            }

            return owner;
        }

        private void PaintSegment(Vector2 from, Vector2 to)
        {
            if (!paintedInitial)
            {
                PlaceHazard(from, patchRadius, patchDuration);
                paintedInitial = true;
            }

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
            {
                return;
            }

            Vector2 normal = delta / distance;
            float spacing = Mathf.Max(0.05f, paintSpacing);
            float remaining = distance;
            Vector2 cursor = from;

            while (distanceSinceLastPaint + remaining >= spacing)
            {
                float step = spacing - distanceSinceLastPaint;
                cursor += normal * step;
                PlaceHazard(cursor, patchRadius, patchDuration);
                remaining -= step;
                distanceSinceLastPaint = 0f;
            }

            distanceSinceLastPaint += remaining;
        }

        private static bool IsWallCollider(Collider2D collider)
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            return wallLayer >= 0
                && collider != null
                && collider.gameObject.layer == wallLayer;
        }
    }
}
