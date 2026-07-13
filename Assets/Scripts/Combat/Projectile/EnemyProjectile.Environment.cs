using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private float GetWallClippedLength(Vector2 start, Vector2 direction, float length)
        {
            return TryGetWallHit(start, direction, length, out RaycastHit2D hit)
                ? Mathf.Max(0f, hit.distance)
                : length;
        }

        protected virtual bool TryDestroyIfCrossedWall()
        {
            Vector2 currentPosition = transform.position;
            Vector2 delta = currentPosition - lastWallCheckPosition;
            if (delta.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            if (!TryGetWallHit(lastWallCheckPosition, delta.normalized, delta.magnitude + projectileRadius, out _))
            {
                return false;
            }

            DestroyProjectile();
            return true;
        }

        private bool TryGetWallHit(Vector2 start, Vector2 direction, float distance, out RaycastHit2D hit)
        {
            hit = default;
            int wallMask = GetWallMask();
            if (wallMask == 0 || distance <= 0.001f || direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float castRadius = Mathf.Max(0.001f, projectileRadius);
            hit = Physics2D.CircleCast(start, castRadius, direction.normalized, distance, wallMask);
            return hit.collider != null;
        }

        private static bool IsWallCollider(Collider2D collider)
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            return wallLayer >= 0
                && collider != null
                && collider.gameObject.layer == wallLayer;
        }

        private static bool IsGroundCollider(Collider2D collider)
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            return groundLayer >= 0
                && collider != null
                && collider.gameObject.layer == groundLayer;
        }

        private static int GetWallMask()
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            return wallLayer >= 0 ? 1 << wallLayer : 0;
        }

    }
}
