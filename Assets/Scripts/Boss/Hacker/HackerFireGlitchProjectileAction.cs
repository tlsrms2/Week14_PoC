using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
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

        [Header("Glitch Timing")]
        [FormerlySerializedAs("useChargeStartAfterLaunchTime")]
        [SerializeField] private bool useTimedChargeApproach;
        [FormerlySerializedAs("chargeStartAfterLaunchSeconds")]
        [SerializeField, Min(0f)] private float chargeApproachArrivalSeconds = 1.5f;

        [Header("Launch")]
        [SerializeField] private string animationTriggerName = "FireGlitch";
        [SerializeField, Min(0f)] private float windupSeconds = 0.3f;
        [FormerlySerializedAs("upwardRandomHalfAngleDegrees")]
        [SerializeField, Range(0f, 180f)] private float oppositePlayerRandomHalfAngleDegrees = 65f;
        [Header("Wall Avoidance")]
        [SerializeField, Min(0f)] private float initialWallClearance = 3f;
        [SerializeField, Min(1)] private int launchDirectionSamples = 8;
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
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, 0);
            Vector2 oppositePlayerDirection = -context.GetDirectionToPlayer(aimOrigin);
            if (oppositePlayerDirection.sqrMagnitude <= 0.0001f)
            {
                oppositePlayerDirection = Vector2.up;
            }

            Vector2 direction = SelectLaunchDirection(context, originSpec, oppositePlayerDirection);
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
            if (firedProjectile is HackerGlitchProjectile glitchProjectile)
            {
                glitchProjectile.ConfigureTimedChargeApproach(
                    useTimedChargeApproach ? chargeApproachArrivalSeconds : -1f);
            }
            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, direction);
            context.PlayCameraShakeIfEnabled(effects, direction);
            yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds) + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private Vector2 SelectLaunchDirection(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            Vector2 oppositePlayerDirection)
        {
            float baseAngleDegrees = Mathf.Atan2(oppositePlayerDirection.y, oppositePlayerDirection.x) * Mathf.Rad2Deg;
            int wallLayer = LayerMask.NameToLayer("Wall");
            int sampleCount = Mathf.Max(1, launchDirectionSamples);
            Vector2 safestDirection = GetRandomDirection(baseAngleDegrees, oppositePlayerRandomHalfAngleDegrees);
            float greatestClearance = float.NegativeInfinity;

            for (int i = 0; i < sampleCount; i++)
            {
                Vector2 candidateDirection = GetRandomDirection(baseAngleDegrees, oppositePlayerRandomHalfAngleDegrees);
                if (IsDirectionClear(context, originSpec, candidateDirection, wallLayer, out float clearance))
                {
                    return candidateDirection;
                }

                if (clearance > greatestClearance)
                {
                    greatestClearance = clearance;
                    safestDirection = candidateDirection;
                }
            }

            // 반대편 부채꼴이 모두 막힌 경우에만 벽이 없는 방향을 넓게 다시 찾는다.
            for (int i = 0; i < sampleCount; i++)
            {
                Vector2 candidateDirection = GetRandomDirection(UnityEngine.Random.Range(0f, 360f), 0f);
                if (IsDirectionClear(context, originSpec, candidateDirection, wallLayer, out float clearance))
                {
                    return candidateDirection;
                }

                if (clearance > greatestClearance)
                {
                    greatestClearance = clearance;
                    safestDirection = candidateDirection;
                }
            }

            return safestDirection;
        }

        private bool IsDirectionClear(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            Vector2 direction,
            int wallLayer,
            out float clearance)
        {
            clearance = float.PositiveInfinity;
            if (initialWallClearance <= 0f || wallLayer < 0)
            {
                return true;
            }

            Vector3 spawnOrigin = originSpec.GetSpawnOrigin(context, 0, direction);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(direction * spawnForwardOffset);
            }

            RaycastHit2D hit = Physics2D.Raycast(
                spawnOrigin,
                direction,
                initialWallClearance,
                1 << wallLayer);
            if (hit.collider == null)
            {
                return true;
            }

            clearance = hit.distance;
            return false;
        }

        private static Vector2 GetRandomDirection(float centerAngleDegrees, float halfAngleDegrees)
        {
            float angleDegrees = centerAngleDegrees + UnityEngine.Random.Range(-halfAngleDegrees, halfAngleDegrees);
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
