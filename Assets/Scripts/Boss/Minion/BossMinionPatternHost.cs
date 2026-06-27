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

        public IEnumerator SummonMinions(int summonCount, bool stopBossWhileSummoning)
        {
            if (!minionPatternEnabled)
            {
                yield break;
            }

            yield return SummonMinionsForGraph(summonCount, stopBossWhileSummoning);
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

            yield return SummonMinionsForGraph(0, false);
            ScheduleNextAutoSummon();
        }

        public IEnumerator FireMinionsSequentially(
            BossProjectileSettings projectile,
            int cycleCount,
            float fireInterval,
            MinionGraphProjectileFireSpec fireSpec)
        {
            if (!minionPatternEnabled || projectile == null)
            {
                yield break;
            }

            List<Minion> minions = MinionPatternContext.GetControlledMinions();
            int totalShots = CountActiveMinions(minions) * Mathf.Max(1, cycleCount);
            if (totalShots <= 0)
            {
                yield break;
            }

            fireSpec = MinionPatternContext.ResolveSharedMinionAim(fireSpec, minions);
            float safeInterval = Mathf.Max(0f, fireInterval);
            int firedCount = 0;
            for (int cycle = 0; cycle < Mathf.Max(1, cycleCount); cycle++)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    Minion minion = minions[i];
                    if (minion == null)
                    {
                        continue;
                    }

                    yield return MinionPatternContext.WaitWhileExecutionPaused();

                    minion.FireOnce(projectile, fireSpec, firedCount);
                    firedCount++;
                    if (firedCount < totalShots && safeInterval > 0f)
                    {
                        yield return MinionPatternContext.WaitMinionPatternSeconds(safeInterval);
                    }
                }
            }
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

            MinionGraphCommandRequest resolvedRequest = request.WithFireSpec(
                MinionPatternContext.ResolveSharedMinionAim(request.FireSpec, minions));
            float duration = 0f;
            int activeCount = CountActiveMinions(minions);
            float orbitBaseAngle = GetOrbitBaseAngle(minions);
            Vector2 playerPathCenter = MinionTarget != null ? MinionTarget.position : transform.position;
            int commandIndex = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                duration = Mathf.Max(duration, CommandMinion(
                    minion,
                    commandIndex,
                    activeCount,
                    orbitBaseAngle,
                    playerPathCenter,
                    resolvedRequest));
                commandIndex++;
            }

            return duration;
        }

        public float CommandMinionAngleDistanceList(
            IReadOnlyList<MinionGraphAngleDistanceSlot> slots,
            float speedMultiplier,
            float settleSeconds)
        {
            if (!minionPatternEnabled || slots == null || slots.Count == 0)
            {
                return 0f;
            }

            List<Minion> minions = MinionPatternContext.GetControlledMinions();
            int commandIndex = 0;
            float moveSpeed = 24f * Mathf.Max(0f, speedMultiplier);
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                MinionGraphAngleDistanceSlot slot = slots[commandIndex % slots.Count];
                if (slot == null)
                {
                    slot = new MinionGraphAngleDistanceSlot(0f, 2f);
                }

                minion.CommandAngleDistance(slot.AngleDegrees, slot.Distance, moveSpeed);
                commandIndex++;
            }

            return Mathf.Max(0f, settleSeconds);
        }

        public float CommandMinionGather(
            MinionGraphGatherAnchorMode anchorMode,
            float angleDegrees,
            MinionGraphGatherLayout layout,
            float radius,
            float spacing,
            float moveSpeed,
            float settleSeconds)
        {
            if (!minionPatternEnabled || MinionTarget == null)
            {
                return 0f;
            }

            List<Minion> orderedMinions = GetGatherOrderedMinions(
                MinionPatternContext.GetControlledMinions(),
                MinionTarget.position,
                anchorMode);
            if (orderedMinions.Count == 0)
            {
                return 0f;
            }

            float baseAngle = anchorMode == MinionGraphGatherAnchorMode.FixedAngle
                ? angleDegrees
                : GetGatherBaseAngle(orderedMinions[0], MinionTarget.position);
            float safeRadius = Mathf.Max(0.1f, radius);
            float safeSpacing = Mathf.Max(0.1f, spacing);
            float safeMoveSpeed = Mathf.Max(0f, moveSpeed);
            for (int i = 0; i < orderedMinions.Count; i++)
            {
                Vector2 randomOffset = layout == MinionGraphGatherLayout.Random
                    ? GetGatherRandomOffset(safeRadius)
                    : Vector2.zero;
                orderedMinions[i].CommandGather(
                    layout,
                    i,
                    orderedMinions.Count,
                    baseAngle,
                    safeRadius,
                    safeSpacing,
                    randomOffset,
                    safeMoveSpeed);
            }

            return Mathf.Max(0f, settleSeconds);
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

            minionPatternContext.StopAllMinions();
            if (killSpawnedMinionsOnOwnerDeath)
            {
                minionPatternContext.KillSpawnedMinions();
            }

            minionPatternContext.ReleaseMinions();
        }

        private IEnumerator SummonMinionsForGraph(int requestedCount, bool stopBossWhileSummoning)
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

            if (stopBossWhileSummoning)
            {
                Stop();
            }

            List<Minion> spawnedThisAction = new();
            for (int i = 0; i < summonCount; i++)
            {
                yield return MinionPatternContext.WaitWhileExecutionPaused();

                Minion spawnedMinion = MinionPatternContext.SpawnMinion(i, currentCount + summonCount);
                if (spawnedMinion != null)
                {
                    spawnedThisAction.Add(spawnedMinion);
                }

                if (i < summonCount - 1 && minionSummon.SummonInterval > 0f)
                {
                    yield return stopBossWhileSummoning
                        ? MinionPatternContext.WaitStoppedSeconds(minionSummon.SummonInterval)
                        : MinionPatternContext.WaitMinionPatternSeconds(minionSummon.SummonInterval);
                }
            }

            if (spawnedThisAction.Count > 0)
            {
                yield return MinionPatternContext.WaitForSummonIntros(spawnedThisAction, stopBossWhileSummoning);
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
                yield return SummonMinionsForGraph(missingCount, false);

                MinionPatternContext.RefreshControlledMinions();
                if (MinionPatternContext.ControlledMinionCount <= beforeCount)
                {
                    yield break;
                }
            }
        }

        private float CommandMinion(
            Minion minion,
            int index,
            int minionCount,
            float orbitBaseAngle,
            Vector2 playerPathCenter,
            MinionGraphCommandRequest request)
        {
            switch (request.Mode)
            {
                case MinionGraphCommandMode.Orbit:
                    return minion.CommandOrbit(
                        request.OrbitRadius,
                        request.OrbitSeconds,
                        request.Clockwise,
                        request.OrbitMoveSpeed,
                        GetOrbitLineAngleOffset(minion, index, minionCount, request.Clockwise, orbitBaseAngle),
                        request.OrbitUseStartPlayerPosition);
                case MinionGraphCommandMode.Wander:
                    return minion.CommandWander(
                        request.SettleSeconds,
                        request.WanderSpeed,
                        request.WanderRadius,
                        request.WanderRetargetSeconds);
                case MinionGraphCommandMode.RadialBurst:
                    return minion.CommandRadialBurst(
                        request.Projectile,
                        request.RepeatCount,
                        request.DirectionCount,
                        request.FireInterval,
                        request.SpreadDegrees,
                        request.ResumeIdle,
                        request.FireSpec);
                case MinionGraphCommandMode.Dash:
                    float sign = index % 2 == 0 ? 1f : -1f;
                    float ring = 1f + index / 2;
                    return minion.CommandDash(
                        request.ChargeSeconds,
                        request.ChargeSpeed,
                        sign * request.AimOffsetDegrees * ring,
                        request.FireSpec);
                case MinionGraphCommandMode.SideFire:
                    return minion.CommandSideFire(
                        request.Projectile,
                        request.ChargeSeconds,
                        request.SideFireInterval,
                        request.SideFireAngleDegrees,
                        request.SideFireOriginMode,
                        request.SideFireOriginSpacing,
                        request.FireSpec);
                case MinionGraphCommandMode.HoldPosition:
                    return minion.CommandHoldPosition(request.SettleSeconds);
                case MinionGraphCommandMode.FormationCircle:
                    float formationAngle = request.FormationSideBySide
                        ? MinionPatternContext.GetSideBySideFormationAngle(index, request.FormationAngleSpacingDegrees)
                        : MinionPatternContext.GetFormationAngle(index, request.FormationAngleSpacingDegrees);
                    minion.CommandFormationCircle(
                        formationAngle,
                        request.FormationRadius,
                        request.FormationSideBySide,
                        request.FormationMoveSpeed);
                    return Mathf.Max(0f, request.SettleSeconds);
                case MinionGraphCommandMode.FormationStraight:
                    minion.CommandFormationStraight(
                        MinionPatternContext.GetAlternatingOffset(index, request.FormationLineSpacing),
                        request.FormationLineDistance,
                        request.FormationStraightMode,
                        request.FormationMoveSpeed);
                    return Mathf.Max(0f, request.SettleSeconds);
                case MinionGraphCommandMode.PlayerPath:
                    minion.CommandPlayerPath(
                        GetPlayerPathType(request.PlayerPathMode, index),
                        playerPathCenter,
                        request.FormationLineDistance,
                        request.PlayerPathMoveToStartSeconds,
                        request.SettleSeconds);
                    return Mathf.Max(0f, request.PlayerPathMoveToStartSeconds) + Mathf.Max(0f, request.SettleSeconds);
                default:
                    return minion.CommandRepeatFire(
                        request.Projectile,
                        request.RepeatCount,
                        request.FireInterval,
                        request.FireSpec);
            }
        }

        private static MinionGraphPlayerPathType GetPlayerPathType(MinionGraphPlayerPathMode mode, int index)
        {
            int normalizedIndex = Mathf.Abs(index) % 4;
            if (mode == MinionGraphPlayerPathMode.Diagonal)
            {
                return normalizedIndex switch
                {
                    1 => MinionGraphPlayerPathType.DiagonalRightTopToLeftBottom,
                    2 => MinionGraphPlayerPathType.DiagonalRightBottomToLeftTop,
                    3 => MinionGraphPlayerPathType.DiagonalLeftBottomToRightTop,
                    _ => MinionGraphPlayerPathType.DiagonalLeftTopToRightBottom
                };
            }

            return normalizedIndex switch
            {
                1 => MinionGraphPlayerPathType.VerticalTopToBottom,
                2 => MinionGraphPlayerPathType.HorizontalRightToLeft,
                3 => MinionGraphPlayerPathType.VerticalBottomToTop,
                _ => MinionGraphPlayerPathType.HorizontalLeftToRight
            };
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

        private static int CountActiveMinions(IReadOnlyList<Minion> minions)
        {
            if (minions == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<Minion> GetGatherOrderedMinions(
            IReadOnlyList<Minion> minions,
            Vector3 playerPosition,
            MinionGraphGatherAnchorMode anchorMode)
        {
            List<Minion> ordered = new();
            if (minions == null)
            {
                return ordered;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                if (minions[i] != null)
                {
                    ordered.Add(minions[i]);
                }
            }

            ordered.Sort((left, right) =>
                GetSqrDistanceToPlayer(left, playerPosition).CompareTo(GetSqrDistanceToPlayer(right, playerPosition)));
            if (ordered.Count <= 1)
            {
                return ordered;
            }

            int anchorIndex = anchorMode switch
            {
                MinionGraphGatherAnchorMode.FarthestFromPlayer => ordered.Count - 1,
                MinionGraphGatherAnchorMode.MiddleDistanceToPlayer => ordered.Count / 2,
                MinionGraphGatherAnchorMode.FixedAngle => 0,
                _ => 0
            };
            Minion anchor = ordered[anchorIndex];
            ordered.RemoveAt(anchorIndex);
            ordered.Insert(0, anchor);
            return ordered;
        }

        private static float GetGatherBaseAngle(Minion anchor, Vector3 playerPosition)
        {
            if (anchor == null)
            {
                return 0f;
            }

            Vector2 direction = (Vector2)(anchor.transform.position - playerPosition);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static float GetSqrDistanceToPlayer(Minion minion, Vector3 playerPosition)
        {
            return minion != null
                ? ((Vector2)(minion.transform.position - playerPosition)).sqrMagnitude
                : float.PositiveInfinity;
        }

        private static Vector2 GetGatherRandomOffset(float radius)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            float distance = UnityEngine.Random.Range(radius * 0.25f, radius);
            return direction.normalized * distance;
        }

        private float GetOrbitBaseAngle(IReadOnlyList<Minion> minions)
        {
            Transform target = MinionTarget;
            if (target == null || minions == null)
            {
                return 0f;
            }

            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion != null)
                {
                    return GetAngleFromTarget(minion.transform.position, target.position);
                }
            }

            return 0f;
        }

        private float GetOrbitLineAngleOffset(
            Minion minion,
            int index,
            int minionCount,
            bool clockwise,
            float baseAngle)
        {
            if (minionCount <= 1)
            {
                return 0f;
            }

            float step = 360f / minionCount;
            float desiredAngle = baseAngle + step * Mathf.Max(0, index) * (clockwise ? 1f : -1f);
            Transform target = MinionTarget;
            if (target == null || minion == null)
            {
                return desiredAngle - baseAngle;
            }

            float currentAngle = GetAngleFromTarget(minion.transform.position, target.position);
            return Mathf.DeltaAngle(currentAngle, desiredAngle);
        }

        private static float GetAngleFromTarget(Vector3 position, Vector3 targetPosition)
        {
            Vector2 offset = (Vector2)position - (Vector2)targetPosition;
            if (offset.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
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
