using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorDiamondCollapseAction : BossAction
    {
        private const int DroneCount = 4;

        [Serializable]
        public sealed class VolleyStage
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, Min(0f)] private float fireSeconds = 1f;
            [SerializeField, Min(0f)] private float fireInterval = 0.12f;
            [SerializeField] private bool shrinkAfter;
            [SerializeField, Min(0.01f)] private float nextRadius = 1.5f;
            [SerializeField, Min(0f)] private float firePauseBeforeShrinkSeconds = 0.25f;
            [SerializeField, Min(0.01f)] private float shrinkMoveSeconds = 0.15f;

            public VolleyStage()
            {
            }

            public VolleyStage(bool shrinkAfter)
            {
                this.shrinkAfter = shrinkAfter;
            }

            public string ProjectileName => projectileName?.Trim();
            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public bool ShrinkAfter => shrinkAfter;
            public float NextRadius => Mathf.Max(0.01f, nextRadius);
            public float FirePauseBeforeShrinkSeconds => Mathf.Max(0f, firePauseBeforeShrinkSeconds);
            public float ShrinkMoveSeconds => Mathf.Max(0.01f, shrinkMoveSeconds);
        }

        [Header("Formation")]
        [SerializeField] private Vector2 centerPosition;
        [SerializeField, Min(0.01f)] private float initialRadius = 3f;
        [SerializeField, Min(0.01f)] private float initialAlignSeconds = 0.5f;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField] private BossGraphEffectSettings effects = new();

        [Header("Diamond Volleys")]
        [SerializeField] private List<VolleyStage> volleyStages = new()
        {
            new VolleyStage(true),
            new VolleyStage()
        };

        [Header("Final Center Shot")]
        [SerializeField, BossGraphProjectileName] private string finalProjectileName = "Default";
        [SerializeField, Min(0f)] private float finalShotDelaySeconds = 0.15f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || volleyStages == null
                || volleyStages.Count == 0)
            {
                yield break;
            }

            yield return host.EnsureMinionCount(DroneCount);
            List<Minion> drones = GetDrones(host.GetControlledMinionsForGraph());
            if (drones.Count != DroneCount)
            {
                yield break;
            }

            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);

            float currentRadius = Mathf.Max(0.01f, initialRadius);
            CommandDronesToCardinalSlots(drones, currentRadius, initialAlignSeconds);
            yield return context.WaitSeconds(initialAlignSeconds);

            for (int i = 0; i < volleyStages.Count; i++)
            {
                VolleyStage stage = volleyStages[i];
                if (stage == null)
                {
                    continue;
                }

                yield return FireStage(context, host, drones, stage);
                if (!stage.ShrinkAfter || stage.NextRadius >= currentRadius)
                {
                    continue;
                }

                yield return context.WaitSeconds(stage.FirePauseBeforeShrinkSeconds);
                currentRadius = stage.NextRadius;
                CommandDronesToCardinalSlots(drones, currentRadius, stage.ShrinkMoveSeconds);
                yield return context.WaitSeconds(stage.ShrinkMoveSeconds);
            }

            yield return context.WaitSeconds(finalShotDelaySeconds);
            FireCenterVolley(context, host, drones);
        }

        private IEnumerator FireStage(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones,
            VolleyStage stage)
        {
            BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(stage.ProjectileName);
            if (projectile == null)
            {
                yield break;
            }

            if (stage.FireSeconds <= 0f || stage.FireInterval <= 0f)
            {
                FireDiamondVolley(context, host, drones, projectile);
                yield break;
            }

            float elapsed = 0f;
            float nextFireSeconds = 0f;
            while (elapsed < stage.FireSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                while (elapsed >= nextFireSeconds && nextFireSeconds < stage.FireSeconds)
                {
                    FireDiamondVolley(context, host, drones, projectile);
                    nextFireSeconds += stage.FireInterval;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private void FireDiamondVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones,
            BossProjectileSettings projectile)
        {
            for (int i = 0; i < DroneCount; i++)
            {
                Minion source = drones[i];
                Minion target = drones[(i + 1) % DroneCount];
                if (source == null || target == null)
                {
                    continue;
                }

                Vector3 origin = source.GetGraphProjectileOrigin();
                Vector2 direction = (Vector2)target.GetGraphProjectileOrigin() - (Vector2)origin;
                FireProjectile(context, host, source, projectile, origin, direction);
            }
        }

        private void FireCenterVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones)
        {
            BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(finalProjectileName);
            if (projectile == null)
            {
                return;
            }

            for (int i = 0; i < DroneCount; i++)
            {
                Minion source = drones[i];
                if (source == null)
                {
                    continue;
                }

                Vector3 origin = source.GetGraphProjectileOrigin();
                Vector2 direction = centerPosition - (Vector2)origin;
                FireProjectile(context, host, source, projectile, origin, direction);
            }
        }

        private void CommandDronesToCardinalSlots(
            IReadOnlyList<Minion> drones,
            float radius,
            float moveSeconds)
        {
            for (int i = 0; i < DroneCount; i++)
            {
                Minion minion = drones[i];
                if (minion == null)
                {
                    continue;
                }

                minion.CommandConductorFanBlade(
                    centerPosition,
                    90f * i,
                    radius,
                    moveSeconds,
                    0f,
                    0f);
            }
        }

        private void FireProjectile(
            BossActionContext context,
            IMinionPatternHost host,
            Minion source,
            BossProjectileSettings projectile,
            Vector3 origin,
            Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            EnemyProjectile firedProjectile = host.FireMinionProjectile(
                source,
                projectile,
                origin,
                direction.normalized,
                true);
            if (firedProjectile == null)
            {
                return;
            }

            firedProjectile.ConfigurePathIndicatorSuppressed(true);
            context.PlayOriginBurst(effects, origin);
            context.PlayMuzzleFlashIfEnabled(effects, origin, direction);
            context.PlayCameraShakeIfEnabled(effects, direction);
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
