using UnityEngine;
using UnityEngine.Tilemaps;

namespace Week14.Combat
{
    [AddComponentMenu("")]
    public sealed class PlayerOnlyMovementBarrier : MonoBehaviour
    {
    }

    public static class GroundMovementConstraint
    {
        private const string GroundLayerName = "Ground";
        private const float DefaultProbeRadius = 0.12f;
        private const float MinProbeRadius = 0.01f;
        private const float WallCastSkin = 0.01f;
        private static Tilemap[] groundTilemaps;
        private static readonly RaycastHit2D[] wallCastHits = new RaycastHit2D[8];
        private static readonly RaycastHit2D[] playerBarrierCastHits = new RaycastHit2D[16];
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

        public static Vector2 ClampVelocityAgainstLayer(Rigidbody2D body, Vector2 velocity, int layerMask)
        {
            if (body == null || velocity.sqrMagnitude <= 0.0001f || layerMask == 0)
            {
                return velocity;
            }

            float stepSeconds = Mathf.Max(Time.fixedDeltaTime, Time.deltaTime);
            float castDistance = velocity.magnitude * Mathf.Max(0f, stepSeconds);
            if (castDistance <= 0f)
            {
                return velocity;
            }

            ContactFilter2D filter = new();
            filter.useLayerMask = true;
            filter.layerMask = layerMask;
            filter.useTriggers = false;

            int hitCount = body.Cast(velocity.normalized, filter, wallCastHits, castDistance + WallCastSkin);
            Vector2 constrainedVelocity = velocity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = wallCastHits[i];
                if (hit.collider == null)
                {
                    continue;
                }

                float intoWallSpeed = Vector2.Dot(constrainedVelocity, hit.normal);
                if (intoWallSpeed < 0f)
                {
                    constrainedVelocity -= hit.normal * intoWallSpeed;
                }
            }

            return constrainedVelocity;
        }

        public static Vector2 ClampVelocityAgainstPlayerOnlyBarriers(Rigidbody2D body, Vector2 velocity)
        {
            if (body == null || velocity.sqrMagnitude <= 0.0001f)
            {
                return velocity;
            }

            float stepSeconds = Mathf.Max(Time.fixedDeltaTime, Time.deltaTime);
            float castDistance = velocity.magnitude * Mathf.Max(0f, stepSeconds);
            if (castDistance <= 0f)
            {
                return velocity;
            }

            ContactFilter2D filter = new();
            filter.useTriggers = true;
            int hitCount = body.Cast(
                velocity.normalized,
                filter,
                playerBarrierCastHits,
                castDistance + WallCastSkin);
            Vector2 constrainedVelocity = velocity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = playerBarrierCastHits[i];
                if (hit.collider == null || hit.collider.GetComponent<PlayerOnlyMovementBarrier>() == null)
                {
                    continue;
                }

                float intoBarrierSpeed = Vector2.Dot(constrainedVelocity, hit.normal);
                if (intoBarrierSpeed < 0f)
                {
                    constrainedVelocity -= hit.normal * intoBarrierSpeed;
                }
            }

            return constrainedVelocity;
        }

        public static void PushPlayerOutOfMovingLine(
            Transform player,
            Vector2 previousStart,
            Vector2 previousEnd,
            Vector2 currentStart,
            Vector2 currentEnd,
            float padding)
        {
            if (player == null
                || !TryGetBodyAndColliders(player, out Rigidbody2D body, out Collider2D[] colliders)
                || !TryGetBounds(colliders, out Bounds playerBounds)
                || !TryGetMovingLineDisplacement(
                    playerBounds,
                    previousStart,
                    previousEnd,
                    currentStart,
                    currentEnd,
                    Mathf.Max(0f, padding),
                    out Vector2 displacement))
            {
                return;
            }

            body.position = ClampStep(body.position, body.position + displacement, colliders);
            RemoveVelocityInDirection(body, -displacement);
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

        private static bool TryGetBodyAndColliders(
            Transform target,
            out Rigidbody2D body,
            out Collider2D[] colliders)
        {
            body = target.GetComponentInParent<Rigidbody2D>();
            if (body == null)
            {
                body = target.GetComponentInChildren<Rigidbody2D>();
            }

            colliders = body != null ? body.GetComponentsInChildren<Collider2D>() : null;
            return body != null && colliders != null && colliders.Length > 0;
        }

        private static bool TryGetBounds(Collider2D[] colliders, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (colliders == null)
            {
                return false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetMovingLineDisplacement(
            Bounds playerBounds,
            Vector2 previousStart,
            Vector2 previousEnd,
            Vector2 currentStart,
            Vector2 currentEnd,
            float padding,
            out Vector2 displacement)
        {
            displacement = Vector2.zero;
            Vector2 lineDelta = currentEnd - currentStart;
            if (lineDelta.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            if (Mathf.Abs(lineDelta.x) >= Mathf.Abs(lineDelta.y))
            {
                float segmentMinX = Mathf.Min(currentStart.x, currentEnd.x);
                float segmentMaxX = Mathf.Max(currentStart.x, currentEnd.x);
                if (playerBounds.max.x < segmentMinX || playerBounds.min.x > segmentMaxX)
                {
                    return false;
                }

                float previousY = (previousStart.y + previousEnd.y) * 0.5f;
                float currentY = (currentStart.y + currentEnd.y) * 0.5f;
                if (currentY < previousY)
                {
                    if (playerBounds.max.y <= currentY - padding || playerBounds.min.y > previousY + padding)
                    {
                        return false;
                    }

                    displacement.y = currentY - padding - playerBounds.max.y;
                    return displacement.y < 0f;
                }

                if (currentY > previousY)
                {
                    if (playerBounds.min.y >= currentY + padding || playerBounds.max.y < previousY - padding)
                    {
                        return false;
                    }

                    displacement.y = currentY + padding - playerBounds.min.y;
                    return displacement.y > 0f;
                }

                return false;
            }

            float segmentMinY = Mathf.Min(currentStart.y, currentEnd.y);
            float segmentMaxY = Mathf.Max(currentStart.y, currentEnd.y);
            if (playerBounds.max.y < segmentMinY || playerBounds.min.y > segmentMaxY)
            {
                return false;
            }

            float previousX = (previousStart.x + previousEnd.x) * 0.5f;
            float currentX = (currentStart.x + currentEnd.x) * 0.5f;
            if (currentX < previousX)
            {
                if (playerBounds.max.x <= currentX - padding || playerBounds.min.x > previousX + padding)
                {
                    return false;
                }

                displacement.x = currentX - padding - playerBounds.max.x;
                return displacement.x < 0f;
            }

            if (currentX > previousX)
            {
                if (playerBounds.min.x >= currentX + padding || playerBounds.max.x < previousX - padding)
                {
                    return false;
                }

                displacement.x = currentX + padding - playerBounds.min.x;
                return displacement.x > 0f;
            }

            return false;
        }

        private static void RemoveVelocityInDirection(Rigidbody2D body, Vector2 direction)
        {
            if (body == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalizedDirection = direction.normalized;
            Vector2 velocity = body.linearVelocity;
            float speed = Vector2.Dot(velocity, normalizedDirection);
            if (speed > 0f)
            {
                body.linearVelocity = velocity - normalizedDirection * speed;
            }
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
