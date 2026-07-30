using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerThrustPerpendicularFireAction : HackerThrustAction
    {
        [Header("Perpendicular Projectile")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private float chargeSecondsOverride = -1f;
        [SerializeField] private BossGraphEffectSettings effects = new();

        [Header("Projectile Windup")]
        [SerializeField, Min(0f)] private float projectileWindupSeconds = 0.2f;

        [Header("Perpendicular Fire")]
        [FormerlySerializedAs("projectilesPerSide")]
        [SerializeField, Min(1)] private int projectilePairCount = 3;
        [FormerlySerializedAs("pairSpacing")]
        [SerializeField, Min(0.01f)] private float launchPointSpacing = 0.65f;
        [FormerlySerializedAs("centerLineProgress")]
        [SerializeField, Range(0f, 1f)] private float firstLaunchPointProgress = 0.25f;
        [SerializeField, Min(0f)] private float pairInterval;

        protected override IEnumerator CreateThrustPayload(BossActionContext context, Vector2 direction)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            if (settings?.Prefab == null)
            {
                yield break;
            }

            float windupElapsed = 0f;
            while (windupElapsed < projectileWindupSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                windupElapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            Vector2 perpendicular = new(-direction.y, direction.x);
            int count = Mathf.Max(1, projectilePairCount);
            Vector2 firstLaunchPoint = (Vector2)context.Boss.transform.position
                + direction * (ThrustLength * firstLaunchPointProgress);
            for (int index = 0; index < count; index++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    index--;
                    continue;
                }

                Vector2 launchPoint = firstLaunchPoint + direction * (launchPointSpacing * index);
                FireProjectile(context, settings, launchPoint, perpendicular);
                FireProjectile(context, settings, launchPoint, -perpendicular);

                if (index < count - 1)
                {
                    float elapsed = 0f;
                    while (elapsed < pairInterval)
                    {
                        if (context.IsExecutionPaused)
                        {
                            context.Stop();
                            yield return null;
                            continue;
                        }

                        elapsed += EnemyTimeScale.DeltaTime;
                        yield return null;
                    }
                }
            }
        }

        private void FireProjectile(
            BossActionContext context,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            EnemyProjectile firedProjectile = context.FireProjectile(
                settings,
                origin,
                direction,
                0f,
                aimAtPlayerWhileChargingOverride: false,
                aimAtPlayerOnLaunchOverride: false,
                chargeSecondsOverride: chargeSecondsOverride,
                suppressHoming: true,
                projectileName: projectileName);
            if (firedProjectile == null)
            {
                return;
            }

            context.PlaySfx(SoundEvent.Hacker_Fire);
            context.PlaySfxOnLaunch(firedProjectile, SoundEvent.Hacker_Launch);
            context.PlayOriginBurst(effects, origin);
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, direction);
            context.PlayCameraShakeIfEnabled(effects, direction);
        }
    }
}
