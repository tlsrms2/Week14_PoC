using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public enum MinionGraphCommandMode
    {
        StopAndFire,
        OrbitFire,
        RadialBurst,
        ChargeSideFire,
        Formation
    }

    public enum MinionGraphProjectileOriginMode
    {
        ProjectileOrigin,
        MinionRoot,
        MinionChild,
        MinionChildList,
        AlternatingMinionChildList,
        AlternatingMinionChildren
    }

    [Serializable]
    public sealed class MinionGraphProjectileOriginSpec
    {
        [SerializeField] private MinionGraphProjectileOriginMode mode;
        [SerializeField] private string minionChildPath;
        [SerializeField] private List<string> minionChildPaths = new();
        [SerializeField] private string firstMinionChildPath;
        [SerializeField] private string secondMinionChildPath;
        [SerializeField, Min(0f)] private float fallbackSpacing = 0.18f;

        public Vector3 GetAimOrigin(Minion minion, int shotIndex)
        {
            if (minion == null)
            {
                return Vector3.zero;
            }

            return mode switch
            {
                MinionGraphProjectileOriginMode.MinionRoot => minion.transform.position,
                MinionGraphProjectileOriginMode.MinionChild => minion.GetGraphChildPosition(minionChildPath),
                MinionGraphProjectileOriginMode.MinionChildList => minion.GetGraphChildPosition(GetListPath(shotIndex, false)),
                MinionGraphProjectileOriginMode.AlternatingMinionChildList => minion.GetGraphChildPosition(GetListPath(shotIndex, true)),
                MinionGraphProjectileOriginMode.AlternatingMinionChildren => minion.GetGraphChildPosition(GetAlternatingPath(shotIndex)),
                _ => minion.GetGraphProjectileOrigin()
            };
        }

        public Vector3 GetSpawnOrigin(Minion minion, int shotIndex, Vector2 direction)
        {
            Vector3 origin = GetAimOrigin(minion, shotIndex);
            if (mode != MinionGraphProjectileOriginMode.ProjectileOrigin || fallbackSpacing <= 0f || shotIndex <= 0)
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
            bool hasFirst = !string.IsNullOrWhiteSpace(firstMinionChildPath);
            bool hasSecond = !string.IsNullOrWhiteSpace(secondMinionChildPath);
            if (!hasFirst)
            {
                return secondMinionChildPath;
            }

            if (!hasSecond)
            {
                return firstMinionChildPath;
            }

            return shotIndex % 2 == 0 ? firstMinionChildPath : secondMinionChildPath;
        }

        private string GetListPath(int shotIndex, bool loop)
        {
            if (minionChildPaths == null || minionChildPaths.Count == 0)
            {
                return minionChildPath;
            }

            int index = loop
                ? Mathf.Abs(shotIndex) % minionChildPaths.Count
                : Mathf.Clamp(shotIndex, 0, minionChildPaths.Count - 1);
            return minionChildPaths[index];
        }
    }

    public readonly struct MinionGraphProjectileFireSpec
    {
        private static readonly MinionGraphProjectileOriginSpec DefaultOrigin = new();
        private static readonly BossGraphProjectileAimSpec DefaultAim = new();

        public MinionGraphProjectileFireSpec(
            MinionGraphProjectileOriginSpec origin,
            BossGraphProjectileAimSpec aim,
            BossGraphEffectSettings effects,
            BossActionContext context)
        {
            Origin = origin;
            Aim = aim;
            Effects = effects;
            Context = context;
        }

        public MinionGraphProjectileOriginSpec Origin { get; }
        public BossGraphProjectileAimSpec Aim { get; }
        public BossGraphEffectSettings Effects { get; }
        public BossActionContext Context { get; }
        public bool HasEffects => Effects != null;

        public Vector3 GetAimOrigin(Minion minion, int shotIndex)
        {
            return (Origin ?? DefaultOrigin).GetAimOrigin(minion, shotIndex);
        }

        public Vector3 GetSpawnOrigin(Minion minion, int shotIndex, Vector2 direction)
        {
            return (Origin ?? DefaultOrigin).GetSpawnOrigin(minion, shotIndex, direction);
        }

        public Vector2 GetDirection(Minion minion, Vector3 origin)
        {
            Func<Vector3, Vector2> getDirectionToPlayer = minion != null ? minion.GetGraphDirectionToPlayer : null;
            return (Aim ?? DefaultAim).GetDirection(getDirectionToPlayer, origin);
        }

        public void PlayEffects(Vector3 origin, Vector2 direction)
        {
            if (Context == null || Effects == null)
            {
                return;
            }

            Context.PlayOriginBurst(Effects, origin);
            Context.PlayMuzzleFlashIfEnabled(Effects, origin, direction);
            Context.PlayCameraShakeIfEnabled(Effects, direction);
        }
    }

    public readonly struct MinionGraphCommandRequest
    {
        public MinionGraphCommandRequest(
            MinionGraphCommandMode mode,
            BossProjectileSettings projectile,
            int repeatCount,
            float fireInterval,
            int directionCount,
            float spreadDegrees,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees,
            float formationRadius,
            float formationAngleSpacingDegrees,
            float formationSpeedMultiplier,
            float settleSeconds,
            MinionGraphProjectileFireSpec fireSpec,
            bool resumeIdle)
        {
            Mode = mode;
            Projectile = projectile;
            RepeatCount = repeatCount;
            FireInterval = fireInterval;
            DirectionCount = directionCount;
            SpreadDegrees = spreadDegrees;
            OrbitRadius = orbitRadius;
            OrbitSeconds = orbitSeconds;
            FireAngleStepDegrees = fireAngleStepDegrees;
            Clockwise = clockwise;
            ChargeSeconds = chargeSeconds;
            ChargeSpeed = chargeSpeed;
            AimOffsetDegrees = aimOffsetDegrees;
            SideFireInterval = sideFireInterval;
            SideFireAngleDegrees = sideFireAngleDegrees;
            FormationRadius = formationRadius;
            FormationAngleSpacingDegrees = formationAngleSpacingDegrees;
            FormationSpeedMultiplier = formationSpeedMultiplier;
            SettleSeconds = settleSeconds;
            FireSpec = fireSpec;
            ResumeIdle = resumeIdle;
        }

        public MinionGraphCommandMode Mode { get; }
        public BossProjectileSettings Projectile { get; }
        public int RepeatCount { get; }
        public float FireInterval { get; }
        public int DirectionCount { get; }
        public float SpreadDegrees { get; }
        public float OrbitRadius { get; }
        public float OrbitSeconds { get; }
        public float FireAngleStepDegrees { get; }
        public bool Clockwise { get; }
        public float ChargeSeconds { get; }
        public float ChargeSpeed { get; }
        public float AimOffsetDegrees { get; }
        public float SideFireInterval { get; }
        public float SideFireAngleDegrees { get; }
        public float FormationRadius { get; }
        public float FormationAngleSpacingDegrees { get; }
        public float FormationSpeedMultiplier { get; }
        public float SettleSeconds { get; }
        public MinionGraphProjectileFireSpec FireSpec { get; }
        public bool ResumeIdle { get; }

        public static MinionGraphCommandRequest StopAndFire(
            BossProjectileSettings projectile,
            int repeatCount,
            float fireInterval,
            MinionGraphProjectileFireSpec fireSpec,
            bool resumeIdle)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.StopAndFire,
                projectile,
                repeatCount,
                fireInterval,
                1,
                0f,
                0.1f,
                0.1f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                0.1f,
                1f,
                1f,
                0f,
                fireSpec,
                resumeIdle);
        }

        public static MinionGraphCommandRequest OrbitFire(
            BossProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            MinionGraphProjectileFireSpec fireSpec,
            bool clockwise)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.OrbitFire,
                projectile,
                1,
                0f,
                1,
                0f,
                orbitRadius,
                orbitSeconds,
                fireAngleStepDegrees,
                clockwise,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                0.1f,
                1f,
                1f,
                0f,
                fireSpec,
                true);
        }

        public static MinionGraphCommandRequest RadialBurst(
            BossProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            MinionGraphProjectileFireSpec fireSpec,
            bool resumeIdle)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.RadialBurst,
                projectile,
                volleyCount,
                volleyInterval,
                directionCount,
                spreadDegrees,
                0.1f,
                0.1f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                0.1f,
                1f,
                1f,
                0f,
                fireSpec,
                resumeIdle);
        }

        public static MinionGraphCommandRequest ChargeSideFire(
            BossProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            MinionGraphProjectileFireSpec fireSpec,
            float sideFireAngleDegrees)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.ChargeSideFire,
                projectile,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                1f,
                false,
                chargeSeconds,
                chargeSpeed,
                aimOffsetDegrees,
                sideFireInterval,
                sideFireAngleDegrees,
                0.1f,
                1f,
                1f,
                0f,
                fireSpec,
                true);
        }

        public static MinionGraphCommandRequest Formation(
            float radius,
            float angleSpacingDegrees,
            float speedMultiplier,
            float settleSeconds)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.Formation,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                radius,
                angleSpacingDegrees,
                speedMultiplier,
                settleSeconds,
                default,
                true);
        }
    }

    public readonly struct MinionGraphBossBurstRequest
    {
        public MinionGraphBossBurstRequest(
            BossProjectileSettings bossProjectile,
            int bulletCount,
            float fireInterval,
            float spawnSpacing,
            float windupSeconds,
            bool notifyMinions,
            BossProjectileSettings minionProjectile,
            BossGraphProjectileOriginSpec bossOrigin,
            BossGraphProjectileAimSpec aim,
            BossGraphEffectSettings effects,
            MinionGraphProjectileFireSpec minionFireSpec,
            BossActionContext context)
        {
            BossProjectile = bossProjectile;
            BulletCount = bulletCount;
            FireInterval = fireInterval;
            SpawnSpacing = spawnSpacing;
            WindupSeconds = windupSeconds;
            NotifyMinions = notifyMinions;
            MinionProjectile = minionProjectile;
            BossOrigin = bossOrigin;
            Aim = aim;
            Effects = effects;
            MinionFireSpec = minionFireSpec;
            Context = context;
        }

        public BossProjectileSettings BossProjectile { get; }
        public int BulletCount { get; }
        public float FireInterval { get; }
        public float SpawnSpacing { get; }
        public float WindupSeconds { get; }
        public bool NotifyMinions { get; }
        public BossProjectileSettings MinionProjectile { get; }
        public BossGraphProjectileOriginSpec BossOrigin { get; }
        public BossGraphProjectileAimSpec Aim { get; }
        public BossGraphEffectSettings Effects { get; }
        public MinionGraphProjectileFireSpec MinionFireSpec { get; }
        public BossActionContext Context { get; }
    }

    public readonly struct MinionGraphOrbitCrossfireRequest
    {
        public MinionGraphOrbitCrossfireRequest(
            BossProjectileSettings orbitProjectile,
            BossProjectileSettings stationaryProjectile,
            int minimumMinionCount,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise,
            int stationaryBulletCount,
            float stationaryFireInterval,
            MinionGraphProjectileFireSpec fireSpec,
            bool resumeIdle)
        {
            OrbitProjectile = orbitProjectile;
            StationaryProjectile = stationaryProjectile;
            MinimumMinionCount = minimumMinionCount;
            OrbitRadius = orbitRadius;
            OrbitSeconds = orbitSeconds;
            FireAngleStepDegrees = fireAngleStepDegrees;
            Clockwise = clockwise;
            StationaryBulletCount = stationaryBulletCount;
            StationaryFireInterval = stationaryFireInterval;
            FireSpec = fireSpec;
            ResumeIdle = resumeIdle;
        }

        public BossProjectileSettings OrbitProjectile { get; }
        public BossProjectileSettings StationaryProjectile { get; }
        public int MinimumMinionCount { get; }
        public float OrbitRadius { get; }
        public float OrbitSeconds { get; }
        public float FireAngleStepDegrees { get; }
        public bool Clockwise { get; }
        public int StationaryBulletCount { get; }
        public float StationaryFireInterval { get; }
        public MinionGraphProjectileFireSpec FireSpec { get; }
        public bool ResumeIdle { get; }
    }

    public interface IMinionPatternHost : IMinionOwner
    {
        bool MinionPatternEnabled { get; }
        BossProjectileSettings ResolveMinionProjectileSettings(string projectileName);
        IEnumerator SummonMinions(int summonCount);
        IEnumerator EnsureMinionCount(int targetCount);
        IEnumerator AutoSummonIfNeeded();
        IEnumerator FireBossBurst(MinionGraphBossBurstRequest request);
        int FireAllMinions(BossProjectileSettings projectile, MinionGraphProjectileFireSpec fireSpec);
        int BeginSynchronizedMinionFire(BossProjectileSettings projectile, int shotCount, MinionGraphProjectileFireSpec fireSpec);
        IEnumerator WaitSynchronizedMinionFire(int syncVersion);
        float CommandMinions(MinionGraphCommandRequest request);
        IEnumerator RunOrbitCrossfire(MinionGraphOrbitCrossfireRequest request);
        IEnumerator WaitForMinionCommands(float timeoutSeconds);
        void ClearSynchronizedMinionFire();
        void StopAllMinions();
        void ResumeAllMinions();
    }
}
