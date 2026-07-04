using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public abstract class ArsonistLineSprayProjectile : EnemyProjectile
    {
        private const string BulletVisualName = "BulletVisual";
        private const string VisualName = "Visual";
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
        protected float PaintSpacing => Mathf.Max(0.05f, paintSpacing);
        protected override bool UsesProjectileVisibility => false;
        protected override bool ShowsPathIndicator => true;
        protected abstract Color HazardColor { get; }

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
            ResetPaintState();
            if (IsLaunched)
            {
                SetProjectileVisualVisible(false);
                BeginPaint();
                return;
            }

            SetProjectileVisualVisible(true);
        }

        protected override void OnProjectileLaunched()
        {
            ConfigureInterceptable(false);
            SetProjectileVisualVisible(WillSplitRadiallyOnLaunch);
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

        private void SetProjectileVisualVisible(bool visible)
        {
            Transform visual = ResolveProjectileVisualRoot();
            if (visual != null && visible)
            {
                visual.gameObject.SetActive(true);
            }

            SpriteRenderer[] spriteRenderers = visual != null
                ? visual.GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponents<SpriteRenderer>();
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    if (visible)
                    {
                        spriteRenderers[i].gameObject.SetActive(true);
                    }

                    spriteRenderers[i].enabled = visible;
                }
            }

            TrailRenderer[] trailRenderers = visual != null
                ? visual.GetComponentsInChildren<TrailRenderer>(true)
                : GetComponents<TrailRenderer>();
            for (int i = 0; i < trailRenderers.Length; i++)
            {
                if (trailRenderers[i] != null)
                {
                    if (visible)
                    {
                        trailRenderers[i].gameObject.SetActive(true);
                    }

                    trailRenderers[i].enabled = visible;
                }
            }
        }

        private Transform ResolveProjectileVisualRoot()
        {
            Transform visual = transform.Find(BulletVisualName);
            if (visual != null)
            {
                return visual;
            }

            visual = transform.Find(VisualName);
            if (visual != null)
            {
                return visual;
            }

            SpriteRenderer childRenderer = GetComponentInChildren<SpriteRenderer>(true);
            return childRenderer != null && childRenderer.transform != transform
                ? childRenderer.transform
                : null;
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
            float spacing = PaintSpacing;
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
