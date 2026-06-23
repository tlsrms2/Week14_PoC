using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum BossGraphStartAngleMode
    {
        Fixed,
        Random,
        PlayerDirection
    }

    [Serializable]
    public sealed class FireRotatingProjectilesAction : BossAction
    {
        [SerializeField] private BossProjectileSettings projectile = new();
        [SerializeField, Min(1)] private int bulletCount = 8;
        [SerializeField] private BossGraphStartAngleMode startAngleMode = BossGraphStartAngleMode.Random;
        [SerializeField] private float startAngleDegrees;
        [SerializeField] private float angleStepDegrees = 45f;
        [SerializeField] private Vector2 originOffset;
        [SerializeField, Min(0f)] private float spawnRadius;
        [SerializeField, Min(0f)] private float fireInterval = 0.1f;
        [SerializeField, Min(0f)] private float muzzleFlashScale = 0.9f;
        [SerializeField] private bool overrideAimAtPlayerOnLaunch = true;
        [SerializeField] private bool aimAtPlayerOnLaunch = true;
        [SerializeField] private string fireSfxId;
        [SerializeField] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            int count = Mathf.Max(1, bulletCount);
            Vector3 center = context.OriginPosition + (Vector3)originOffset;
            float currentAngle = ResolveStartAngle(context, center);
            bool? aimOnLaunch = overrideAimAtPlayerOnLaunch ? aimAtPlayerOnLaunch : (bool?)null;

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
                Vector3 origin = spawnRadius > 0f
                    ? center + (Vector3)(direction.normalized * spawnRadius)
                    : center;

                EnemyProjectile firedProjectile = context.FireProjectile(projectile, origin, direction, muzzleFlashScale, null, aimOnLaunch);
                if (firedProjectile != null)
                {
                    context.PlayOriginBurst(effects, origin);
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                }

                currentAngle += angleStepDegrees;

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }

        private float ResolveStartAngle(BossActionContext context, Vector3 center)
        {
            return startAngleMode switch
            {
                BossGraphStartAngleMode.PlayerDirection => ToAngle(context.GetDirectionToPlayer(center)) + startAngleDegrees,
                BossGraphStartAngleMode.Random => UnityEngine.Random.Range(0f, 360f) + startAngleDegrees,
                _ => startAngleDegrees
            };
        }

        private static float ToAngle(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    }
}
