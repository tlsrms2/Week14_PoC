using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorParrySuppressionBaitAction : BossAction
    {
        private const int DroneCount = 4;

        [Header("Boss Movement")]
        [SerializeField] private Vector2 bossTargetPosition;
        [SerializeField, Min(0.01f)] private float bossMoveSpeed = 8f;
        [SerializeField, Min(0.01f)] private float bossArrivalDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float bossMoveTimeoutSeconds = 5f;
        [SerializeField, Min(0.01f)] private float bossMoveStallSeconds = 0.2f;

        [Header("Shrinking Drone Orbit")]
        [SerializeField, Min(0.1f)] private float initialOrbitRadius = 5f;
        [SerializeField, Min(0.1f)] private float finalOrbitRadius = 0.55f;
        [SerializeField, Min(0.01f)] private float orbitSeconds = 6f;
        [SerializeField, Min(0f)] private float angularSpeedDegrees = 120f;
        [SerializeField] private bool clockwise = true;
        [SerializeField] private float startAngleDegrees;
        [SerializeField, Min(0f)] private float droneMoveSpeed = 14f;

        [Header("Tangent Volley")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, Min(0.01f)] private float fireInterval = 0.18f;
        [SerializeField, Min(0f)] private float firstFireDelaySeconds;
        [SerializeField, Min(0.01f)] private float tangentCircleRadius = 0.8f;
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings projectileEffects = new();

        [Header("Parry Bait")]
        [SerializeField, BossGraphProjectileName] private string baitProjectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings baitProjectile = new();
        [SerializeField] private Vector2 baitDroneOffset = new(0f, 0.5f);
        [SerializeField, Min(0f)] private float baitSpawnSeconds = 3f;
        [SerializeField, Min(0.1f)] private float baitDurationSeconds = 2f;
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float rewardLifetimeSeconds = 2f;
        [SerializeField, BossGraphSfxId] private string baitSpawnSfxId;
        [SerializeField] private BossGraphEffectSettings baitEffects = new();

        [Header("Completion")]
        [SerializeField, Min(0f)] private float parryStopSeconds = 1f;
        [SerializeField, Min(0f)] private float bossGroggySeconds = 3f;
        [SerializeField, Min(1)] private int finalCollapseDamage = 1;
        [SerializeField] private bool releaseDronesAfterPattern = true;

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

            yield return MoveBossToTarget(context);

            CommandShrinkingOrbit(drones);
            bool baitParried = false;
            bool baitWasSpawned = false;
            bool collapseTriggered = false;
            EnemyProjectile baitProjectileInstance = null;
            Minion baitDrone = null;
            Action<EnemyProjectile, EnemyProjectileDestroyReason, Vector3> baitDestroyedHandler = null;
            try
            {
                float elapsed = 0f;
                float nextFireAt = Mathf.Max(0f, firstFireDelaySeconds);
                int volleyIndex = 0;
                float effectiveFireInterval = Mathf.Max(0.01f, fireInterval);
                bool baitSpawned = false;
                while (elapsed < Mathf.Max(0.01f, orbitSeconds))
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    if (!baitSpawned && elapsed >= Mathf.Max(0f, baitSpawnSeconds))
                    {
                        baitSpawned = true;
                        baitDrone = GetRandomDrone(drones);
                        baitProjectileInstance = SpawnBait(context, baitDrone);
                        if (baitProjectileInstance != null)
                        {
                            baitWasSpawned = true;
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
                        FireTangentVolley(context, drones, projectile, volleyIndex++);
                        nextFireAt += effectiveFireInterval;
                    }

                    if (baitParried)
                    {
                        HoldDrones(drones, parryStopSeconds);
                        if (bossGroggySeconds > 0f && context.Boss is GraphBossAI boss)
                        {
                            boss.RequestGroggy(bossGroggySeconds);
                        }

                        if (parryStopSeconds > 0f)
                        {
                            yield return context.WaitSeconds(parryStopSeconds);
                        }

                        yield break;
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }

                if (baitWasSpawned && !baitParried)
                {
                    collapseTriggered = true;
                    if (baitProjectileInstance != null)
                    {
                        baitProjectileInstance.DestroyFromOwner();
                    }

                    HoldDrones(drones, 0f);
                    ApplyFinalCollapseDamage(context);
                }
            }
            finally
            {
                if (!ReferenceEquals(baitProjectileInstance, null) && baitDestroyedHandler != null)
                {
                    baitProjectileInstance.Destroyed -= baitDestroyedHandler;
                }

                if (releaseDronesAfterPattern && !baitParried && !collapseTriggered)
                {
                    ResumeDrones(drones);
                }
            }
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
            context.PlaySfx(baitSpawnSfxId);
            context.PlayOriginBurst(baitEffects, spawnOrigin);
            return bait;
        }

        private void UpdateBaitPosition(EnemyProjectile bait, Minion drone)
        {
            if (bait == null || drone == null)
            {
                return;
            }

            bait.transform.position = drone.transform.position + (Vector3)baitDroneOffset;
        }

        private void CommandShrinkingOrbit(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.CommandConductorShrinkingOrbit(
                    initialOrbitRadius,
                    finalOrbitRadius,
                    orbitSeconds,
                    angularSpeedDegrees,
                    clockwise,
                    droneMoveSpeed,
                    startAngleDegrees + 360f * i / DroneCount);
            }
        }

        private void FireTangentVolley(
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
            for (int i = 0; i < drones.Count; i++)
            {
                Minion drone = drones[i];
                if (drone == null)
                {
                    continue;
                }

                Vector2 direction = GetTangentDirection(context.Boss.Player, drone);
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
                    droneFireSpec.PlayEffects(spawnOrigin, direction);
                }
            }
        }

        private Vector2 GetTangentDirection(Transform player, Minion drone)
        {
            if (player == null || drone == null)
            {
                return Vector2.left;
            }

            Vector2 radial = (Vector2)drone.transform.position - (Vector2)player.position;
            float distance = radial.magnitude;
            if (distance <= 0.0001f)
            {
                return clockwise ? Vector2.down : Vector2.up;
            }

            float maxTangentRadius = Mathf.Max(0f, distance - 0.001f);
            float radius = Mathf.Min(tangentCircleRadius, maxTangentRadius);
            float turnRadians = Mathf.Asin(Mathf.Clamp01(radius / distance));
            Vector2 inward = -radial / distance;
            return Rotate(inward, clockwise ? turnRadians : -turnRadians);
        }

        private IEnumerator MoveBossToTarget(BossActionContext context)
        {
            float elapsed = 0f;
            float stalledSeconds = 0f;
            float arrivalDistanceSquared = bossArrivalDistance * bossArrivalDistance;
            Vector2 previousPosition = context.Boss.Body != null
                ? context.Boss.Body.position
                : context.Boss.transform.position;
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
                if (elapsed > 0f)
                {
                    if ((currentPosition - previousPosition).sqrMagnitude <= 0.000001f)
                    {
                        stalledSeconds += EnemyTimeScale.DeltaTime;
                        if (stalledSeconds >= bossMoveStallSeconds)
                        {
                            break;
                        }
                    }
                    else
                    {
                        stalledSeconds = 0f;
                    }
                }

                previousPosition = currentPosition;
                Vector2 offset = bossTargetPosition - currentPosition;
                if (offset.sqrMagnitude <= arrivalDistanceSquared)
                {
                    break;
                }

                context.Boss.SetMovementVelocity(offset.normalized * bossMoveSpeed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.Stop();
        }

        private static void HoldDrones(IReadOnlyList<Minion> drones, float holdSeconds)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.CommandHoldPosition(holdSeconds);
            }
        }

        private static void ResumeDrones(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.ResumeIdle();
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

        private static Vector2 Rotate(Vector2 direction, float radians)
        {
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(
                direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }
    }
}
