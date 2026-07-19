using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorDefenseArcVolleyAction : BossAction, IBossProjectileEmissionAction
    {
        private const int DroneCount = 4;

        [Serializable]
        public sealed class Volley
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, Min(1)] private int repeatCount = 3;
            [SerializeField, Min(0f)] private float fireInterval = 0.15f;
            [SerializeField, Min(0f)] private float restSeconds = 0.25f;
            [SerializeField, BossGraphSfxId] private string fireSfxId;
            [SerializeField, BossGraphSfxId] private string launchSfxId;

            public string ProjectileName => projectileName;
            public int RepeatCount => Mathf.Max(1, repeatCount);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
            public string FireSfxId => fireSfxId;
            public string LaunchSfxId => launchSfxId;
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

        [Header("Drone Shield")]
        [SerializeField] private Color shieldColor = new(0.3f, 0.78f, 1f, 0.82f);
        [SerializeField, Min(0.01f)] private float shieldRadius = 0.45f;
        [SerializeField, Min(0.001f)] private float shieldLineWidth = 0.035f;
        [SerializeField, Range(8, 64)] private int shieldSegments = 24;
        [SerializeField] private int shieldSortingOrder = 72;

        [Header("Volleys")]
        [SerializeField] private List<Volley> volleys = new() { new Volley() };
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphEffectSettings effects = new();

        private static Material shieldMaterial;

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

            BeginDroneProjectileBlocking(drones);
            List<GameObject> shieldVisuals = CreateDroneShieldVisuals(context, drones);
            try
            {
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
            finally
            {
                EndDroneProjectileBlocking(drones);
                ClearDroneShieldVisuals(context, shieldVisuals);
            }
        }

        private static void BeginDroneProjectileBlocking(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.BeginPlayerProjectileBlocking();
            }
        }

        private static void EndDroneProjectileBlocking(IReadOnlyList<Minion> drones)
        {
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.EndPlayerProjectileBlocking();
            }
        }

        private List<GameObject> CreateDroneShieldVisuals(BossActionContext context, IReadOnlyList<Minion> drones)
        {
            List<GameObject> visuals = new(drones.Count);
            for (int i = 0; i < drones.Count; i++)
            {
                Minion drone = drones[i];
                if (drone == null)
                {
                    continue;
                }

                GameObject shield = CreateDroneShieldVisual(drone);
                if (shield == null)
                {
                    continue;
                }

                visuals.Add(shield);
                context.RegisterTransientVisual(shield);
            }

            return visuals;
        }

        private void ClearDroneShieldVisuals(BossActionContext context, IReadOnlyList<GameObject> visuals)
        {
            if (visuals == null)
            {
                return;
            }

            for (int i = 0; i < visuals.Count; i++)
            {
                GameObject shield = visuals[i];
                context.UnregisterTransientVisual(shield);
                if (shield != null)
                {
                    UnityEngine.Object.Destroy(shield);
                }
            }
        }

        private GameObject CreateDroneShieldVisual(Minion drone)
        {
            GameObject shield = new("ConductorDefenseArcShield");
            shield.transform.SetParent(drone.transform, false);

            LineRenderer line = shield.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            line.material = GetShieldMaterial();
            line.startWidth = Mathf.Max(0.001f, shieldLineWidth);
            line.endWidth = Mathf.Max(0.001f, shieldLineWidth);
            line.startColor = shieldColor;
            line.endColor = shieldColor;
            BossSorting.Apply(line);
            line.sortingOrder = shieldSortingOrder;

            int segments = Mathf.Clamp(shieldSegments, 8, 64);
            float radius = Mathf.Max(0.01f, shieldRadius);
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }

            return shield;
        }

        private static Material GetShieldMaterial()
        {
            if (shieldMaterial != null)
            {
                return shieldMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            shieldMaterial = shader != null ? new Material(shader) : null;
            return shieldMaterial;
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
                if (!string.IsNullOrWhiteSpace(volley.FireSfxId) || !string.IsNullOrWhiteSpace(volley.LaunchSfxId))
                {
                    fireSpec = fireSpec.WithOnFired(
                        CreateShotSfxHandler(context, volley.FireSfxId, volley.LaunchSfxId, volley.RepeatCount));
                }

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

        // 매 shotIndex 틱에 드론 4마리가 동시에 쏘더라도, 그중 처음 실제로 발사에 성공한 투사체 하나만
        // 대표로 삼아 사운드가 한 번만 나게 한다. launchSfxId는 그 투사체의 실제 Launched 이벤트에
        // 걸리므로, 발사 직후 파괴되더라도 소리가 나지 않는다.
        private static Action<int, EnemyProjectile> CreateShotSfxHandler(
            BossActionContext context,
            string fireSfxId,
            string launchSfxId,
            int repeatCount)
        {
            bool[] handledShots = new bool[Mathf.Max(1, repeatCount)];
            return (shotIndex, firedProjectile) =>
            {
                if (shotIndex < 0 || shotIndex >= handledShots.Length || handledShots[shotIndex])
                {
                    return;
                }

                handledShots[shotIndex] = true;
                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            };
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
