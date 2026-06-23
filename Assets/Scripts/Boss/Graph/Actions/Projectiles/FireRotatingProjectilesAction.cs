using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    [Obsolete("Use FireRadialEmissionAction instead.")]
    public sealed class FireRotatingProjectilesAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, BossGraphBossChildPath] private string firstProjectileOriginPath;
        [SerializeField, BossGraphBossChildPath] private string secondProjectileOriginPath;
        [SerializeField, Min(1)] private int bulletCount = 8;
        [SerializeField, Min(0f)] private float spawnRadius;
        [SerializeField, Min(0f)] private float fireInterval = 0.1f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            int count = Mathf.Max(1, bulletCount);
            float currentAngle = UnityEngine.Random.Range(0f, 360f);
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector2 direction = BossActionContext.AngleToDirection(currentAngle);
                Vector3 center = GetOriginCenter(context, i);
                Vector3 origin = spawnRadius > 0f
                    ? center + (Vector3)(direction.normalized * spawnRadius)
                    : center;

                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    origin,
                    direction,
                    0.9f,
                    projectileName: projectileName);
                if (firedProjectile != null)
                {
                    context.PlayOriginBurst(effects, origin);
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                }

                currentAngle += angleStep;

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }

        private Vector3 GetOriginCenter(BossActionContext context, int bulletIndex)
        {
            if (!HasConfiguredOrigin())
            {
                return context.OriginPosition;
            }

            return context.GetBossChildPosition(GetAlternatingOriginPath(bulletIndex));
        }

        private string GetAlternatingOriginPath(int bulletIndex)
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(firstProjectileOriginPath);
            bool hasSecond = !string.IsNullOrWhiteSpace(secondProjectileOriginPath);
            if (!hasFirst)
            {
                return secondProjectileOriginPath;
            }

            if (!hasSecond)
            {
                return firstProjectileOriginPath;
            }

            return bulletIndex % 2 == 0 ? firstProjectileOriginPath : secondProjectileOriginPath;
        }

        private bool HasConfiguredOrigin()
        {
            return !string.IsNullOrWhiteSpace(firstProjectileOriginPath)
                || !string.IsNullOrWhiteSpace(secondProjectileOriginPath);
        }
    }
}
