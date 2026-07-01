using UnityEngine;
using UnityEngine.Tilemaps;

namespace Week14.Combat
{
    public static class GroundMovementConstraint
    {
        private const string GroundLayerName = "Ground";
        private const float DefaultProbeRadius = 0.12f;
        private const float MinProbeRadius = 0.01f;
        private static Tilemap[] groundTilemaps;
        private static int cachedGroundLayer = -1;

        public static Vector2 ClampVelocity(Rigidbody2D body, Vector2 velocity)
        {
            return ClampVelocity(body, velocity, null);
        }

        public static Vector2 ClampVelocity(Rigidbody2D body, Vector2 velocity, Collider2D[] probeColliders)
        {
            if (body == null || velocity.sqrMagnitude <= 0.0001f)
            {
                return velocity;
            }

            float stepSeconds = Mathf.Max(Time.fixedDeltaTime, Time.deltaTime);
            if (stepSeconds <= 0f)
            {
                return velocity;
            }

            Vector2 current = body.position;
            Vector2 next = ClampStep(current, current + velocity * stepSeconds, DefaultProbeRadius, probeColliders);
            return (next - current) / stepSeconds;
        }

        public static Vector2 ClampStep(Vector2 current, Vector2 target)
        {
            return ClampStep(current, target, null);
        }

        public static Vector2 ClampStep(Vector2 current, Vector2 target, Collider2D[] probeColliders)
        {
            return ClampStep(current, target, DefaultProbeRadius, probeColliders);
        }

        private static Vector2 ClampStep(
            Vector2 current,
            Vector2 target,
            float probeRadius,
            Collider2D[] probeColliders)
        {
            if (IsGrounded(target, probeRadius, current, probeColliders))
            {
                return target;
            }

            Vector2 xOnly = new(target.x, current.y);
            if (IsGrounded(xOnly, probeRadius, current, probeColliders))
            {
                return xOnly;
            }

            Vector2 yOnly = new(current.x, target.y);
            return IsGrounded(yOnly, probeRadius, current, probeColliders) ? yOnly : current;
        }

        private static bool IsGrounded(
            Vector2 position,
            float probeRadius,
            Vector2 current,
            Collider2D[] probeColliders)
        {
            int groundMask = GetGroundMask();
            if (groundMask == 0)
            {
                return true;
            }

            if (IsGroundedAt(position, probeRadius, groundMask))
            {
                return true;
            }

            if (probeColliders == null || probeColliders.Length == 0)
            {
                return false;
            }

            Vector2 delta = position - current;
            for (int i = 0; i < probeColliders.Length; i++)
            {
                Collider2D probeCollider = probeColliders[i];
                if (probeCollider == null || !probeCollider.enabled || !probeCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = probeCollider.bounds;
                Vector2 center = (Vector2)bounds.center + delta;
                Vector2 extents = bounds.extents;
                if (IsGroundedAt(center, probeRadius, groundMask)
                    || IsGroundedAt(center + new Vector2(0f, -extents.y), probeRadius, groundMask)
                    || IsGroundedAt(center + new Vector2(-extents.x, 0f), probeRadius, groundMask)
                    || IsGroundedAt(center + new Vector2(extents.x, 0f), probeRadius, groundMask))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsGroundedAt(Vector2 position, float probeRadius, int groundMask)
        {
            return HasGroundTile(position)
                || Physics2D.OverlapCircle(position, Mathf.Max(MinProbeRadius, probeRadius), groundMask) != null;
        }

        private static int GetGroundMask()
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            return groundLayer >= 0 ? 1 << groundLayer : 0;
        }

        private static bool HasGroundTile(Vector2 position)
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                return true;
            }

            RefreshGroundTilemapsIfNeeded(groundLayer);
            if (groundTilemaps == null || groundTilemaps.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < groundTilemaps.Length; i++)
            {
                Tilemap tilemap = groundTilemaps[i];
                if (tilemap == null || !tilemap.isActiveAndEnabled)
                {
                    continue;
                }

                Vector3Int cell = tilemap.WorldToCell(position);
                if (tilemap.HasTile(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RefreshGroundTilemapsIfNeeded(int groundLayer)
        {
            if (groundTilemaps != null && cachedGroundLayer == groundLayer)
            {
                return;
            }

            cachedGroundLayer = groundLayer;
            Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] != null && tilemaps[i].gameObject.layer == groundLayer)
                {
                    count++;
                }
            }

            groundTilemaps = new Tilemap[count];
            int index = 0;
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] != null && tilemaps[i].gameObject.layer == groundLayer)
                {
                    groundTilemaps[index] = tilemaps[i];
                    index++;
                }
            }
        }
    }
}
