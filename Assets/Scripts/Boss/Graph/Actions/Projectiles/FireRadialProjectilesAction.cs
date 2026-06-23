using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    [Obsolete("Use FireRadialEmissionAction instead.")]
    public sealed class FireRadialProjectilesAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, Min(1)] private int bulletCount = 8;
        [SerializeField] private bool centerOnPlayer;
        [SerializeField, Range(0f, 360f)] private float arcDegrees = 360f;
        [SerializeField, Min(0f)] private float spawnRadius;
        [SerializeField, Min(0f)] private float fireInterval;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField] private Vector2 cameraShakeDirection = Vector2.down;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            int count = Mathf.Max(1, bulletCount);
            Vector3 originCenter = context.OriginPosition;
            Vector2 playerDirection = context.GetDirectionToPlayer(originCenter);
            float baseAngle = centerOnPlayer
                ? Mathf.Atan2(playerDirection.y, playerDirection.x) * Mathf.Rad2Deg
                : 0f;
            float step = GetAngleStep(count);
            float firstAngle = arcDegrees >= 360f
                ? baseAngle
                : baseAngle - arcDegrees * 0.5f;

            context.PlayOriginBurst(effects, originCenter);
            context.PlayCameraShakeIfEnabled(effects, cameraShakeDirection);
            context.PlaySfx(fireSfxId);

            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector2 direction = BossActionContext.AngleToDirection(firstAngle + step * i);
                Vector3 origin = spawnRadius > 0f
                    ? originCenter + (Vector3)(direction * spawnRadius)
                    : originCenter;
                context.FireProjectile(projectile, origin, direction, 0.9f, projectileName: projectileName);

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }

        private float GetAngleStep(int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return arcDegrees >= 360f
                ? 360f / count
                : arcDegrees / (count - 1);
        }
    }

    [Serializable]
    public sealed class FireRadialEmissionAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(1)] private int bulletCount = 8;
        [SerializeField, Range(0f, 360f)] private float arcDegrees = 360f;
        [SerializeField] private float startAngleOffset;
        [SerializeField] private bool randomizeStartAngle;
        [SerializeField, Min(0f)] private float spawnRadius;
        [SerializeField, Min(0f)] private float fireInterval;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField] private Vector2 cameraShakeDirection = Vector2.down;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            int count = Mathf.Max(1, bulletCount);
            float step = GetAngleStep(count);
            float angleOffset = randomizeStartAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : startAngleOffset;

            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector3 center = originSpec.GetAimOrigin(context, i);
                Vector2 centerDirection = aimSpec.GetDirection(context, center);
                float baseAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg;
                float firstAngle = arcDegrees >= 360f
                    ? baseAngle + angleOffset
                    : baseAngle - arcDegrees * 0.5f + angleOffset;
                Vector2 direction = BossActionContext.AngleToDirection(firstAngle + step * i);
                Vector3 spawnOrigin = spawnRadius > 0f
                    ? center + (Vector3)(direction * spawnRadius)
                    : center;

                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    spawnOrigin,
                    direction,
                    0.9f,
                    projectileName: projectileName);

                if (firedProjectile != null)
                {
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                    context.PlayOriginBurst(effects, spawnOrigin);
                    context.PlayMuzzleFlashIfEnabled(effects, spawnOrigin, direction);
                    context.PlayCameraShakeIfEnabled(effects, cameraShakeDirection);
                }

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }

        private float GetAngleStep(int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return arcDegrees >= 360f
                ? 360f / count
                : arcDegrees / (count - 1);
        }
    }
}
