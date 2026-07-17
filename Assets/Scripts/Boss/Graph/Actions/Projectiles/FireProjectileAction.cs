using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum BossGraphAimMode
    {
        Player,
        Angle
    }

    public enum BossGraphProjectileOriginMode
    {
        BossOrigin,
        BossChild,
        BossChildList,
        AlternatingBossChildList,
        AlternatingBossChildren
    }

    public enum BossGraphProjectileAimMode
    {
        AtPlayer,
        FixedAngle,
        ClosestMinionToPlayer
    }

    [Serializable]
    public sealed class BossGraphProjectileOriginSpec
    {
        [SerializeField] private BossGraphProjectileOriginMode mode;
        [SerializeField, BossGraphBossChildPath] private string bossChildPath;
        [SerializeField, BossGraphBossChildPath] private List<string> bossChildPaths = new();
        [SerializeField, BossGraphBossChildPath] private string firstBossChildPath;
        [SerializeField, BossGraphBossChildPath] private string secondBossChildPath;
        [SerializeField, Min(0f)] private float fallbackSpacing = 0.18f;

        public Vector3 GetAimOrigin(BossActionContext context, int shotIndex)
        {
            if (context == null)
            {
                return Vector3.zero;
            }

            return mode switch
            {
                BossGraphProjectileOriginMode.BossChild => context.GetBossChildPosition(bossChildPath),
                BossGraphProjectileOriginMode.BossChildList => context.GetBossChildPosition(GetListPath(shotIndex, false)),
                BossGraphProjectileOriginMode.AlternatingBossChildList => context.GetBossChildPosition(GetListPath(shotIndex, true)),
                BossGraphProjectileOriginMode.AlternatingBossChildren => context.GetBossChildPosition(GetAlternatingPath(shotIndex)),
                _ => context.OriginPosition
            };
        }

        public Transform GetAimOriginTransform(BossActionContext context, int shotIndex)
        {
            if (context == null)
            {
                return null;
            }

            return mode switch
            {
                BossGraphProjectileOriginMode.BossChild => context.GetBossChildTransform(bossChildPath),
                BossGraphProjectileOriginMode.BossChildList => context.GetBossChildTransform(GetListPath(shotIndex, false)),
                BossGraphProjectileOriginMode.AlternatingBossChildList => context.GetBossChildTransform(GetListPath(shotIndex, true)),
                BossGraphProjectileOriginMode.AlternatingBossChildren => context.GetBossChildTransform(GetAlternatingPath(shotIndex)),
                _ => null
            };
        }

        public Vector3 GetSpawnOrigin(BossActionContext context, int shotIndex, Vector2 direction)
        {
            Vector3 origin = GetAimOrigin(context, shotIndex);
            if (mode != BossGraphProjectileOriginMode.BossOrigin || fallbackSpacing <= 0f || shotIndex <= 0)
            {
                return origin;
            }

            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            Vector2 side = new(-normalizedDirection.y, normalizedDirection.x);
            int ring = (shotIndex + 1) / 2;
            float sign = shotIndex % 2 == 0 ? -1f : 1f;
            return origin + (Vector3)(side * ring * fallbackSpacing * sign);
        }

        private string GetAlternatingPath(int shotIndex)
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(firstBossChildPath);
            bool hasSecond = !string.IsNullOrWhiteSpace(secondBossChildPath);
            if (!hasFirst)
            {
                return secondBossChildPath;
            }

            if (!hasSecond)
            {
                return firstBossChildPath;
            }

            return shotIndex % 2 == 0 ? firstBossChildPath : secondBossChildPath;
        }

        private string GetListPath(int shotIndex, bool loop)
        {
            if (bossChildPaths == null || bossChildPaths.Count == 0)
            {
                return bossChildPath;
            }

            int index = loop
                ? Mathf.Abs(shotIndex) % bossChildPaths.Count
                : Mathf.Clamp(shotIndex, 0, bossChildPaths.Count - 1);
            return bossChildPaths[index];
        }
    }

    [Serializable]
    public sealed class BossGraphProjectileAimSpec
    {
        [SerializeField] private BossGraphProjectileAimMode mode;
        [SerializeField] private float angleDegrees;

        public BossGraphProjectileAimMode Mode => mode;

        public Vector2 GetDirection(BossActionContext context, Vector3 origin)
        {
            return GetDirection(context != null ? context.GetDirectionToPlayer : null, origin);
        }

        public Vector2 GetDirection(Func<Vector3, Vector2> getDirectionToPlayer, Vector3 origin)
        {
            if (mode == BossGraphProjectileAimMode.FixedAngle)
            {
                return BossActionContext.AngleToDirection(angleDegrees);
            }

            if (getDirectionToPlayer == null)
            {
                return Vector2.left;
            }

            return getDirectionToPlayer(origin);
        }
    }

    [Serializable]
    public sealed class FireProjectileAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Tooltip("0 이상이면 Projectile Settings의 Charge Seconds 대신 이 값을 사용합니다. 음수(-1)면 오버라이드하지 않습니다.")] private float chargeSecondsOverride = -1f;
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
            ResolveShot(context, originSpec, aimSpec, 0, out Vector3 spawnOrigin, out Vector2 finalDirection);

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                finalDirection,
                0f,
                chargeSecondsOverride: chargeSecondsOverride,
                projectileName: projectileName);
            if (firedProjectile != null)
            {
                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                context.PlayOriginBurst(effects, spawnOrigin);
                context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, finalDirection);
                context.PlayCameraShakeIfEnabled(effects, finalDirection);
            }

            yield break;
        }

        private void ResolveShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int shotIndex,
            out Vector3 spawnOrigin,
            out Vector2 finalDirection)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, shotIndex);
            Vector2 direction = aimSpec.GetDirection(context, aimOrigin);
            spawnOrigin = originSpec.GetSpawnOrigin(context, shotIndex, direction);
            finalDirection = aimSpec.GetDirection(context, spawnOrigin);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(finalDirection.normalized * spawnForwardOffset);
            }
        }
    }

    [Serializable]
    public sealed class FireProjectileBurstAction : BossAction
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, Min(1)] private int bulletCount = 4;
            [SerializeField, Min(0f)] private float fireInterval = 0.12f;
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;

            public int BulletCount => Mathf.Max(1, bulletCount);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [FormerlySerializedAs("startDelaySeconds")]
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField] private List<Volley> volleys = new() { new Volley() };

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || volleys == null || volleys.Count == 0)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            int shotIndex = 0;
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                for (int bulletIndex = 0; bulletIndex < volley.BulletCount; bulletIndex++)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        bulletIndex--;
                        continue;
                    }

                    FireShot(context, shotIndex);
                    shotIndex++;

                    if (bulletIndex < volley.BulletCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return context.WaitSeconds(volley.FireInterval);
                    }
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        private void FireShot(BossActionContext context, int shotIndex)
        {
            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            ResolveShot(context, originSpec, aimSpec, shotIndex, out Vector3 spawnOrigin, out Vector2 finalDirection);

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                finalDirection,
                0f,
                projectileName: projectileName);

            if (firedProjectile == null)
            {
                return;
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, finalDirection);
            context.PlayCameraShakeIfEnabled(effects, finalDirection);
        }

        private void ResolveShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int shotIndex,
            out Vector3 spawnOrigin,
            out Vector2 finalDirection)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, shotIndex);
            Vector2 direction = aimSpec.GetDirection(context, aimOrigin);
            spawnOrigin = originSpec.GetSpawnOrigin(context, shotIndex, direction);
            finalDirection = aimSpec.GetDirection(context, spawnOrigin);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(finalDirection.normalized * spawnForwardOffset);
            }
        }
    }

    [Serializable]
    public sealed class FireRandomConeBurstAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private bool useOriginFacingDirection = true;
        [SerializeField, Min(1)] private int bulletCount = 18;
        [SerializeField, Range(0f, 360f)] private float angleRangeDegrees = 45f;
        [SerializeField, Min(0f)] private float fireInterval = 0.04f;
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [FormerlySerializedAs("startDelaySeconds")]
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField] private float chargeSecondsOverride = -1f;
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
            int count = Mathf.Max(1, bulletCount);
            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                FireShot(context, originSpec, aimSpec, i);
                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }

        private void FireShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int shotIndex)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, shotIndex);
            Vector2 baseDirection = GetBaseDirection(context, originSpec, aimSpec, shotIndex, aimOrigin);
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            float randomOffset = UnityEngine.Random.Range(-angleRangeDegrees * 0.5f, angleRangeDegrees * 0.5f);
            Vector2 finalDirection = BossActionContext.AngleToDirection(baseAngle + randomOffset);
            Vector3 spawnOrigin = originSpec.GetSpawnOrigin(context, shotIndex, finalDirection);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(finalDirection.normalized * spawnForwardOffset);
            }

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                finalDirection,
                0f,
                chargeSecondsOverride: chargeSecondsOverride,
                projectileName: projectileName);

            if (firedProjectile == null)
            {
                return;
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, finalDirection);
            context.PlayCameraShakeIfEnabled(effects, finalDirection);
        }

        private Vector2 GetBaseDirection(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int shotIndex,
            Vector3 aimOrigin)
        {
            if (useOriginFacingDirection)
            {
                Transform originTransform = originSpec.GetAimOriginTransform(context, shotIndex);
                if (originTransform != null)
                {
                    Vector2 facingDirection = originTransform.right;
                    if (facingDirection.sqrMagnitude > 0.0001f)
                    {
                        return facingDirection.normalized;
                    }
                }
            }

            Vector2 aimDirection = aimSpec.GetDirection(context, aimOrigin);
            return aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.left;
        }
    }

    [Serializable]
    public sealed class FirePlayerSideFanSweepAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, Min(1)] private int bulletCount = 5;
        [SerializeField, Range(-180f, 180f)] private float playerSideAngleDegrees = 90f;
        [SerializeField, Min(0f)] private float firstOffsetDistance = 1f;
        [SerializeField, Min(0f)] private float lineSpacing = 0.55f;
        [SerializeField, Min(0.01f)] private float lineupSpeed = 8f;
        [SerializeField, Min(0f)] private float lineupIntervalSeconds = 0.06f;
        [SerializeField, Range(0f, 180f)] private float fanAngleDegrees = 65f;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Min(0f)] private float waitBeforeSweepSeconds = 0.6f;
        [SerializeField, Min(0.01f)] private float sweepDurationSeconds = 0.8f;
        [SerializeField, Range(2, 24)] private int arcSegments = 8;
        [SerializeField] private bool destroyOnSweepEnd = true;
        [SerializeField] private bool waitForSweepEnd = true;
        [SerializeField, BossGraphSfxId] private string setupSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Player == null)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            Vector2 bossPosition = context.OriginPosition;
            Vector2 playerPosition = context.Boss.Player.position;
            Vector2 toPlayer = playerPosition - bossPosition;
            if (toPlayer.sqrMagnitude <= 0.0001f)
            {
                toPlayer = Vector2.left;
            }

            float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float lineupAngle = baseAngle + playerSideAngleDegrees;
            int count = Mathf.Max(1, bulletCount);
            Vector2 lineupDirection = BossActionContext.AngleToDirection(lineupAngle);
            float firstStopDistance = Mathf.Max(0.1f, firstOffsetDistance);
            float effectiveSpacing = Mathf.Max(0f, lineSpacing);
            float lineupLength = firstStopDistance + effectiveSpacing * Mathf.Max(0, count - 1);
            float sweepTargetAngle = GetRodSweepTargetAngle(lineupAngle, baseAngle);
            float spawnInterval = Mathf.Max(0f, lineupIntervalSeconds);
            float moveSpeed = Mathf.Max(0.01f, lineupSpeed);
            Vector2[] stopPositions = new Vector2[count];
            Vector2[][] sweepPaths = new Vector2[count][];
            float[] travelDurations = new float[count];
            float[] spawnDelays = new float[count];
            float lineupEndSeconds = 0f;
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Max(0.1f, lineupLength - effectiveSpacing * i);
                Vector2 stopPosition = bossPosition + lineupDirection * distance;
                stopPositions[i] = stopPosition;
                sweepPaths[i] = BuildSweepPath(bossPosition, stopPosition, lineupAngle, sweepTargetAngle);
                travelDurations[i] = Vector2.Distance(bossPosition, stopPosition) / moveSpeed;
                spawnDelays[i] = spawnInterval * i;
                lineupEndSeconds = Mathf.Max(lineupEndSeconds, spawnDelays[i] + travelDurations[i]);
            }

            bool spawnedAny = false;
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && spawnInterval > 0f)
                {
                    yield return context.WaitSeconds(spawnInterval);
                }

                Vector2 stopPosition = stopPositions[i];
                Vector2 launchDirection = lineupDirection;
                if (launchDirection.sqrMagnitude <= 0.0001f)
                {
                    launchDirection = toPlayer;
                }

                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    bossPosition,
                    launchDirection.normalized,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: 0f,
                    suppressHoming: true,
                    projectileName: projectileName);

                if (firedProjectile == null)
                {
                    continue;
                }

                firedProjectile.ConfigurePathIndicatorSuppressed(true);
                float sweepStartSeconds = Mathf.Max(
                    travelDurations[i],
                    lineupEndSeconds - spawnDelays[i] + waitBeforeSweepSeconds);
                firedProjectile.gameObject.AddComponent<BossDelayedPathProjectileMotion>().Initialize(
                    bossPosition,
                    stopPosition,
                    sweepPaths[i],
                    travelDurations[i],
                    sweepStartSeconds,
                    sweepDurationSeconds,
                    destroyOnSweepEnd);
                spawnedAny = true;
                context.PlaySfx(setupSfxId);
            }

            if (spawnedAny)
            {
                context.PlaySfx(launchSfxId);
                context.PlayOriginBurst(effects, bossPosition);
                context.PlayCameraShakeIfEnabled(effects, toPlayer);
            }

            if (waitForSweepEnd)
            {
                float elapsedSpawnSeconds = spawnInterval * Mathf.Max(0, count - 1);
                float remainingSeconds = Mathf.Max(
                    0f,
                    lineupEndSeconds + waitBeforeSweepSeconds + sweepDurationSeconds - elapsedSpawnSeconds);
                yield return context.WaitSeconds(remainingSeconds);
            }
        }

        private float GetRodSweepTargetAngle(float startAngle, float playerAngle)
        {
            float deltaToPlayer = Mathf.DeltaAngle(startAngle, playerAngle);
            float sweepDirection = Mathf.Sign(deltaToPlayer);
            if (Mathf.Approximately(sweepDirection, 0f))
            {
                sweepDirection = Mathf.Sign(-playerSideAngleDegrees);
                if (Mathf.Approximately(sweepDirection, 0f))
                {
                    sweepDirection = 1f;
                }
            }

            return startAngle + sweepDirection * Mathf.Max(0f, fanAngleDegrees);
        }

        private Vector2[] BuildSweepPath(Vector2 bossPosition, Vector2 spawnPosition, float startAngle, float targetAngle)
        {
            int segments = Mathf.Max(2, arcSegments);
            Vector2 fromBoss = spawnPosition - bossPosition;
            float radius = Mathf.Max(0.1f, fromBoss.magnitude);
            float deltaAngle = Mathf.DeltaAngle(startAngle, targetAngle);
            Vector2[] points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points[i] = bossPosition + BossActionContext.AngleToDirection(startAngle + deltaAngle * t) * radius;
            }

            points[0] = spawnPosition;
            return points;
        }
    }
}
