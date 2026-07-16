using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private Color ResolveInitialProjectileColor(Color overrideColor)
        {
            if (overrideColor != Color.clear)
            {
                return overrideColor;
            }

            SpriteRenderer prefabVisualRenderer = poolPrefabSource != null && poolPrefabSource != this
                ? poolPrefabSource.GetProjectileVisualRenderer()
                : null;
            if (prefabVisualRenderer != null && prefabVisualRenderer.color != Color.clear)
            {
                return prefabVisualRenderer.color;
            }

            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            if (visualRenderer != null && visualRenderer.color != Color.clear)
            {
                return visualRenderer.color;
            }

            return Color.white;
        }

        private void EnsureProjectileShape()
        {
            BossSorting.ApplyToChildren(gameObject);

            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            if (visualRenderer != null)
            {
                visualRenderer.color = projectileColor;
                visualRenderer.sortingOrder = 20;
            }

            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null)
            {
                circleCollider.isTrigger = true;
                circleCollider.radius = projectileRadius;
                return;
            }

            Collider2D projectileCollider = GetComponent<Collider2D>();
            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
            }
        }

        protected void ApplyProjectileColor(Color color)
        {
            projectileColor = color;

            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            if (visualRenderer != null)
            {
                visualRenderer.color = projectileColor;
            }

            if (!customTrailColorConfigured)
            {
                ConfigureTrailColor(projectileColor);
                customTrailColorConfigured = false;
            }
        }

        protected Sprite GetProjectileSprite()
        {
            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            return visualRenderer != null ? visualRenderer.sprite : null;
        }

        protected void ApplyProjectileSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            SpriteRenderer visualRenderer = GetProjectileVisualRenderer();
            if (visualRenderer != null)
            {
                visualRenderer.sprite = sprite;
            }
        }

        private SpriteRenderer GetProjectileVisualRenderer()
        {
            Transform visual = transform.Find(BulletVisualName);
            SpriteRenderer visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            return visualRenderer != null ? visualRenderer : GetComponent<SpriteRenderer>();
        }

    }
}
