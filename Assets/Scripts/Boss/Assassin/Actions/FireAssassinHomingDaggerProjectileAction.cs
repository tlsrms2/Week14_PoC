using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireAssassinHomingDaggerProjectileAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
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
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, 0);
            Vector2 direction = aimSpec.GetDirection(context, aimOrigin);
            Vector3 spawnOrigin = originSpec.GetSpawnOrigin(context, 0, direction);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(direction.normalized * spawnForwardOffset);
            }

            EnemyProjectile daggerProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                direction,
                0f,
                projectileName: projectileName);

            if (daggerProjectile == null)
            {
                yield break;
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(daggerProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);

            if (context.Boss is not AssassinBossAI assassin)
            {
                yield break;
            }

            // 패링(Intercepted)당한 위치에 단검을 남긴다. 패링 외의 사유(수명 만료 등)로는 아무 것도 하지 않는다.
            daggerProjectile.Destroyed += (_, reason, position) =>
            {
                if (reason == EnemyProjectileDestroyReason.Intercepted)
                {
                    assassin.CreateDagger(position);
                }
            };
        }
    }
}
