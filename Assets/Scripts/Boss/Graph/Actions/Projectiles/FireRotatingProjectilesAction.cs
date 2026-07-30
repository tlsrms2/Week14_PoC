using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireRotatingProjectilesAction : BossAction, IBossProjectileEmissionAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(1)] private int bulletCount = 12;
        [SerializeField, Min(0f)] private float fireInterval = 0.04f;
        [SerializeField, Min(0f)] private float startRadius;
        [SerializeField, Min(1)] private int ringCount = 1;
        [SerializeField, Min(0f)] private float ringSpacing;
        [SerializeField, Min(0f)] private float radialSpeedMultiplier = 1f;
        [SerializeField] private float angularSpeedDegrees = 540f;
        [SerializeField] private float startAngleOffset;
        [SerializeField] private bool randomizeStartAngle;
        [SerializeField, Min(0f)] private float motionDurationSeconds;
        [SerializeField] private bool destroyOnMotionEnd;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            int count = Mathf.Max(1, bulletCount);
            int rings = Mathf.Max(1, ringCount);
            float baseStartAngle = randomizeStartAngle ? UnityEngine.Random.Range(0f, 360f) : startAngleOffset;

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
                Vector2 aimDirection = aimSpec.GetDirection(context, center);
                float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                float spiralAngle = aimAngle + baseStartAngle + 360f / count * i;
                Vector2 spawnDirection = BossActionContext.AngleToDirection(spiralAngle);
                for (int ringIndex = 0; ringIndex < rings; ringIndex++)
                {
                    float ringStartRadius = startRadius + Mathf.Max(0f, ringSpacing) * ringIndex;
                    Vector3 spawnPosition = center + (Vector3)(spawnDirection * ringStartRadius);
                    EnemyProjectile firedProjectile = context.FireProjectile(
                        projectile,
                        spawnPosition,
                        spawnDirection,
                        0f,
                        aimAtPlayerWhileChargingOverride: false,
                        aimAtPlayerOnLaunchOverride: false,
                        chargeSecondsOverride: 0f,
                        suppressHoming: true,
                        projectileName: projectileName);

                    if (firedProjectile != null)
                    {
                        firedProjectile.ConfigurePathIndicatorSuppressed(true);
                        firedProjectile.gameObject.AddComponent<BossSpiralProjectileMotion>().Initialize(
                            center,
                            spiralAngle,
                            (settings?.Speed ?? 0f) * radialSpeedMultiplier,
                            angularSpeedDegrees,
                            ringStartRadius,
                            motionDurationSeconds,
                            destroyOnMotionEnd);
                        context.PlaySfx(SoundEvent.Boss_ProjectileFire);
                        context.PlaySfxOnLaunch(firedProjectile, SoundEvent.Boss_ProjectileLaunch);
                        context.PlayOriginBurst(effects, spawnPosition);
                        context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, spawnDirection);
                        context.PlayCameraShakeIfEnabled(effects, spawnDirection);
                    }
                }

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }
    }
}
