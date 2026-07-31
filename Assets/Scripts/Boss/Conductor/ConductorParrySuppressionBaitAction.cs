using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorParrySuppressionBaitAction : BossAction, IBossProjectileEmissionAction
    {
        private const int DroneCount = 4;

        [Header("Boss Movement")]
        [SerializeField] private Vector2 bossTargetPosition;
        [SerializeField, Min(0.01f)] private float bossMoveSpeed = 8f;
        [SerializeField, Min(0.01f)] private float bossArrivalDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float bossMoveTimeoutSeconds = 5f;
        [SerializeField, Min(0.01f)] private float bossMoveStallSeconds = 0.2f;

        [Header("Closing Drone Columns")]
        [SerializeField] private Vector2 formationCenter;
        [FormerlySerializedAs("rowHalfWidth")]
        [FormerlySerializedAs("initialSquareHalfExtent")]
        [FormerlySerializedAs("initialOrbitRadius")]
        [SerializeField, Min(0.1f)] private float initialSquareHalfExtent = 5f;
        [SerializeField, Min(0.1f)] private float rowHalfHeight = 5f;
        [FormerlySerializedAs("finalRowHalfHeight")]
        [FormerlySerializedAs("finalSquareHalfExtent")]
        [FormerlySerializedAs("finalOrbitRadius")]
        [SerializeField, Min(0.1f)] private float finalColumnHalfWidth = 0.55f;
        [FormerlySerializedAs("squareSeconds")]
        [FormerlySerializedAs("orbitSeconds")]
        [SerializeField, Min(0.01f)] private float closingSeconds = 6f;
        [Tooltip("실패 시 좌·우 드론이 한 번에 압축되는 가로 반쪽 길이입니다.")]
        [FormerlySerializedAs("finalCollapseRadius")]
        [SerializeField, Min(0.1f)] private float finalCollapseHalfExtent = 0.1f;

        [Header("Vertical Volley")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, Min(0.01f)] private float fireInterval = 0.18f;
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings projectileEffects = new();
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;

        [Header("Parry Bait")]
        [SerializeField, BossGraphProjectileName] private string baitProjectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings baitProjectile = new();
        [SerializeField] private Vector2 baitDroneOffset = new(0f, 0.5f);
        [SerializeField] private GameObject baitSpawnEffectPrefab;
        [Tooltip("위쪽 두 드론은 입력값을 그대로 사용하고, 아래쪽 두 드론은 X/Y 부호를 모두 반전합니다.")]
        [SerializeField] private Vector2 baitSpawnEffectOffset = new(0f, 0.5f);
        [Tooltip("이펙트 프리팹 원본 스케일에 곱할 배율입니다.")]
        [SerializeField, Min(0.01f)] private float baitSpawnEffectScale = 1f;
        [Tooltip("패링 미끼 투사체 생성 전에 드론 자식으로 이펙트를 유지할 시간입니다.")]
        [SerializeField, Min(0f)] private float baitSpawnEffectLeadSeconds = 0.8f;
        [Tooltip("전체 오비트 종료 전 이 시간만 남았을 때 패링 억제탄을 생성합니다.")]
        [SerializeField, Min(0.01f)] private float baitSpawnRemainingSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float baitDurationSeconds = 2f;
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float rewardLifetimeSeconds = 2f;
        [SerializeField, BossGraphSfxId] private string baitSpawnSfxId;
        [SerializeField] private BossGraphEffectSettings baitEffects = new();

        [Header("Completion")]
        [SerializeField, Min(0f)] private float bossGroggySeconds = 3f;
        [SerializeField, Min(1)] private int finalCollapseDamage = 1;
        [SerializeField, Min(0f)] private float releaseWanderSeconds;
        [SerializeField, Min(0.01f)] private float releaseWanderSpeed = 4f;
        [SerializeField, Min(0.1f)] private float releaseWanderRadius = 3f;
        [SerializeField, Min(0.1f)] private float releaseWanderRetargetSeconds = 0.6f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context?.Boss == null
                || context.Boss.Player == null
                || !MinionGraphActionHost.TryResolveProjectile(host, projectileName, out BossProjectileSettings projectile))
            {
                yield break;
            }

            yield return host.EnsureMinionCount(DroneCount);
            List<Minion> drones = GetDrones(host.GetControlledMinionsForGraph());
            if (drones.Count != DroneCount)
            {
                yield break;
            }

            Conductor.ConductingAnimationLease conductingAnimation =
                (context.Boss as Conductor)?.CreateConductingAnimationLease();
            yield return MoveBossToTarget(context);

            float safeRowHalfHeight = GetSafeRowHalfHeight();
            bool baitParried = false;
            EnemyProjectile baitProjectileInstance = null;
            Minion baitDrone = null;
            GameObject baitSpawnEffectInstance = null;
            Action<EnemyProjectile, EnemyProjectileDestroyReason, Vector3> baitDestroyedHandler = null;
            try
            {
                conductingAnimation?.Begin();
                SnapDronesToColumns(
                    drones,
                    formationCenter,
                    initialSquareHalfExtent,
                    safeRowHalfHeight);
                yield return null;
                CommandClosingColumns(drones);

                float elapsed = 0f;
                float nextFireAt = 0f;
                int volleyIndex = 0;
                float effectiveFireInterval = Mathf.Max(0.01f, fireInterval);
                float safeClosingSeconds = Mathf.Max(0.01f, closingSeconds);
                float safeBaitSpawnRemainingSeconds = baitSpawnRemainingSeconds > 0f
                    ? baitSpawnRemainingSeconds
                    : Mathf.Min(1.5f, safeClosingSeconds * 0.5f);
                float baitSpawnTime = Mathf.Max(0f, safeClosingSeconds - safeBaitSpawnRemainingSeconds);
                float baitSpawnEffectTime = Mathf.Max(
                    0f,
                    baitSpawnTime - Mathf.Max(0f, baitSpawnEffectLeadSeconds));
                bool baitSpawnEffectSpawned = false;
                bool baitSpawned = false;
                bool baitSpawnSfxPlayed = false;
                while (elapsed < safeClosingSeconds)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    if (!baitSpawnEffectSpawned && elapsed >= baitSpawnEffectTime)
                    {
                        baitSpawnEffectSpawned = true;
                        baitDrone = GetRandomDrone(drones);
                        baitSpawnEffectInstance = SpawnBaitEffect(context, baitDrone);
                        if (baitSpawnEffectInstance != null)
                        {
                            PlayBaitSpawnSfx(context);
                            baitSpawnSfxPlayed = true;
                        }
                    }

                    if (!baitSpawned && elapsed >= baitSpawnTime)
                    {
                        baitSpawned = true;
                        baitDrone ??= GetRandomDrone(drones);
                        ClearBaitSpawnEffect(context, ref baitSpawnEffectInstance);
                        if (!baitSpawnSfxPlayed)
                        {
                            PlayBaitSpawnSfx(context);
                        }
                        baitProjectileInstance = SpawnBait(context, baitDrone);
                        if (baitProjectileInstance != null)
                        {
                            baitDestroyedHandler = (_, reason, _) =>
                            {
                                baitParried = reason == EnemyProjectileDestroyReason.Intercepted;
                            };
                            baitProjectileInstance.Destroyed += baitDestroyedHandler;
                        }
                    }

                    UpdateBaitPosition(baitProjectileInstance, baitDrone);

                    if (elapsed >= nextFireAt)
                    {
                        FireVerticalVolley(context, drones, projectile, volleyIndex++);
                        nextFireAt += effectiveFireInterval;
                    }

                    if (baitParried)
                    {
                        if (bossGroggySeconds > 0f && context.Boss is GraphBossAI boss)
                        {
                            boss.RequestGroggy(bossGroggySeconds);
                        }

                        yield break;
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                if (!baitParried)
                {
                    if (baitProjectileInstance != null)
                    {
                        baitProjectileInstance.DestroyFromOwner();
                    }

                    SnapDronesToColumns(
                        drones,
                        formationCenter,
                        finalCollapseHalfExtent,
                        safeRowHalfHeight);
                    yield return null;
                    ApplyFinalCollapseDamage(context);
                }
            }
            finally
            {
                if (!ReferenceEquals(baitProjectileInstance, null) && baitDestroyedHandler != null)
                {
                    baitProjectileInstance.Destroyed -= baitDestroyedHandler;
                }

                ClearBaitSpawnEffect(context, ref baitSpawnEffectInstance);
                ResumeDrones(drones);
                conductingAnimation?.Dispose();
            }
        }

        private GameObject SpawnBaitEffect(BossActionContext context, Minion drone)
        {
            if (baitSpawnEffectPrefab == null || drone == null)
            {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(baitSpawnEffectPrefab, drone.transform);
            Vector2 effectOffset = baitSpawnEffectOffset;
            if (drone.transform.position.y < formationCenter.y)
            {
                effectOffset = -effectOffset;
            }

            instance.transform.position = drone.transform.position + (Vector3)effectOffset;
            instance.transform.localScale *= Mathf.Max(0.01f, baitSpawnEffectScale);
            context.RegisterTransientVisual(instance);
            return instance;
        }

        private static void ClearBaitSpawnEffect(BossActionContext context, ref GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            context.UnregisterTransientVisual(instance);
            UnityEngine.Object.Destroy(instance);
            instance = null;
        }

        private EnemyProjectile SpawnBait(BossActionContext context, Minion drone)
        {
            if (drone == null)
            {
                return null;
            }

            Vector3 spawnOrigin = drone.transform.position + (Vector3)baitDroneOffset;
            Vector2 direction = context?.Boss?.Player != null
                ? (Vector2)context.Boss.Player.position - (Vector2)spawnOrigin
                : Vector2.up;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.up;
            }

            EnemyProjectile spawned = context.FireProjectile(
                baitProjectile,
                spawnOrigin,
                direction,
                0f,
                projectileName: baitProjectileName);
            if (spawned is not ParryBaitRewardProjectile bait)
            {
                spawned?.DestroyFromOwner();
                return null;
            }

            bait.ConfigureBaitDuration(baitDurationSeconds);
            bait.ConfigureRewardOverrides(rewardBulletCount, rewardCircleRadius, rewardLifetimeSeconds);
            bait.ConfigureExternalMotionDriven(true);
            context.PlayOriginBurst(baitEffects, spawnOrigin);
            return bait;
        }

        private void PlayBaitSpawnSfx(BossActionContext context)
        {
            context?.PlaySfx(string.IsNullOrWhiteSpace(baitSpawnSfxId)
                ? GameplaySfxIds.BossCreateParrySuppressionBait
                : baitSpawnSfxId);
        }

        private void UpdateBaitPosition(EnemyProjectile bait, Minion drone)
        {
            if (bait == null || drone == null)
            {
                return;
            }

            bait.transform.position = drone.transform.position + (Vector3)baitDroneOffset;
        }

        private void CommandClosingColumns(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.CommandConductorClosingColumns(
                    i,
                    formationCenter,
                    initialSquareHalfExtent,
                    finalColumnHalfWidth,
                    GetSafeRowHalfHeight(),
                    closingSeconds);
            }
        }

        private static void SnapDronesToColumns(
            IReadOnlyList<Minion> drones,
            Vector2 center,
            float halfWidth,
            float halfHeight)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.CommandConductorSnapToClosingColumn(i, center, halfWidth, halfHeight);
            }
        }

        private float GetSafeRowHalfHeight()
        {
            return rowHalfHeight > 0f ? rowHalfHeight : initialSquareHalfExtent;
        }

        private void FireVerticalVolley(
            BossActionContext context,
            IReadOnlyList<Minion> drones,
            BossProjectileSettings projectile,
            int shotIndex)
        {
            MinionGraphProjectileFireSpec fireSpec = new(
                minionOrigin,
                null,
                projectileEffects,
                context);
            bool firedAny = false;
            EnemyProjectile launchSfxTarget = null;
            for (int i = 0; i < drones.Count; i++)
            {
                Minion drone = drones[i];
                if (drone == null)
                {
                    continue;
                }

                Vector2 direction = GetRowFireDirection(i);
                MinionGraphProjectileFireSpec droneFireSpec = fireSpec.WithFixedDirection(direction);
                Vector3 spawnOrigin = droneFireSpec.GetSpawnOrigin(drone, shotIndex, direction);
                EnemyProjectile spawned = context.FireProjectile(
                    projectile,
                    spawnOrigin,
                    direction,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    suppressHoming: true,
                    projectileName: projectileName);
                if (spawned != null)
                {
                    droneFireSpec.PlayEffects(spawnOrigin, spawned, direction, drone.transform);
                    firedAny = true;
                    launchSfxTarget ??= spawned;
                }
            }

            // 이 틱에 드론이 몇 마리 쐈든 사운드는 한 번만 재생한다.
            // launchSfxId는 대표 투사체 1개의 실제 Launched 이벤트에 걸어서, 그 사이 파괴되면 소리가 안 나게 한다.
            if (firedAny)
            {
                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(launchSfxTarget, launchSfxId);
            }
        }

        private static Vector2 GetRowFireDirection(int droneIndex)
        {
            return droneIndex < 2 ? Vector2.down : Vector2.up;
        }

        private IEnumerator MoveBossToTarget(BossActionContext context)
        {
            float elapsed = 0f;
            float stalledSeconds = 0f;
            float arrivalDistanceSquared = bossArrivalDistance * bossArrivalDistance;
            context.Boss.SetIgnorePlayerCollision(true);
            try
            {
                while (elapsed < bossMoveTimeoutSeconds)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    Vector2 currentPosition = context.Boss.Body != null
                        ? context.Boss.Body.position
                        : context.Boss.transform.position;
                    Vector2 offset = bossTargetPosition - currentPosition;
                    if (offset.sqrMagnitude <= arrivalDistanceSquared)
                    {
                        yield break;
                    }

                    if (context.Boss.TryMovePatternTowards(bossTargetPosition, bossMoveSpeed))
                    {
                        stalledSeconds = 0f;
                    }
                    else
                    {
                        stalledSeconds += EnemyTimeScale.DeltaTime;
                        if (stalledSeconds >= bossMoveStallSeconds)
                        {
                            yield break;
                        }
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }
            finally
            {
                context.Stop();
                context.Boss.SetIgnorePlayerCollision(false);
            }
        }

        private void ResumeDrones(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.CommandWander(
                    releaseWanderSeconds,
                    releaseWanderSpeed > 0f ? releaseWanderSpeed : 4f,
                    releaseWanderRadius > 0f ? releaseWanderRadius : 3f,
                    releaseWanderRetargetSeconds > 0f ? releaseWanderRetargetSeconds : 0.6f);
            }
        }

        private static Minion GetRandomDrone(IReadOnlyList<Minion> drones)
        {
            if (drones == null || drones.Count == 0)
            {
                return null;
            }

            return drones[UnityEngine.Random.Range(0, drones.Count)];
        }

        private void ApplyFinalCollapseDamage(BossActionContext context)
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || context?.Boss == null)
            {
                return;
            }

            Vector2 hitDirection = (Vector2)player.transform.position - (Vector2)context.Boss.transform.position;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = Vector2.up;
            }

            player.ReceiveAttack(finalCollapseDamage, player.transform.position, hitDirection.normalized);
        }

        private static List<Minion> GetDrones(IReadOnlyList<Minion> source)
        {
            List<Minion> drones = new(DroneCount);
            if (source == null)
            {
                return drones;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Minion minion = source[i];
                if (minion != null && minion.Health != null && !minion.Health.IsDead)
                {
                    drones.Add(minion);
                }
            }

            drones.Sort((left, right) => GetSlotNumber(left).CompareTo(GetSlotNumber(right)));
            if (drones.Count > DroneCount)
            {
                drones.RemoveRange(DroneCount, drones.Count - DroneCount);
            }

            return drones;
        }

        private static int GetSlotNumber(Minion minion)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : int.MaxValue;
        }

    }
}
