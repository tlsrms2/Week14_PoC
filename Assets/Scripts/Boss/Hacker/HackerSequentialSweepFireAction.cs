using System;
using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerSequentialSweepFireAction : BossAction, IBossActionDurationProvider
    {
        private const string ScatterAnimationTrigger = "Scatter";

        [Header("Projectile")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, Tooltip("0 이상이면 Projectile Settings의 Charge Seconds 대신 이 값을 사용합니다. 음수(-1)면 오버라이드하지 않습니다.")]
        private float chargeSecondsOverride = -1f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        [Header("Sweep")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.35f;
        [SerializeField, Min(1)] private int bulletCount = 8;
        [SerializeField, Min(0.01f)] private float spawnCircleRadius = 2.5f;
        [SerializeField, Range(0f, 360f)] private float spawnArcDegrees = 360f;
        [SerializeField] private bool clockwise;
        [SerializeField] private bool trackPlayerPerShot = true;
        [SerializeField, Min(0f)] private float fireInterval = 0.1f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            string resolvedProjectileName = projectileName?.Trim() ?? string.Empty;
            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(resolvedProjectileName);
            if (settings == null)
            {
                settings = projectile;
            }

            if (settings?.Prefab == null)
            {
                yield break;
            }

            if (context.IsMeleeAdvanceSynchronized)
            {
                yield return WaitForMeleeAttackAdvanceCompletion(context);
            }

            Vector2 initialFacingDirection = GetDirectionToPlayer(context, context.Boss.transform.position);
            if (context.Boss is HackerBossAI hacker)
            {
                hacker.FaceHorizontalDirection(initialFacingDirection.x);
            }

            using IDisposable facingLock = context.AcquireFacingLock();
            if (!context.IsExecutingParallelPatternGroup)
            {
                context.PlayAnimationTrigger(ScatterAnimationTrigger);
            }

            yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

            int count = Mathf.Max(1, bulletCount);
            float halfArcAngle = Mathf.Max(0f, spawnArcDegrees) * 0.5f;
            bool isFullCircle = spawnArcDegrees >= 359.99f;
            Vector2 lockedPlayerDirection = GetDirectionToPlayer(context, context.Boss.transform.position);
            for (int index = 0; index < count; index++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    index--;
                    continue;
                }

                float progress = count <= 1
                    ? 0.5f
                    : isFullCircle
                        ? index / (float)count
                        : index / (float)(count - 1);
                float sweepOffset = Mathf.Lerp(-halfArcAngle, halfArcAngle, progress);
                if (clockwise)
                {
                    sweepOffset = -sweepOffset;
                }

                Vector2 playerDirection = trackPlayerPerShot
                    ? GetDirectionToPlayer(context, context.Boss.transform.position)
                    : lockedPlayerDirection;
                Vector2 spawnDirection = Rotate(playerDirection, sweepOffset);
                Vector3 origin = context.Boss.transform.position + (Vector3)(spawnDirection * spawnCircleRadius);

                EnemyProjectile firedProjectile = context.FireProjectile(
                    settings,
                    origin,
                    spawnDirection,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: chargeSecondsOverride,
                    suppressHoming: true,
                    projectileName: resolvedProjectileName);
                if (firedProjectile != null)
                {
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                    context.PlayOriginBurst(effects, origin);
                    context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, spawnDirection);
                    context.PlayCameraShakeIfEnabled(effects, spawnDirection);
                }

                if (index < count - 1)
                {
                    yield return HackerMeleeAttackAction.Wait(context, fireInterval);
                }
            }

            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0f, fireInterval) * Mathf.Max(0, bulletCount - 1)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private static Vector2 GetDirectionToPlayer(BossActionContext context, Vector3 origin)
        {
            Vector2 direction = context.GetDirectionToPlayer(origin);
            return direction.sqrMagnitude > 0.0001f ? direction : Vector2.left;
        }

        private static IEnumerator WaitForMeleeAttackAdvanceCompletion(BossActionContext context)
        {
            while (!context.HasMeleeAttackAdvanceCompleted)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                }

                yield return null;
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

    }
}
