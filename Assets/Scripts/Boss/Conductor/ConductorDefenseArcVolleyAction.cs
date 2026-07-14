using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorDefenseArcVolleyAction : BossAction
    {
        private const int DroneCount = 4;

        [Serializable]
        public sealed class Volley
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, Min(1)] private int repeatCount = 3;
            [SerializeField, Min(0f)] private float fireInterval = 0.15f;
            [SerializeField, Min(0f)] private float restSeconds = 0.25f;

            public string ProjectileName => projectileName;
            public int RepeatCount => Mathf.Max(1, repeatCount);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [Header("Boss Movement")]
        [SerializeField] private Vector2 targetPosition;
        [SerializeField, Min(0.01f)] private float moveSpeed = 8f;
        [SerializeField, Min(0.01f)] private float arrivalDistance = 0.05f;
        [SerializeField, Min(0.01f)] private float moveTimeoutSeconds = 5f;

        [Header("Defense Arc")]
        [SerializeField, Min(0f)] private float arcRadius = 2.5f;
        [SerializeField, Range(0f, 360f)] private float arcDegrees = 90f;
        [SerializeField, Min(0f)] private float droneMoveSpeed = 12f;
        [SerializeField, Min(0f)] private float formationHoldSeconds = 1f;
        [SerializeField, Min(0f)] private float windupSeconds = 0.5f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.25f;
        [SerializeField] private bool releaseDronesAfterVolley = true;

        [Header("Volleys")]
        [SerializeField] private List<Volley> volleys = new() { new Volley() };
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context?.Boss == null
                || context.Boss.Player == null
                || !HasConfiguredVolley())
            {
                yield break;
            }

            yield return host.EnsureMinionCount(DroneCount);
            List<Minion> drones = GetDrones(host.GetControlledMinionsForGraph());
            if (drones.Count != DroneCount)
            {
                yield break;
            }

            CommandDefenseArc(drones);
            yield return MoveBossToTarget(context);
            yield return context.WaitSeconds(formationHoldSeconds);
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);

            yield return ExecuteVolleys(context, host, drones);

            yield return context.WaitSeconds(recoverySeconds);
            if (releaseDronesAfterVolley)
            {
                for (int i = 0; i < drones.Count; i++)
                {
                    if (drones[i] != null)
                    {
                        drones[i].ResumeIdle();
                    }
                }
            }
        }

        private IEnumerator ExecuteVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones)
        {
            if (volleys == null)
            {
                yield break;
            }

            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null
                    || !MinionGraphActionHost.TryResolveProjectile(host, volley.ProjectileName, out BossProjectileSettings projectile))
                {
                    continue;
                }

                Vector2 direction = GetSharedPlayerDirection(context);
                MinionGraphProjectileFireSpec fireSpec = new(
                    minionOrigin,
                    null,
                    effects,
                    context);
                fireSpec = fireSpec.WithFixedDirection(direction);

                for (int shotIndex = 0; shotIndex < volley.RepeatCount; shotIndex++)
                {
                    while (context.IsExecutionPaused)
                    {
                        yield return null;
                    }

                    for (int droneIndex = 0; droneIndex < drones.Count; droneIndex++)
                    {
                        drones[droneIndex]?.FireOnce(projectile, fireSpec, shotIndex);
                    }

                    if (shotIndex < volley.RepeatCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return context.WaitSeconds(volley.FireInterval);
                    }
                }

                if (volley.RestSeconds > 0f)
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        private static Vector2 GetSharedPlayerDirection(BossActionContext context)
        {
            Vector2 direction = context?.Boss != null && context.Boss.Player != null
                ? (Vector2)context.Boss.Player.position - (Vector2)context.Boss.transform.position
                : Vector2.left;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        private bool HasConfiguredVolley()
        {
            if (volleys == null)
            {
                return false;
            }

            for (int i = 0; i < volleys.Count; i++)
            {
                if (volleys[i] != null && !string.IsNullOrWhiteSpace(volleys[i].ProjectileName))
                {
                    return true;
                }
            }

            return false;
        }

        private void CommandDefenseArc(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i].CommandConductorDefenseArc(
                    i,
                    DroneCount,
                    arcRadius,
                    arcDegrees,
                    droneMoveSpeed);
            }
        }

        private IEnumerator MoveBossToTarget(BossActionContext context)
        {
            float elapsed = 0f;
            float arrivalDistanceSquared = arrivalDistance * arrivalDistance;
            while (elapsed < moveTimeoutSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector2 offset = targetPosition - (Vector2)context.Boss.transform.position;
                if (offset.sqrMagnitude <= arrivalDistanceSquared)
                {
                    break;
                }

                context.Boss.SetMovementVelocity(offset.normalized * moveSpeed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.Stop();
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
