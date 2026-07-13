using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerFireGlitchProjectileAction : BossAction, IBossActionDurationProvider
    {
        [Header("Projectile")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;

        [Header("Launch")]
        [SerializeField] private string animationTriggerName = "FireGlitch";
        [SerializeField, Min(0f)] private float windupSeconds = 0.3f;
        [SerializeField, Range(-180f, 180f)] private float upwardAngleDegrees = 90f;
        [SerializeField, Range(0f, 180f)] private float upwardRandomHalfAngleDegrees = 65f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            if (settings?.Prefab is not HackerGlitchProjectile)
            {
                Debug.LogWarning($"{nameof(HackerFireGlitchProjectileAction)} requires a {nameof(HackerGlitchProjectile)} prefab.");
                yield break;
            }

            context.PlayAnimationTrigger(animationTriggerName);
            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            float angleDegrees = upwardAngleDegrees + UnityEngine.Random.Range(
                -upwardRandomHalfAngleDegrees,
                upwardRandomHalfAngleDegrees);
            float radians = angleDegrees * Mathf.Deg2Rad;
            Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector3 spawnOrigin = originSpec.GetSpawnOrigin(context, 0, direction);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(direction * spawnForwardOffset);
            }

            EnemyProjectile firedProjectile = context.FireProjectile(
                settings,
                spawnOrigin,
                direction,
                0f,
                chargeSecondsOverride: 0f,
                projectileName: projectileName);
            if (firedProjectile == null)
            {
                yield break;
            }

            firedProjectile.ConfigureInterceptable(false);
            firedProjectile.ConfigurePathIndicatorSuppressed(true);
            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            context.PlayMuzzleFlashIfEnabled(effects, spawnOrigin, direction);
            context.PlayCameraShakeIfEnabled(effects, direction);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + Mathf.Max(0f, recoverySeconds);
            return true;
        }
    }
}
