using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogProjectileOriginResolver
    {
        public static Vector3 GetPattern1SpawnPosition(
            HogBossAI.Pattern1Settings pattern1,
            int shotIndex,
            Vector2 direction,
            Vector3 defaultOrigin)
        {
            if (pattern1.ProjectileOrigins != null && pattern1.ProjectileOrigins.HasAny)
            {
                return GetAlternatingPosition(pattern1.ProjectileOrigins, shotIndex, defaultOrigin);
            }

            float radius = Mathf.Max(0f, pattern1.SpawnRadius);
            if (radius <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return defaultOrigin;
            }

            return defaultOrigin + (Vector3)(direction.normalized * radius);
        }

        public static Vector3 GetAlternatingPosition(
            HogBossAI.AlternatingProjectileOrigins origins,
            int shotIndex,
            Vector3 defaultOrigin)
        {
            Transform origin = origins != null ? origins.Get(shotIndex) : null;
            return origin != null ? origin.position : defaultOrigin;
        }

        public static Vector3 GetFirePointPosition(
            HogBossAI.FirePoint firePoint,
            Vector3 defaultOrigin)
        {
            return HogFirePointUtility.GetProjectilePosition(firePoint, defaultOrigin);
        }

        public static Vector3 GetPattern7NormalPosition(
            HogBossAI.Pattern7Settings pattern7,
            Vector3 defaultOrigin)
        {
            return GetFirePointPosition(pattern7.FirePoint, defaultOrigin);
        }

        public static Vector3 GetPattern7SpecialPosition(
            HogBossAI.Pattern7Settings pattern7,
            int index,
            Vector3 defaultOrigin)
        {
            IReadOnlyList<Transform> origins = pattern7.SpecialProjectileOrigins;
            Transform origin = origins != null && index >= 0 && index < origins.Count ? origins[index] : null;
            return origin != null ? origin.position : GetFirePointPosition(pattern7.FirePoint, defaultOrigin);
        }

        public static Vector3 GetPattern4Position(
            HogBossAI.Pattern4Settings settings,
            Vector3 defaultOrigin)
        {
            return settings.ProjectileOrigin != null ? settings.ProjectileOrigin.position : defaultOrigin;
        }
    }
}
