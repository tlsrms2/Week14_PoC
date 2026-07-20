using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal static class HackerWireFireVfx
    {
        internal static void Play(
            GameObject prefab,
            Transform spawnPoint,
            Vector2 wireDirection,
            float rotationOffsetDegrees,
            float scale)
        {
            if (prefab == null || spawnPoint == null)
            {
                return;
            }

            Vector2 direction = wireDirection.sqrMagnitude > 0.0001f
                ? wireDirection.normalized
                : Vector2.right;
            float angleDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
                + rotationOffsetDegrees;
            ProjectileVfx.PlayPrefab(
                prefab,
                spawnPoint.position,
                Quaternion.Euler(0f, 0f, angleDegrees),
                null,
                Mathf.Max(0.01f, scale),
                false);
        }
    }
}
