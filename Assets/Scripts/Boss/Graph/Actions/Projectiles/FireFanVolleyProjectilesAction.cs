using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireFanEmissionAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(1)] private int volleyCount = 1;
        [SerializeField, Min(1)] private int projectilesPerVolley = 3;
        [SerializeField, Min(0f)] private float volleyInterval = 0.18f;
        [SerializeField, Range(0f, 180f)] private float fanAngleDegrees = 42f;
        [SerializeField, Min(0f)] private float spawnSpacing = 0.16f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            int count = Mathf.Max(1, volleyCount);
            for (int volleyIndex = 0; volleyIndex < count; volleyIndex++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    volleyIndex--;
                    continue;
                }

                FireVolley(context, originSpec, aimSpec, volleyIndex);

                if (volleyInterval > 0f && volleyIndex < count - 1)
                {
                    yield return context.WaitSeconds(volleyInterval);
                }
            }
        }

        private void FireVolley(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int volleyIndex)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, volleyIndex);
            Vector2 lockedDirection = aimSpec.GetDirection(context, aimOrigin);
            float baseAngle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
            Vector2 side = new(-lockedDirection.y, lockedDirection.x);
            int count = Mathf.Max(1, projectilesPerVolley);
            float centerIndex = (count - 1) * 0.5f;
            bool firedAny = false;

            for (int i = 0; i < count; i++)
            {
                float normalizedIndex = count <= 1 ? 0f : i / (count - 1f) - 0.5f;
                float offsetIndex = i - centerIndex;
                Vector2 direction = BossActionContext.AngleToDirection(baseAngle + fanAngleDegrees * normalizedIndex);
                Vector3 originCenter = originSpec.GetSpawnOrigin(context, volleyIndex * count + i, direction);
                Vector3 spawnPosition = originCenter + (Vector3)(side * spawnSpacing * offsetIndex);
                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    spawnPosition,
                    direction,
                    0f,
                    projectileName: projectileName);
                if (firedProjectile != null)
                {
                    firedAny = true;
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                }
            }

            if (!firedAny)
            {
                return;
            }

            context.PlayMuzzleFlashIfEnabled(effects, aimOrigin, lockedDirection);
            context.PlayCameraShakeIfEnabled(effects, lockedDirection);
            context.PlaySfx(fireSfxId);
        }
    }
}
