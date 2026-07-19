using UnityEngine;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void TickRadialSplitDelay()
        {
            if (!splitRadiallyOnLaunch || resolved || isDestroying || radialSplitAt <= 0f)
            {
                return;
            }

            if (!radialSplitImminentFired && Time.time >= radialSplitAt - radialSplitSfxLeadSeconds)
            {
                radialSplitImminentFired = true;
                RadialSplitImminent?.Invoke(this);
            }

            if (Time.time < radialSplitAt)
            {
                return;
            }

            SplitRadiallyOnLaunch();
        }

        private bool TrySplitOnObstacle(Collider2D obstacle)
        {
            if (!splitOnObstacle || splitRemaining <= 0 || obstacle == null)
            {
                return false;
            }

            Vector2 normal = GetObstacleNormal(obstacle);
            Vector2 reflected = Vector2.Reflect(flightDirection, normal);
            if (reflected.sqrMagnitude <= 0.0001f)
            {
                reflected = -flightDirection;
            }

            ProjectileVfx.PlayHogSmokeBurst(transform.position, projectileColor, Mathf.Max(1f, projectileRadius * 2.6f), 20);
            SpawnSplitChild(RotateDirection(reflected, -splitAngleDegrees * 0.5f));
            SpawnSplitChild(RotateDirection(reflected, splitAngleDegrees * 0.5f));
            resolved = true;
            DestroyProjectile();
            return true;
        }

        private void SplitRadiallyOnLaunch()
        {
            int count = Mathf.Max(1, radialSplitBulletCount);
            float step = 360f / count;
            ProjectileVfx.PlayHogSmokeBurst(transform.position, projectileColor, Mathf.Max(1f, projectileRadius * 2.6f), count);
            RadialSplit?.Invoke(this);

            for (int i = 0; i < count; i++)
            {
                SpawnSplitChild(AngleToDirection(radialSplitStartAngleDegrees + step * i));
            }

            resolved = true;
            SetChargeVfxVisible(false);
            SetPathIndicatorVisible(false);
            DestroyProjectile();
        }

        private Vector2 GetObstacleNormal(Collider2D obstacle)
        {
            Vector2 position = transform.position;
            Vector2 closest = obstacle.ClosestPoint(position);
            Vector2 normal = position - closest;
            if (normal.sqrMagnitude <= 0.0001f)
            {
                normal = -flightDirection;
            }

            return normal.normalized;
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void SpawnSplitChild(Vector2 direction)
        {
            GetHomingSpawnConfig(
                out bool homingEnabled,
                out float homingSeconds,
                out float homingTurnDegrees);

            // 다른 프리팹을 지정한 경우, 그 프리팹 고유의 크기(자신의 BossProjectileSettings 반지름)를
            // 기준으로 삼는다. splitRadiusMultiplier는 원래 "분열 전 탄이 커진 만큼을 상쇄"하는 용도라
            // 자기복제(this)에만 의미가 있고, 남의 프리팹에 그대로 곱하면 의도치 않게 더 작아진다.
            bool usesPrefabOverride = radialSplitPrefabOverride != null;
            EnemyProjectile splitPrefab = usesPrefabOverride ? radialSplitPrefabOverride : this;
            float baseRadius = usesPrefabOverride && radialSplitBaseRadiusOverride > 0f
                ? radialSplitBaseRadiusOverride
                : projectileRadius;

            EnemyProjectile child = SpawnInternal(
                splitPrefab,
                ownerBullets,
                transform.position + (Vector3)(direction.normalized * Mathf.Max(0.08f, projectileRadius)),
                direction,
                bulletDamage,
                0f,
                projectileSpeed * splitSpeedMultiplier,
                projectileLifetime * splitLifetimeMultiplier,
                usesPrefabOverride ? baseRadius : baseRadius * splitRadiusMultiplier,
                projectileColor,
                0.08f,
                3f,
                homingEnabled,
                homingSeconds,
                homingTurnDegrees,
                suppressPathIndicator: true);

            if (child == null)
            {
                return;
            }

            child.ConfigureObstacleSplit(
                splitRemaining - 1,
                splitAngleDegrees,
                splitSpeedMultiplier,
                splitRadiusMultiplier,
                splitLifetimeMultiplier);

            if (usesPrefabOverride)
            {
                child.ConfigureProjectileSize(baseRadius);
            }
            else
            {
                child.ConfigureProjectileSize(baseRadius * splitRadiusMultiplier);
                child.MultiplyProjectileScale(splitRadiusMultiplier);
            }
        }

        private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector2.left;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 normalized = direction.normalized;
            return new Vector2(
                normalized.x * cos - normalized.y * sin,
                normalized.x * sin + normalized.y * cos);
        }

    }
}
