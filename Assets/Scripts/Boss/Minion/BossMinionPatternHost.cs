using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    // BossMinionPatternHost 컴포넌트는 제거하고, 기존 컴파일 경로에 BossAI 공용 소환수 구현만 남깁니다.
    public abstract partial class BossAI
    {
        [Header("Minions")]
        [SerializeField] private bool minionPatternEnabled = true;
        [FormerlySerializedAs("projectileOrigin")]
        [SerializeField] private Transform minionProjectileOrigin;
        [FormerlySerializedAs("summon")]
        [SerializeField] private MinionSummonSettings minionSummon = new();
        [SerializeField] private bool releaseMinionsOnDisable = true;
        [SerializeField] private bool killSpawnedMinionsOnOwnerDeath = true;

        private readonly List<Minion> minionControlledMinions = new();
        private readonly List<Minion> minionSpawnedMinions = new();
        private readonly BossPatternMovement minionPatternMovement = new();
        private MinionPatternContext minionPatternContext;
        private float nextAutoMinionSummonAt;

        private MinionPatternContext MinionPatternContext => minionPatternContext ??= CreateMinionPatternContext();

        public bool MinionPatternEnabled => minionPatternEnabled;
        public Transform MinionOwnerTransform => transform;
        public Transform MinionTarget => Player;

        public BossProjectileSettings ResolveMinionProjectileSettings(string projectileName)
        {
            if (!minionPatternEnabled)
            {
                return null;
            }

            return ResolveGraphProjectileSettingsForActions(projectileName)
                ?? ResolveGraphProjectileSettingsForActions(null);
        }

        public EnemyProjectile FireMinionProjectile(
            Minion source,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash)
        {
            if (!minionPatternEnabled || IsExecutionPaused || settings == null || settings.Prefab == null)
            {
                return null;
            }

            BulletGauge ownerBullets = source != null ? source.Bullets : Bullets;
            bool canSpawn = source != null ? source.CanSpawnEnemyProjectile() : CanSpawnEnemyProjectile();
            if (!canSpawn)
            {
                return null;
            }

            EnemyProjectile projectile = BossProjectileEmitter.Fire(
                SpawnMinionProjectile,
                settings,
                origin,
                direction,
                settings.ChargingColor,
                settings.LaunchedColor,
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
                ProjectileVfx.PlayMuzzleFlash(origin, direction, settings.LaunchedColor, 0.75f);
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

        public IEnumerator SummonMinions(int summonCount)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

            yield return SummonMinionsForGraph(summonCount);
        }

        public IEnumerator EnsureMinionCount(int targetCount)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

            yield return EnsureMinionCountForGraph(targetCount);
        }

        public IEnumerator AutoSummonIfNeeded()
        {
            if (!minionPatternEnabled || !ShouldRunAutoSummon())
            {
                yield break;
            }

            yield return SummonMinionsForGraph(0);
            ScheduleNextAutoSummon();
        }

        public IEnumerator FireBossBurst(MinionGraphBossBurstRequest request)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

            yield return FireBossBurstForGraph(request);
        }

        public int FireAllMinions(BossProjectileSettings projectile, MinionGraphProjectileFireSpec fireSpec)
        {
            if (!minionPatternEnabled || projectile == null)
            {
                return 0;
            }

            List<Minion> minions = MinionPatternContext.GetControlledMinions();
            int firedCount = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                minion.FireOnce(projectile, fireSpec, i);
                firedCount++;
            }

            return firedCount;
        }

        public int BeginSynchronizedMinionFire(
            BossProjectileSettings projectile,
            int shotCount,
            MinionGraphProjectileFireSpec fireSpec)
        {
            return minionPatternEnabled
                ? MinionPatternContext.BeginSynchronizedMinionFire(projectile, shotCount, fireSpec)
                : 0;
        }

        public IEnumerator WaitSynchronizedMinionFire(int syncVersion)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

            yield return MinionPatternContext.WaitSynchronizedMinionFire(syncVersion);
        }

        public float CommandMinions(MinionGraphCommandRequest request)
        {
            if (!minionPatternEnabled)
            {
                return 0f;
            }

            List<Minion> minions = MinionPatternContext.GetControlledMinions();
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

        public IEnumerator RunOrbitCrossfire(MinionGraphOrbitCrossfireRequest request)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

            if (request.MinimumMinionCount > 0)
            {
                yield return EnsureMinionCountForGraph(request.MinimumMinionCount);
            }

            List<Minion> minions = MinionPatternContext.GetControlledMinions();
            if (minions.Count == 0 || request.OrbitProjectile == null)
            {
                yield break;
            }

            float duration = minions[0].CommandOrbitFire(
                request.OrbitProjectile,
                request.OrbitRadius,
                request.OrbitSeconds,
                request.FireAngleStepDegrees,
                request.Clockwise,
                request.FireSpec);

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
                        request.ResumeIdle,
                        request.FireSpec));
                }
            }

            if (duration > 0f)
            {
                yield return MinionPatternContext.WaitMinionPatternSeconds(duration);
            }
        }

        public IEnumerator WaitForMinionCommands(float timeoutSeconds)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

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

        public void ClearSynchronizedMinionFire()
        {
            minionPatternContext?.ClearSynchronizedMinionFire();
        }

        public void StopAllMinions()
        {
            minionPatternContext?.StopAllMinions();
        }

        public void ResumeAllMinions()
        {
            minionPatternContext?.ResumeAllMinions();
        }

        private void InitializeMinionPatternHost()
        {
            if (!minionPatternEnabled)
            {
                return;
            }

            MinionPatternContext.RefreshControlledMinions();
            ScheduleNextAutoSummon();
        }

        private void DisableMinionPatternHost()
        {
            if (minionPatternContext == null)
            {
                return;
            }

            minionPatternContext.ClearSynchronizedMinionFire();
            minionPatternContext.StopAllMinions();
            if (releaseMinionsOnDisable)
            {
                minionPatternContext.ReleaseMinions();
            }
        }

        private void CancelMinionPatternAction()
        {
            if (minionPatternContext == null)
            {
                return;
            }

            minionPatternContext.ClearSynchronizedMinionFire();
            minionPatternContext.StopAllMinions();
        }

        private void HandleMinionBossPhaseChanged()
        {
            CancelMinionPatternAction();
        }

        private void HandleMinionBossDied()
        {
            if (minionPatternContext == null)
            {
                return;
            }

            minionPatternContext.ClearSynchronizedMinionFire();
            minionPatternContext.StopAllMinions();
            if (killSpawnedMinionsOnOwnerDeath)
            {
                minionPatternContext.KillSpawnedMinions();
            }

            minionPatternContext.ReleaseMinions();
        }

        private IEnumerator SummonMinionsForGraph(int requestedCount)
        {
            yield return MinionPatternContext.WaitWhileExecutionPaused();

            MinionPatternContext.RefreshControlledMinions();
            if (minionSummon.Prefab == null)
            {
                yield break;
            }

            int maxOwned = minionSummon.MaxOwnedMinions;
            int currentCount = MinionPatternContext.GetControlledMinions().Count;
            int summonCount = requestedCount > 0 ? requestedCount : Mathf.Max(1, minionSummon.SummonCount);
            if (maxOwned > 0)
            {
                summonCount = Mathf.Min(summonCount, Mathf.Max(0, maxOwned - currentCount));
            }

            Stop();
            float longestIntro = 0f;
            for (int i = 0; i < summonCount; i++)
            {
                yield return MinionPatternContext.WaitWhileExecutionPaused();

                longestIntro = Mathf.Max(longestIntro, MinionPatternContext.SpawnMinion(i, currentCount + summonCount));
                if (i < summonCount - 1 && minionSummon.SummonInterval > 0f)
                {
                    yield return MinionPatternContext.WaitStoppedSeconds(minionSummon.SummonInterval);
                }
            }

            if (longestIntro > 0f)
            {
                yield return MinionPatternContext.WaitStoppedSeconds(longestIntro);
            }
        }

        private IEnumerator EnsureMinionCountForGraph(int targetCount)
        {
            int safeTargetCount = Mathf.Max(0, targetCount);
            if (safeTargetCount <= 0)
            {
                yield break;
            }

            MinionPatternContext.RefreshControlledMinions();
            while (MinionPatternContext.ControlledMinionCount < safeTargetCount)
            {
                int beforeCount = MinionPatternContext.ControlledMinionCount;
                int missingCount = safeTargetCount - beforeCount;
                yield return SummonMinionsForGraph(missingCount);

                MinionPatternContext.RefreshControlledMinions();
                if (MinionPatternContext.ControlledMinionCount <= beforeCount)
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
                yield return MinionPatternContext.WaitPatternSeconds(request.WindupSeconds);
            }

            int count = Mathf.Max(1, request.BulletCount);
            BossActionContext fireContext = request.Context;
            BossGraphProjectileOriginSpec ownerOrigin = request.BossOrigin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aim = request.Aim ?? new BossGraphProjectileAimSpec();
            for (int i = 0; i < count; i++)
            {
                yield return MinionPatternContext.WaitWhileExecutionPaused();

                Vector3 aimOrigin = fireContext != null ? ownerOrigin.GetAimOrigin(fireContext, i) : GetMinionProjectileOrigin();
                Vector2 direction = fireContext != null ? aim.GetDirection(fireContext, aimOrigin) : GetMinionDirectionToPlayer(aimOrigin);
                Vector3 origin = fireContext != null ? ownerOrigin.GetSpawnOrigin(fireContext, i, direction) : aimOrigin;
                Vector2 finalDirection = fireContext != null ? aim.GetDirection(fireContext, origin) : direction;
                Vector2 side = new(-finalDirection.y, finalDirection.x);
                Vector3 spawnPosition = origin + (Vector3)(side * MinionPatternContext.GetAlternatingOffset(i, request.SpawnSpacing));
                EnemyProjectile firedProjectile = FireOwnerMinionPatternProjectile(
                    request.BossProjectile,
                    spawnPosition,
                    finalDirection,
                    origin,
                    request.Effects == null);
                if (firedProjectile != null && fireContext != null)
                {
                    fireContext.PlayOriginBurst(request.Effects, spawnPosition);
                    fireContext.PlayMuzzleFlashIfEnabled(request.Effects, spawnPosition, finalDirection);
                    fireContext.PlayCameraShakeIfEnabled(request.Effects, finalDirection);
                }

                if (request.NotifyMinions)
                {
                    MinionPatternContext.FireAllMinions(request.MinionProjectile, request.MinionFireSpec);
                }

                MinionPatternContext.TryFireSynchronizedMinions();
                if (i < count - 1 && request.FireInterval > 0f)
                {
                    yield return MinionPatternContext.WaitPatternSeconds(request.FireInterval);
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
                        request.Clockwise,
                        request.FireSpec);
                case MinionGraphCommandMode.RadialBurst:
                    return minion.CommandRadialBurst(
                        request.Projectile,
                        request.RepeatCount,
                        request.DirectionCount,
                        request.FireInterval,
                        request.SpreadDegrees,
                        request.ResumeIdle,
                        request.FireSpec);
                case MinionGraphCommandMode.ChargeSideFire:
                    float sign = index % 2 == 0 ? 1f : -1f;
                    float ring = 1f + index / 2;
                    return minion.CommandChargeSideFire(
                        request.Projectile,
                        request.ChargeSeconds,
                        request.ChargeSpeed,
                        sign * request.AimOffsetDegrees * ring,
                        request.SideFireInterval,
                        request.SideFireAngleDegrees,
                        request.FireSpec);
                case MinionGraphCommandMode.Formation:
                    minion.CommandFormation(
                        MinionPatternContext.GetFormationAngle(index, request.FormationAngleSpacingDegrees),
                        request.FormationRadius,
                        request.FormationSpeedMultiplier);
                    return Mathf.Max(0f, request.SettleSeconds);
                default:
                    return minion.CommandStopAndFire(
                        request.Projectile,
                        request.RepeatCount,
                        request.FireInterval,
                        request.ResumeIdle,
                        request.FireSpec);
            }
        }

        private bool HasCommandedMinions()
        {
            if (minionPatternContext == null)
            {
                return false;
            }

            List<Minion> minions = minionPatternContext.GetControlledMinions();
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
            float min = Mathf.Max(0f, minionSummon.MinAutoSummonInterval);
            float max = Mathf.Max(min, minionSummon.MaxAutoSummonInterval);
            nextAutoMinionSummonAt = Time.time + Random.Range(min, max);
        }

        private bool ShouldRunAutoSummon()
        {
            if (Time.time < nextAutoMinionSummonAt || minionSummon.Prefab == null)
            {
                return false;
            }

            MinionPatternContext.RefreshControlledMinions();
            return minionSummon.MaxOwnedMinions <= 0 || MinionPatternContext.ControlledMinionCount < minionSummon.MaxOwnedMinions;
        }

        private MinionPatternContext CreateMinionPatternContext()
        {
            return new MinionPatternContext(
                this,
                minionSummon,
                minionPatternMovement,
                minionControlledMinions,
                minionSpawnedMinions,
                () => IsExecutionPaused,
                Stop,
                GetMinionProjectileOrigin,
                GetMinionDirectionToPlayer,
                FireOwnerMinionPatternProjectile);
        }

        private EnemyProjectile FireOwnerMinionPatternProjectile(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            Vector3 muzzleOrigin)
        {
            return FireOwnerMinionPatternProjectile(settings, origin, direction, muzzleOrigin, true);
        }

        private EnemyProjectile FireOwnerMinionPatternProjectile(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            Vector3 muzzleOrigin,
            bool playMuzzleFlash)
        {
            if (settings == null || settings.Prefab == null || !CanSpawnEnemyProjectile())
            {
                return null;
            }

            return BossProjectileEmitter.Fire(
                SpawnBossProjectile,
                settings,
                origin,
                direction,
                settings.ChargingColor,
                settings.LaunchedColor,
                settings.AimAtPlayerWhileCharging,
                settings.AimAtPlayerOnLaunch,
                false,
                -1f,
                -1f,
                playMuzzleFlash ? muzzleOrigin : null,
                playMuzzleFlash ? 0.9f : 0f,
                null);
        }

        private Vector3 GetMinionProjectileOrigin()
        {
            if (minionProjectileOrigin != null)
            {
                return minionProjectileOrigin.position;
            }

            return BodyRoot != null ? BodyRoot.position : transform.position;
        }

        private Vector2 GetMinionDirectionToPlayer(Vector3 origin)
        {
            Transform target = MinionTarget;
            if (target == null)
            {
                return Vector2.left;
            }

            Vector2 direction = (Vector2)target.position - (Vector2)origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }
    }
}
