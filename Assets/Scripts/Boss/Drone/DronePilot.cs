using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class DronePilot : BossAI, IMinionPatternHost
    {
        [Header("Drone Pilot References")]
        [SerializeField] private Transform projectileOrigin;

        [Header("Boss Graph")]
        [SerializeField] private BossGraphAsset bossGraph;
        [SerializeField] private List<BossGraphProjectileEntry> graphProjectiles = new()
        {
            new BossGraphProjectileEntry()
        };

        [Header("Drone Pilot Minions")]
        [SerializeField] private MinionSummonSettings summon = new();

        private readonly List<Minion> controlledMinions = new();
        private readonly List<Minion> spawnedMinions = new();
        private readonly BossPatternMovement patternMovement = new();
        private readonly BossGraphRunner graphRunner = new();
        private MinionPatternContext patternContext;
        private BossActionContext graphContext;
        private Coroutine patternRoutine;
        private float nextAutoSummonAt;

        private MinionPatternContext PatternContext => patternContext ??= CreatePatternContext();
        protected override BossGraphAsset GraphAsset => bossGraph;
        public Transform MinionOwnerTransform => transform;
        public Transform MinionTarget => Player;

        protected override void OnBossStarted()
        {
            PatternContext.RefreshControlledMinions();
            ScheduleNextAutoSummon();
        }

        protected override BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            if (graphProjectiles == null || graphProjectiles.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(projectileName))
            {
                for (int i = 0; i < graphProjectiles.Count; i++)
                {
                    BossGraphProjectileEntry entry = graphProjectiles[i];
                    if (entry != null
                        && string.Equals(entry.ProjectileName, projectileName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Projectile;
                    }
                }

                return null;
            }

            return graphProjectiles[0]?.Projectile;
        }

        protected override void OnBossTick()
        {
            if (!IsPlayerDetected())
            {
                return;
            }

            if (bossGraph == null)
            {
                return;
            }

            if (patternRoutine == null)
            {
                patternRoutine = StartCoroutine(RunGraphPatternLoop());
            }
        }

        protected override void CancelBossAction()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            BossGraphRuntimeState.Clear(bossGraph);
            graphRunner.Reset();
            graphContext?.ResetBodyRootLocalOffset();
            graphContext = null;
            PatternContext.ClearSynchronizedMinionFire();
            PatternContext.StopAllMinions();
        }

        protected override void OnBossDied()
        {
            CancelBossAction();
            PatternContext.KillSpawnedMinions();
            PatternContext.ReleaseMinions();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            BossGraphRuntimeState.Clear(bossGraph);
            graphRunner.Reset();
            PatternContext.ClearSynchronizedMinionFire();
            PatternContext.StopAllMinions();
        }

        public EnemyProjectile FireMinionProjectile(Minion source, BossProjectileSettings settings, Vector3 origin, Vector2 direction, bool playMuzzleFlash)
        {
            if (IsExecutionPaused)
            {
                return null;
            }

            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            BulletGauge ownerBullets = source != null ? source.Bullets : Bullets;
            bool canSpawn = source != null ? source.CanSpawnEnemyProjectile() : CanSpawnEnemyProjectile();
            if (!canSpawn)
            {
                return null;
            }

            Color chargeColor = GetProjectileChargeColor(settings);
            Color projectileColor = GetProjectileColor(settings);
            EnemyProjectile projectile = BossProjectileEmitter.Fire(
                SpawnMinionProjectile,
                settings,
                origin,
                direction,
                chargeColor,
                projectileColor,
                settings.AimAtPlayerWhileCharging,
                settings.AimAtPlayerOnLaunch,
                false,
                -1f,
                -1f,
                null,
                0f,
                null);

            if (projectile != null && playMuzzleFlash)
            {
                ProjectileVfx.PlayMuzzleFlash(origin, direction, projectileColor, 0.75f);
            }

            return projectile;

            EnemyProjectile SpawnMinionProjectile(
                EnemyProjectile prefab,
                Vector3 position,
                Vector2 projectileDirection,
                int projectileBulletDamage,
                float chargeSeconds,
                float speed,
                float lifetime,
                float radius,
                Color color,
                float trailSeconds,
                float trailWidth,
                bool homingEnabled,
                float homingSeconds,
                float homingTurnDegrees,
                Vector3? muzzleFlashPosition,
                float muzzleFlashScale)
            {
                return EnemyProjectile.Spawn(
                    prefab,
                    ownerBullets,
                    position,
                    projectileDirection,
                    projectileBulletDamage,
                    chargeSeconds,
                    speed,
                    lifetime,
                    radius,
                    color,
                    trailSeconds,
                    trailWidth,
                    homingEnabled,
                    homingSeconds,
                    homingTurnDegrees);
            }
        }

        private IEnumerator RunGraphPatternLoop()
        {
            graphRunner.Reset();
            graphContext = CreateGraphContext();
            yield return graphRunner.RunLoop(bossGraph, graphContext);
            graphContext = null;
            patternRoutine = null;
        }

        private BossActionContext CreateGraphContext()
        {
            return new BossActionContext(
                this,
                Stop,
                () => IsExecutionPaused);
        }

        BossProjectileSettings IMinionPatternHost.ResolveMinionProjectileSettings(string projectileName)
        {
            return ResolveGraphProjectileSettings(projectileName) ?? ResolveGraphProjectileSettings(null);
        }

        IEnumerator IMinionPatternHost.SummonMinions(int summonCount)
        {
            yield return SummonMinionsForGraph(summonCount);
        }

        IEnumerator IMinionPatternHost.EnsureMinionCount(int targetCount)
        {
            yield return EnsureMinionCountForGraph(targetCount);
        }

        IEnumerator IMinionPatternHost.AutoSummonIfNeeded()
        {
            if (!ShouldRunAutoSummon())
            {
                yield break;
            }

            yield return SummonMinionsForGraph(0);
            ScheduleNextAutoSummon();
        }

        IEnumerator IMinionPatternHost.FireBossBurst(MinionGraphBossBurstRequest request)
        {
            yield return FireBossBurstForGraph(request);
        }

        int IMinionPatternHost.FireAllMinions(BossProjectileSettings projectile)
        {
            if (projectile == null)
            {
                return 0;
            }

            List<Minion> minions = PatternContext.GetControlledMinions();
            int firedCount = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                minion.FireOnceAtPlayer(projectile);
                firedCount++;
            }

            return firedCount;
        }

        int IMinionPatternHost.BeginSynchronizedMinionFire(BossProjectileSettings projectile, int shotCount)
        {
            return PatternContext.BeginSynchronizedMinionFire(projectile, shotCount);
        }

        IEnumerator IMinionPatternHost.WaitSynchronizedMinionFire(int syncVersion)
        {
            yield return PatternContext.WaitSynchronizedMinionFire(syncVersion);
        }

        float IMinionPatternHost.CommandMinions(MinionGraphCommandRequest request)
        {
            List<Minion> minions = PatternContext.GetControlledMinions();
            if (minions.Count == 0)
            {
                return 0f;
            }

            float duration = 0f;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                duration = Mathf.Max(duration, CommandMinion(minion, i, request));
            }

            return duration;
        }

        IEnumerator IMinionPatternHost.RunOrbitCrossfire(MinionGraphOrbitCrossfireRequest request)
        {
            if (request.MinimumMinionCount > 0)
            {
                yield return EnsureMinionCountForGraph(request.MinimumMinionCount);
            }

            List<Minion> minions = PatternContext.GetControlledMinions();
            if (minions.Count == 0 || request.OrbitProjectile == null)
            {
                yield break;
            }

            float duration = minions[0].CommandOrbitFire(
                request.OrbitProjectile,
                request.OrbitRadius,
                request.OrbitSeconds,
                request.FireAngleStepDegrees,
                request.Clockwise);

            if (request.StationaryProjectile != null)
            {
                for (int i = 1; i < minions.Count; i++)
                {
                    Minion minion = minions[i];
                    if (minion == null)
                    {
                        continue;
                    }

                    duration = Mathf.Max(duration, minion.CommandStopAndFire(
                        request.StationaryProjectile,
                        request.StationaryBulletCount,
                        request.StationaryFireInterval,
                        request.ResumeIdle));
                }
            }

            if (duration > 0f)
            {
                yield return PatternContext.WaitMinionPatternSeconds(duration);
            }
        }

        IEnumerator IMinionPatternHost.WaitForMinionCommands(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (HasCommandedMinions())
            {
                if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
                {
                    yield break;
                }

                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        void IMinionPatternHost.ClearSynchronizedMinionFire()
        {
            PatternContext.ClearSynchronizedMinionFire();
        }

        void IMinionPatternHost.StopAllMinions()
        {
            PatternContext.StopAllMinions();
        }

        void IMinionPatternHost.ResumeAllMinions()
        {
            PatternContext.ResumeAllMinions();
        }

        private IEnumerator SummonMinionsForGraph(int requestedCount)
        {
            yield return PatternContext.WaitWhileExecutionPaused();

            PatternContext.RefreshControlledMinions();
            if (summon.Prefab == null)
            {
                yield break;
            }

            int maxOwned = summon.MaxOwnedMinions;
            int currentCount = PatternContext.GetControlledMinions().Count;
            int summonCount = requestedCount > 0 ? requestedCount : Mathf.Max(1, summon.SummonCount);
            if (maxOwned > 0)
            {
                summonCount = Mathf.Min(summonCount, Mathf.Max(0, maxOwned - currentCount));
            }

            Stop();
            float longestIntro = 0f;
            for (int i = 0; i < summonCount; i++)
            {
                yield return PatternContext.WaitWhileExecutionPaused();

                longestIntro = Mathf.Max(longestIntro, PatternContext.SpawnMinion(i, currentCount + summonCount));
                if (i < summonCount - 1 && summon.SummonInterval > 0f)
                {
                    yield return PatternContext.WaitStoppedSeconds(summon.SummonInterval);
                }
            }

            if (longestIntro > 0f)
            {
                yield return PatternContext.WaitStoppedSeconds(longestIntro);
            }
        }

        private IEnumerator EnsureMinionCountForGraph(int targetCount)
        {
            int safeTargetCount = Mathf.Max(0, targetCount);
            if (safeTargetCount <= 0)
            {
                yield break;
            }

            PatternContext.RefreshControlledMinions();
            while (PatternContext.ControlledMinionCount < safeTargetCount)
            {
                int beforeCount = PatternContext.ControlledMinionCount;
                int missingCount = safeTargetCount - beforeCount;
                yield return SummonMinionsForGraph(missingCount);

                PatternContext.RefreshControlledMinions();
                if (PatternContext.ControlledMinionCount <= beforeCount)
                {
                    yield break;
                }
            }
        }

        private IEnumerator FireBossBurstForGraph(MinionGraphBossBurstRequest request)
        {
            if (request.BossProjectile == null)
            {
                yield break;
            }

            if (request.WindupSeconds > 0f)
            {
                yield return PatternContext.WaitPatternSeconds(request.WindupSeconds);
            }

            int count = Mathf.Max(1, request.BulletCount);
            for (int i = 0; i < count; i++)
            {
                yield return PatternContext.WaitWhileExecutionPaused();

                Vector3 origin = GetProjectileOrigin();
                Vector2 direction = GetDirectionToPlayer(origin);
                Vector2 side = new(-direction.y, direction.x);
                Vector3 spawnPosition = origin + (Vector3)(side * PatternContext.GetAlternatingOffset(i, request.SpawnSpacing));
                FireBossProjectile(request.BossProjectile, spawnPosition, direction, origin);

                if (request.NotifyMinions)
                {
                    PatternContext.FireAllMinions(request.MinionProjectile);
                }

                PatternContext.TryFireSynchronizedMinions();
                if (i < count - 1 && request.FireInterval > 0f)
                {
                    yield return PatternContext.WaitPatternSeconds(request.FireInterval);
                }
            }
        }

        private float CommandMinion(Minion minion, int index, MinionGraphCommandRequest request)
        {
            switch (request.Mode)
            {
                case MinionGraphCommandMode.OrbitFire:
                    return minion.CommandOrbitFire(
                        request.Projectile,
                        request.OrbitRadius,
                        request.OrbitSeconds,
                        request.FireAngleStepDegrees,
                        request.Clockwise);
                case MinionGraphCommandMode.RadialBurst:
                    return minion.CommandRadialBurst(
                        request.Projectile,
                        request.RepeatCount,
                        request.DirectionCount,
                        request.FireInterval,
                        request.SpreadDegrees,
                        request.ResumeIdle);
                case MinionGraphCommandMode.ChargeSideFire:
                    float sign = index % 2 == 0 ? 1f : -1f;
                    float ring = 1f + index / 2;
                    return minion.CommandChargeSideFire(
                        request.Projectile,
                        request.ChargeSeconds,
                        request.ChargeSpeed,
                        sign * request.AimOffsetDegrees * ring,
                        request.SideFireInterval,
                        request.SideFireAngleDegrees);
                case MinionGraphCommandMode.Formation:
                    minion.CommandFormation(
                        PatternContext.GetFormationAngle(index, request.FormationAngleSpacingDegrees),
                        request.FormationRadius,
                        request.FormationSpeedMultiplier);
                    return Mathf.Max(0f, request.SettleSeconds);
                default:
                    return minion.CommandStopAndFire(
                        request.Projectile,
                        request.RepeatCount,
                        request.FireInterval,
                        request.ResumeIdle);
            }
        }

        private bool HasCommandedMinions()
        {
            List<Minion> minions = PatternContext.GetControlledMinions();
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i] != null && minions[i].IsCommanded)
                {
                    return true;
                }
            }

            return false;
        }

        private void ScheduleNextAutoSummon()
        {
            float min = Mathf.Max(0f, summon.MinAutoSummonInterval);
            float max = Mathf.Max(min, summon.MaxAutoSummonInterval);
            nextAutoSummonAt = Time.time + Random.Range(min, max);
        }

        private bool ShouldRunAutoSummon()
        {
            if (Time.time < nextAutoSummonAt || summon.Prefab == null)
            {
                return false;
            }

            PatternContext.RefreshControlledMinions();
            return summon.MaxOwnedMinions <= 0 || PatternContext.ControlledMinionCount < summon.MaxOwnedMinions;
        }

        private MinionPatternContext CreatePatternContext()
        {
            return new MinionPatternContext(
                this,
                summon,
                patternMovement,
                controlledMinions,
                spawnedMinions,
                () => IsExecutionPaused,
                Stop,
                GetProjectileOrigin,
                GetDirectionToPlayer,
                FireBossProjectile);
        }

        private EnemyProjectile FireBossProjectile(BossProjectileSettings settings, Vector3 origin, Vector2 direction, Vector3 muzzleOrigin)
        {
            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            Color chargeColor = GetProjectileChargeColor(settings);
            Color projectileColor = GetProjectileColor(settings);
            return BossProjectileEmitter.Fire(
                SpawnBossProjectile,
                settings,
                origin,
                direction,
                chargeColor,
                projectileColor,
                settings.AimAtPlayerWhileCharging,
                settings.AimAtPlayerOnLaunch,
                false,
                -1f,
                -1f,
                muzzleOrigin,
                0.9f,
                null);
        }

        private Color GetProjectileColor(BossProjectileSettings settings)
        {
            return settings != null ? settings.LaunchedColor : Color.white;
        }

        private Color GetProjectileChargeColor(BossProjectileSettings settings)
        {
            return settings != null ? settings.ChargingColor : Color.white;
        }

        private Vector3 GetProjectileOrigin()
        {
            if (projectileOrigin != null)
            {
                return projectileOrigin.position;
            }

            return BodyRoot != null ? BodyRoot.position : transform.position;
        }
    }
}
