using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireSweepProjectilesAction : BossAction
    {
        [SerializeField] private BossProjectileSettings projectile = new();
        [SerializeField] private string firePointPath;
        [SerializeField] private string projectileOriginPath;
        [SerializeField] private bool setFirePointActive = true;
        [SerializeField, Min(0f)] private float windupSeconds = 1.4f;
        [SerializeField, Min(1)] private int bulletCount = 36;
        [SerializeField, Min(0f)] private float fireInterval = 0.045f;
        [SerializeField, Min(0f)] private float spawnSpacing = 0.12f;
        [SerializeField, Min(0f)] private float sweepStepDegrees = 5f;
        [SerializeField, Min(0f)] private float maxSweepAngle = 35f;
        [SerializeField, Min(0f)] private float muzzleFlashScale;
        [SerializeField] private string fireSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (setFirePointActive)
            {
                context.SetBossChildActive(firePointPath, true);
            }

            yield return RunWindup(context);
            yield return RunSweepFire(context);

            if (setFirePointActive)
            {
                context.SetBossChildActive(firePointPath, false);
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
                AimFirePointToPlayer(context);
                context.PlaySmokeIfDue(ref nextSmokeAt, effects, context.GetBossChildPosition(projectileOriginPath));
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator RunSweepFire(BossActionContext context)
        {
            int count = Mathf.Max(1, bulletCount);
            float currentSweepOffset = 0f;
            float sweepDirection = 1f;

            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                context.Stop();
                AimFirePointToPlayer(context);

                Vector3 origin = context.GetBossChildPosition(projectileOriginPath);
                Vector2 baseDirection = context.GetDirectionToPlayer(origin);
                float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
                float finalAngle = baseAngle + currentSweepOffset;
                Vector2 finalDirection = BossActionContext.AngleToDirection(finalAngle);

                context.RotateBossChildRight(firePointPath, finalDirection, true);
                origin = context.GetBossChildPosition(projectileOriginPath);

                Vector2 side = new(-finalDirection.y, finalDirection.x);
                Vector3 spawnPosition = origin + (Vector3)(side * GetAlternatingOffset(i));
                EnemyProjectile firedProjectile = context.FireProjectile(projectile, spawnPosition, finalDirection, muzzleFlashScale, false, false, 0f);
                if (firedProjectile != null)
                {
                    context.PlayMuzzleFlashIfEnabled(effects, origin, finalDirection);
                    context.PlayCameraShakeIfEnabled(effects, finalDirection);
                    context.PlaySfx(fireSfxId);
                }

                currentSweepOffset += sweepStepDegrees * sweepDirection;
                if (Mathf.Abs(currentSweepOffset) >= maxSweepAngle)
                {
                    sweepDirection *= -1f;
                    currentSweepOffset = Mathf.Sign(currentSweepOffset) * maxSweepAngle;
                }

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }

        private void AimFirePointToPlayer(BossActionContext context)
        {
            Vector3 origin = context.GetBossChildPosition(projectileOriginPath);
            context.RotateBossChildRight(firePointPath, context.GetDirectionToPlayer(origin), true);
        }

        private float GetAlternatingOffset(int index)
        {
            if (index <= 0 || spawnSpacing <= 0f)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 0 ? -1f : 1f;
            return ring * spawnSpacing * sign;
        }
    }
}
