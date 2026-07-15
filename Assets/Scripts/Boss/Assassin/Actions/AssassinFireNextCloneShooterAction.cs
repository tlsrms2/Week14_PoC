using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class AssassinFireNextCloneShooterAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [Tooltip("발사한 것이 분신이었다면, 그 분신이 사라지는 데 걸리는 페이드 시간입니다. 보스 자신이 쐈다면 무시됩니다.")]
        [SerializeField, Min(0f)] private float despawnFadeSeconds = 0.15f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not AssassinBossAI assassin
                || !assassin.TryDequeueCloneShooter(out Vector3 origin, out AssassinClone clone))
            {
                yield break;
            }

            Vector2 direction = context.GetDirectionToPlayer(origin);
            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                origin,
                direction,
                0f,
                projectileName: projectileName);

            if (firedProjectile != null)
            {
                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                context.PlayOriginBurst(effects, origin);
                context.PlayMuzzleFlashIfEnabled(effects, origin, direction);
                context.PlayCameraShakeIfEnabled(effects, direction);
            }

            if (clone != null)
            {
                clone.PlayDespawn(despawnFadeSeconds);
            }
        }
    }
}
