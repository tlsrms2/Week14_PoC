using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
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
}
