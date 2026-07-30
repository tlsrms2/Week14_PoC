using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class SpawnParryableBombAction : BossAction, IBossProjectileEmissionAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [SerializeField, Min(0f)] private float chargeSeconds = 1.5f;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [Tooltip("폭탄이 패링당하지 않고 그대로 터졌을 때 켤 Animator Bool 이름입니다.")]
        [SerializeField] private string slamSuccessBoolName = "isSlam";
        [Tooltip("폭탄이 패링당했을 때 켤 Animator Bool 이름입니다.")]
        [SerializeField] private string slamFailBoolName = "isSlamFail";
        [Tooltip("폭발 전 대기할 Animation Event 이름입니다. Slam 애니메이션의 충돌 프레임에서 발생시켜야 폭발과 모션이 맞아떨어집니다. 비워두면 대기 없이 즉시 터집니다.")]
        [SerializeField] private string impactEventId = "SlamImpact";
        [SerializeField, Min(0f)] private float impactEventTimeoutSeconds = 2f;
        [Tooltip("켜면 폭탄이 차징(대기)하는 동안 보스 몸에 붙어서 함께 이동합니다. 돌진(BossDashAction) 등 이동 패턴과 겹쳐서 실행하면 탄이 보스를 따라 날아갑니다.")]
        [SerializeField] private bool attachToBossDuringCharge;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            Vector3 spawnOrigin = originSpec.GetAimOrigin(context, 0);
            Vector2 direction = aimSpec.GetDirection(context, spawnOrigin);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(direction.normalized * spawnForwardOffset);
            }

            EnemyProjectile bomb = context.FireProjectile(
                projectile,
                spawnOrigin,
                direction,
                0f,
                aimAtPlayerWhileChargingOverride: false,
                aimAtPlayerOnLaunchOverride: false,
                chargeSecondsOverride: chargeSeconds,
                projectileName: projectileName);

            if (bomb == null)
            {
                yield break;
            }

            if (attachToBossDuringCharge && context.Boss != null)
            {
                Transform bossAnchor = context.Boss.BodyRoot != null ? context.Boss.BodyRoot : context.Boss.transform;
                bomb.ConfigureChargeAnchor(bossAnchor);
                bomb.ConfigureChargeMotion(0f, false, false);
            }

            context.PlaySfx(SoundEvent.Boss_ParryBombSpawn);
            context.PlayOriginBurst(effects, spawnOrigin);

            bool wasParried = false;
            bool isResolved = false;
            bool hasExplosionConfig = false;
            Vector3 explosionCenter = Vector3.zero;
            float explosionRadius = 0f;
            int explosionDamage = 0;

            void OnBombDestroyed(EnemyProjectile destroyedProjectile, EnemyProjectileDestroyReason reason, Vector3 __)
            {
                wasParried = reason == EnemyProjectileDestroyReason.Intercepted;
                if (!wasParried && destroyedProjectile is ParryTimedBomb parryBomb)
                {
                    explosionCenter = parryBomb.ExplosionCenter;
                    explosionRadius = parryBomb.ExplosionRadius;
                    explosionDamage = parryBomb.ExplosionDamage;
                    hasExplosionConfig = true;
                }

                isResolved = true;
            }

            bomb.Destroyed += OnBombDestroyed;

            while (!isResolved)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                }

                yield return null;
            }

            bomb.Destroyed -= OnBombDestroyed;
            context.SetAnimationBool(wasParried ? slamFailBoolName : slamSuccessBoolName, true);

            if (wasParried || !hasExplosionConfig)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(impactEventId))
            {
                yield return context.WaitForAnimationEvent(impactEventId, impactEventTimeoutSeconds);
            }

            if (explosionDamage <= 0)
            {
                yield break;
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController player = hits[i].GetComponentInParent<PlayerCombatController>();
                if (player == null)
                {
                    continue;
                }

                Vector2 hitDirection = (Vector2)(player.transform.position - explosionCenter);
                player.ReceiveAttack(explosionDamage, explosionCenter, hitDirection);
                break;
            }
        }
    }
}
