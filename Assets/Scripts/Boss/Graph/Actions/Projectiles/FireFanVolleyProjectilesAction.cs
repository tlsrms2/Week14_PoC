using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireFanVolleyProjectilesAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string normalProjectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings normalProjectile = new();
        [SerializeField, BossGraphProjectileName] private string secondaryProjectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings secondaryProjectile = new();
        [SerializeField, BossGraphBossChildPath] private string projectileOriginPath;
        [SerializeField, BossGraphBossChildPath] private List<string> secondaryOriginPaths = new();
        [SerializeField, Min(0f)] private float windupSeconds = 1f;
        [SerializeField, Min(1)] private int normalVolleyCount = 3;
        [SerializeField, Min(0f)] private float normalVolleyInterval = 0.18f;
        [SerializeField, Min(0)] private int secondaryBulletCount = 1;
        [SerializeField, Range(0f, 180f)] private float fanAngleDegrees = 42f;
        [SerializeField, Min(0f)] private float normalSpawnSpacing = 0.16f;
        [SerializeField, Min(0f)] private float secondarySpawnForwardOffset;
        [SerializeField, BossGraphSfxId] private string normalVolleySfxId;
        [SerializeField, BossGraphSfxId] private string secondaryFireSfxId;
        [SerializeField, BossGraphSfxId] private string secondaryLaunchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return RunWindup(context);

            Vector3 aimOrigin = context.GetBossChildPosition(projectileOriginPath);
            Vector2 lockedDirection = context.GetDirectionToPlayer(aimOrigin);

            int volleyCount = Mathf.Max(1, normalVolleyCount);
            for (int volleyIndex = 0; volleyIndex < volleyCount; volleyIndex++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    volleyIndex--;
                    continue;
                }

                context.Stop();
                FireNormalVolley(context, lockedDirection);
                if (volleyIndex == 0)
                {
                    FireSecondaryProjectiles(context, lockedDirection);
                }

                if (normalVolleyInterval > 0f && volleyIndex < volleyCount - 1)
                {
                    yield return context.WaitSeconds(normalVolleyInterval);
                }
            }

        }

        private IEnumerator RunWindup(BossActionContext context)
        {
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            while (elapsed < windupSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.Stop();
                Vector3 origin = context.GetBossChildPosition(projectileOriginPath);
                context.PlaySmokeIfDue(ref nextSmokeAt, effects, origin);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void FireNormalVolley(BossActionContext context, Vector2 lockedDirection)
        {
            Vector3 origin = context.GetBossChildPosition(projectileOriginPath);
            float baseAngle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
            float halfFanAngle = fanAngleDegrees * 0.5f;
            Vector2 side = new(-lockedDirection.y, lockedDirection.x);
            bool firedAny = false;

            for (int i = 0; i < 3; i++)
            {
                float offsetIndex = i - 1f;
                Vector2 direction = BossActionContext.AngleToDirection(baseAngle + halfFanAngle * offsetIndex);
                Vector3 spawnPosition = origin + (Vector3)(side * normalSpawnSpacing * offsetIndex);
                EnemyProjectile projectile = context.FireProjectile(
                    normalProjectile,
                    spawnPosition,
                    direction,
                    0f,
                    projectileName: normalProjectileName);
                firedAny |= projectile != null;
            }

            if (firedAny)
            {
                context.PlayMuzzleFlashIfEnabled(effects, origin, lockedDirection);
                context.PlayCameraShakeIfEnabled(effects, lockedDirection);
                context.PlaySfx(normalVolleySfxId);
            }
        }

        private void FireSecondaryProjectiles(BossActionContext context, Vector2 lockedDirection)
        {
            int count = Mathf.Max(0, secondaryBulletCount);
            bool firedAny = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = GetSecondaryOrigin(context, i);
                Vector3 spawnPosition = origin + (Vector3)(lockedDirection.normalized * secondarySpawnForwardOffset);
                EnemyProjectile projectile = context.FireProjectile(
                    secondaryProjectile,
                    spawnPosition,
                    lockedDirection,
                    0f,
                    projectileName: secondaryProjectileName);
                if (projectile != null)
                {
                    firedAny = true;
                    context.PlaySfxOnLaunch(projectile, secondaryLaunchSfxId);
                }
            }

            if (firedAny)
            {
                context.PlaySfx(secondaryFireSfxId);
            }
        }

        private Vector3 GetSecondaryOrigin(BossActionContext context, int index)
        {
            if (secondaryOriginPaths != null
                && index >= 0
                && index < secondaryOriginPaths.Count
                && !string.IsNullOrWhiteSpace(secondaryOriginPaths[index]))
            {
                return context.GetBossChildPosition(secondaryOriginPaths[index]);
            }

            return context.GetBossChildPosition(projectileOriginPath);
        }
    }
}
