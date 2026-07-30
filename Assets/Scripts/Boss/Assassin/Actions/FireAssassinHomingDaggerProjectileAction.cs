using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
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
        [SerializeField, Tooltip("0 이상이면 Projectile Settings의 Charge Seconds 대신 이 값을 사용합니다. 음수(-1)면 오버라이드하지 않습니다.")] private float chargeSecondsOverride = -1f;
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
                chargeSecondsOverride: chargeSecondsOverride,
                projectileName: projectileName);

            if (daggerProjectile == null)
            {
                yield break;
            }

            context.PlaySfx(SoundEvent.Assassin_Fire);
            context.PlaySfxOnLaunch(daggerProjectile, SoundEvent.Assassin_Launch);
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
