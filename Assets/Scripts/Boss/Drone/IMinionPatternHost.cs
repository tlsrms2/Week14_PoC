using System.Collections;

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
        public bool ResumeIdle { get; }

        public static MinionGraphCommandRequest StopAndFire(
            BossProjectileSettings projectile,
            int repeatCount,
            float fireInterval,
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
                resumeIdle);
        }

        public static MinionGraphCommandRequest OrbitFire(
            BossProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
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
                true);
        }

        public static MinionGraphCommandRequest RadialBurst(
            BossProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
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
                resumeIdle);
        }

        public static MinionGraphCommandRequest ChargeSideFire(
            BossProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
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
            BossProjectileSettings minionProjectile)
        {
            BossProjectile = bossProjectile;
            BulletCount = bulletCount;
            FireInterval = fireInterval;
            SpawnSpacing = spawnSpacing;
            WindupSeconds = windupSeconds;
            NotifyMinions = notifyMinions;
            MinionProjectile = minionProjectile;
        }

        public BossProjectileSettings BossProjectile { get; }
        public int BulletCount { get; }
        public float FireInterval { get; }
        public float SpawnSpacing { get; }
        public float WindupSeconds { get; }
        public bool NotifyMinions { get; }
        public BossProjectileSettings MinionProjectile { get; }
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
        public bool ResumeIdle { get; }
    }

    public interface IMinionPatternHost
    {
        BossProjectileSettings ResolveMinionProjectileSettings(string projectileName);
        IEnumerator SummonMinions(int summonCount);
        IEnumerator EnsureMinionCount(int targetCount);
        IEnumerator AutoSummonIfNeeded();
        IEnumerator FireBossBurst(MinionGraphBossBurstRequest request);
        int FireAllMinions(BossProjectileSettings projectile);
        int BeginSynchronizedMinionFire(BossProjectileSettings projectile, int shotCount);
        IEnumerator WaitSynchronizedMinionFire(int syncVersion);
        float CommandMinions(MinionGraphCommandRequest request);
        IEnumerator RunOrbitCrossfire(MinionGraphOrbitCrossfireRequest request);
        IEnumerator WaitForMinionCommands(float timeoutSeconds);
        void ClearSynchronizedMinionFire();
        void StopAllMinions();
        void ResumeAllMinions();
    }
}
