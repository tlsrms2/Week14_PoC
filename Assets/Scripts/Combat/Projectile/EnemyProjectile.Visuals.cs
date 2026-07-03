using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void EnsureProjectileShape()
        {
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

        private void ApplyProjectileColor(Color color)
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

        private SpriteRenderer GetProjectileVisualRenderer()
        {
            Transform visual = transform.Find(BulletVisualName);
            SpriteRenderer visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            return visualRenderer != null ? visualRenderer : GetComponent<SpriteRenderer>();
        }

    }
}
