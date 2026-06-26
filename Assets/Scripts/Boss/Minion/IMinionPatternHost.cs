using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public enum MinionGraphCommandMode
    {
        RepeatFire,
        Orbit,
        RadialBurst,
        Charge,
        SideFire,
        HoldPosition,
        FormationCircle,
        FormationStraight,
        Wander,
        PlayerPath
    }

    public enum MinionGraphFormationStraightMode
    {
        PlayerForward,
        BetweenBossAndPlayer
    }

    public enum MinionGraphPlayerPathType
    {
        Horizontal,
        Vertical,
        LeftToRightDiagonal,
        RightToLeftDiagonal
    }

    public enum MinionGraphSideFireOriginMode
    {
        SharedOrigin,
        BodySides
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
        [SerializeField, BossGraphMinionChildPath] private string minionChildPath;
        [SerializeField, BossGraphMinionChildPath] private List<string> minionChildPaths = new();
        [SerializeField, BossGraphMinionChildPath] private string firstMinionChildPath;
        [SerializeField, BossGraphMinionChildPath] private string secondMinionChildPath;
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
        private readonly Func<Vector2> sharedMinionAimDirectionProvider;

        public MinionGraphProjectileFireSpec(
            MinionGraphProjectileOriginSpec origin,
            BossGraphProjectileAimSpec aim,
            BossGraphEffectSettings effects,
            BossActionContext context)
            : this(origin, aim, effects, context, null)
        {
        }

        private MinionGraphProjectileFireSpec(
            MinionGraphProjectileOriginSpec origin,
            BossGraphProjectileAimSpec aim,
            BossGraphEffectSettings effects,
            BossActionContext context,
            Func<Vector2> sharedMinionAimDirectionProvider)
        {
            Origin = origin;
            Aim = aim;
            Effects = effects;
            Context = context;
            this.sharedMinionAimDirectionProvider = sharedMinionAimDirectionProvider;
        }

        public MinionGraphProjectileOriginSpec Origin { get; }
        public BossGraphProjectileAimSpec Aim { get; }
        public BossGraphEffectSettings Effects { get; }
        public BossActionContext Context { get; }
        public bool HasEffects => Effects != null;
        public bool UsesClosestMinionAim => (Aim ?? DefaultAim).Mode == BossGraphProjectileAimMode.ClosestMinionToPlayer;

        public MinionGraphProjectileFireSpec WithSharedMinionAimDirectionProvider(Func<Vector2> directionProvider)
        {
            if (directionProvider == null)
            {
                return this;
            }

            return new MinionGraphProjectileFireSpec(
                Origin,
                Aim,
                Effects,
                Context,
                directionProvider);
        }

        public bool TryGetSharedMinionAimDirection(out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!UsesClosestMinionAim || sharedMinionAimDirectionProvider == null)
            {
                return false;
            }

            direction = sharedMinionAimDirectionProvider();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.zero;
                return false;
            }

            direction.Normalize();
            return true;
        }

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
            if (TryGetSharedMinionAimDirection(out Vector2 sharedDirection))
            {
                return sharedDirection;
            }

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
        private const float DefaultFormationMoveSpeed = 24f;

        public MinionGraphCommandRequest(
            MinionGraphCommandMode mode,
            BossProjectileSettings projectile,
            int repeatCount,
            float fireInterval,
            int directionCount,
            float spreadDegrees,
            float orbitRadius,
            float orbitSeconds,
            float orbitMoveSpeed,
            float fireAngleStepDegrees,
            bool clockwise,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees,
            MinionGraphSideFireOriginMode sideFireOriginMode,
            float sideFireOriginSpacing,
            float formationRadius,
            bool formationSideBySide,
            float formationAngleSpacingDegrees,
            MinionGraphFormationStraightMode formationStraightMode,
            float formationLineDistance,
            float formationLineSpacing,
            float formationMoveSpeed,
            float playerPathMoveToStartSeconds,
            float wanderSpeed,
            float wanderRadius,
            float wanderRetargetSeconds,
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
            OrbitMoveSpeed = orbitMoveSpeed;
            FireAngleStepDegrees = fireAngleStepDegrees;
            Clockwise = clockwise;
            ChargeSeconds = chargeSeconds;
            ChargeSpeed = chargeSpeed;
            AimOffsetDegrees = aimOffsetDegrees;
            SideFireInterval = sideFireInterval;
            SideFireAngleDegrees = sideFireAngleDegrees;
            SideFireOriginMode = sideFireOriginMode;
            SideFireOriginSpacing = sideFireOriginSpacing;
            FormationRadius = formationRadius;
            FormationSideBySide = formationSideBySide;
            FormationAngleSpacingDegrees = formationAngleSpacingDegrees;
            FormationStraightMode = formationStraightMode;
            FormationLineDistance = formationLineDistance;
            FormationLineSpacing = formationLineSpacing;
            FormationMoveSpeed = formationMoveSpeed;
            PlayerPathMoveToStartSeconds = playerPathMoveToStartSeconds;
            WanderSpeed = wanderSpeed;
            WanderRadius = wanderRadius;
            WanderRetargetSeconds = wanderRetargetSeconds;
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
        public float OrbitMoveSpeed { get; }
        public float FireAngleStepDegrees { get; }
        public bool Clockwise { get; }
        public float ChargeSeconds { get; }
        public float ChargeSpeed { get; }
        public float AimOffsetDegrees { get; }
        public float SideFireInterval { get; }
        public float SideFireAngleDegrees { get; }
        public MinionGraphSideFireOriginMode SideFireOriginMode { get; }
        public float SideFireOriginSpacing { get; }
        public float FormationRadius { get; }
        public bool FormationSideBySide { get; }
        public float FormationAngleSpacingDegrees { get; }
        public MinionGraphFormationStraightMode FormationStraightMode { get; }
        public float FormationLineDistance { get; }
        public float FormationLineSpacing { get; }
        public float FormationMoveSpeed { get; }
        public float PlayerPathMoveToStartSeconds { get; }
        public float WanderSpeed { get; }
        public float WanderRadius { get; }
        public float WanderRetargetSeconds { get; }
        public float SettleSeconds { get; }
        public MinionGraphProjectileFireSpec FireSpec { get; }
        public bool ResumeIdle { get; }

        public MinionGraphCommandRequest WithFireSpec(MinionGraphProjectileFireSpec fireSpec)
        {
            return new MinionGraphCommandRequest(
                Mode,
                Projectile,
                RepeatCount,
                FireInterval,
                DirectionCount,
                SpreadDegrees,
                OrbitRadius,
                OrbitSeconds,
                OrbitMoveSpeed,
                FireAngleStepDegrees,
                Clockwise,
                ChargeSeconds,
                ChargeSpeed,
                AimOffsetDegrees,
                SideFireInterval,
                SideFireAngleDegrees,
                SideFireOriginMode,
                SideFireOriginSpacing,
                FormationRadius,
                FormationSideBySide,
                FormationAngleSpacingDegrees,
                FormationStraightMode,
                FormationLineDistance,
                FormationLineSpacing,
                FormationMoveSpeed,
                PlayerPathMoveToStartSeconds,
                WanderSpeed,
                WanderRadius,
                WanderRetargetSeconds,
                SettleSeconds,
                fireSpec,
                ResumeIdle);
        }

        public static MinionGraphCommandRequest RepeatFire(
            BossProjectileSettings projectile,
            int repeatCount,
            float fireInterval,
            MinionGraphProjectileFireSpec fireSpec)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.RepeatFire,
                projectile,
                repeatCount,
                fireInterval,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                0f,
                0.1f,
                0.1f,
                0f,
                fireSpec,
                true);
        }

        public static MinionGraphCommandRequest Orbit(
            float orbitRadius,
            float orbitSeconds,
            float orbitMoveSpeed,
            bool clockwise)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.Orbit,
                null,
                1,
                0f,
                1,
                0f,
                orbitRadius,
                orbitSeconds,
                orbitMoveSpeed,
                1f,
                clockwise,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                0f,
                0.1f,
                0.1f,
                0f,
                default,
                true);
        }

        public static MinionGraphCommandRequest Wander(
            float wanderSeconds,
            float wanderSpeed,
            float wanderRadius,
            float wanderRetargetSeconds)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.Wander,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                wanderSpeed,
                wanderRadius,
                wanderRetargetSeconds,
                wanderSeconds,
                default,
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
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                0f,
                0.1f,
                0.1f,
                0f,
                fireSpec,
                resumeIdle);
        }

        public static MinionGraphCommandRequest Charge(
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            MinionGraphProjectileFireSpec aimSpec)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.Charge,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                chargeSeconds,
                chargeSpeed,
                aimOffsetDegrees,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                0f,
                0.1f,
                0.1f,
                0f,
                aimSpec,
                true);
        }

        public static MinionGraphCommandRequest SideFire(
            BossProjectileSettings projectile,
            float fireSeconds,
            float fireInterval,
            MinionGraphProjectileFireSpec fireSpec,
            float sideFireAngleDegrees,
            MinionGraphSideFireOriginMode sideFireOriginMode,
            float sideFireOriginSpacing)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.SideFire,
                projectile,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                fireSeconds,
                0f,
                0f,
                fireInterval,
                sideFireAngleDegrees,
                sideFireOriginMode,
                sideFireOriginSpacing,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                0f,
                0.1f,
                0.1f,
                0f,
                fireSpec,
                true);
        }

        public static MinionGraphCommandRequest HoldPosition(float holdSeconds)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.HoldPosition,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                1f,
                0f,
                0f,
                0.1f,
                0.1f,
                holdSeconds,
                default,
                true);
        }

        public static MinionGraphCommandRequest FormationCircle(
            float radius,
            bool sideBySide,
            float angleSpacingDegrees,
            float speedMultiplier,
            float settleSeconds)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.FormationCircle,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                radius,
                sideBySide,
                angleSpacingDegrees,
                MinionGraphFormationStraightMode.PlayerForward,
                2f,
                0.7f,
                DefaultFormationMoveSpeed * Mathf.Max(0f, speedMultiplier),
                0f,
                0f,
                0.1f,
                0.1f,
                settleSeconds,
                default,
                true);
        }

        public static MinionGraphCommandRequest FormationStraight(
            MinionGraphFormationStraightMode straightMode,
            float distanceFromPlayer,
            float lineSpacing,
            float speedMultiplier,
            float settleSeconds)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.FormationStraight,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                straightMode,
                distanceFromPlayer,
                lineSpacing,
                DefaultFormationMoveSpeed * Mathf.Max(0f, speedMultiplier),
                0f,
                0f,
                0.1f,
                0.1f,
                settleSeconds,
                default,
                true);
        }

        public static MinionGraphCommandRequest PlayerPath(
            float distanceFromPlayer,
            float moveToStartSeconds,
            float moveSeconds)
        {
            return new MinionGraphCommandRequest(
                MinionGraphCommandMode.PlayerPath,
                null,
                1,
                0f,
                1,
                0f,
                0.1f,
                0.1f,
                0f,
                1f,
                false,
                0.05f,
                0f,
                0f,
                0.01f,
                90f,
                MinionGraphSideFireOriginMode.SharedOrigin,
                0.35f,
                0.1f,
                false,
                1f,
                MinionGraphFormationStraightMode.PlayerForward,
                distanceFromPlayer,
                0.7f,
                0f,
                moveToStartSeconds,
                0f,
                0.1f,
                0.1f,
                moveSeconds,
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
        IEnumerator WaitForMinionCommands(float timeoutSeconds);
        void ClearSynchronizedMinionFire();
        void StopAllMinions();
        void ResumeAllMinions();
    }
}
