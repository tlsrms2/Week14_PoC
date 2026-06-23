using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class DronePilot : BossAI
    {
        internal enum PatternKind
        {
            BossBurst,
            SummonDrone,
            DronePattern1,
            DronePattern2,
            DronePattern3,
            DronePattern4,
            DronePattern5
        }

        [Header("Drone Pilot References")]
        [SerializeField] private Transform projectileOrigin;

        [System.Serializable]
        public sealed class ProjectileSettings : BossProjectileSettings
        {
        }

        [System.Serializable]
        internal sealed class BossBurstSettings
        {
            [SerializeField] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("첫 발사 전 대기 시간입니다.")] private float windupSeconds = 0.45f;
            [SerializeField, Min(1), Tooltip("연속 발사 수입니다.")] private int bulletCount = 5;
            [SerializeField, Min(0f), Tooltip("연속 발사 간격입니다.")] private float fireInterval = 0.18f;
            [SerializeField, Min(0f), Tooltip("연속 탄환을 총구 옆으로 번갈아 벌리는 거리입니다.")] private float spawnSpacing = 0.12f;

            public ProjectileSettings Projectile => projectile;
            public float WindupSeconds => windupSeconds;
            public int BulletCount => bulletCount;
            public float FireInterval => fireInterval;
            public float SpawnSpacing => spawnSpacing;
        }

        [System.Serializable]
        internal sealed class SummonSettings
        {
            [SerializeField, Tooltip("소환할 드론 프리팹입니다.")] private Drone prefab;
            [SerializeField, Tooltip("씬에 이미 배치된 소유자 없는 드론도 이 보스가 함께 지휘합니다.")] private bool claimSceneDrones = true;
            [SerializeField, Min(0), Tooltip("소유 드론 최대 수입니다. 0이면 제한하지 않습니다.")] private int maxOwnedDrones = 5;
            [SerializeField, Min(1), Tooltip("소환 패턴 한 번에 생성할 드론 수입니다.")] private int summonCount = 1;
            [SerializeField, Min(0f), Tooltip("보스 주변 소환 반지름입니다.")] private float spawnRadius = 1.2f;
            [SerializeField, Min(0f), Tooltip("드론을 여러 마리 소환할 때 사이 간격입니다.")] private float summonInterval = 0.2f;

            [SerializeField, Min(0f), Tooltip("보스 중심에서 소환 위치까지 이동하며 커지는 시간입니다.")] private float introSeconds = 0.55f;
            [SerializeField, Range(0f, 1f), Tooltip("소환 시작 시 드론 크기 비율입니다.")] private float introStartScale = 0.05f;

            [SerializeField, Min(0f), Tooltip("자동 드론 소환 최소 간격입니다.")] private float minAutoSummonInterval = 4f;
            [SerializeField, Min(0f), Tooltip("자동 드론 소환 최대 간격입니다.")] private float maxAutoSummonInterval = 7f;

            public Drone Prefab => prefab;
            public bool ClaimSceneDrones => claimSceneDrones;
            public int MaxOwnedDrones => maxOwnedDrones;
            public int SummonCount => summonCount;
            public float SpawnRadius => spawnRadius;
            public float SummonInterval => summonInterval;
            public float IntroSeconds => introSeconds;
            public float IntroStartScale => introStartScale;
            public float MinAutoSummonInterval => minAutoSummonInterval;
            public float MaxAutoSummonInterval => maxAutoSummonInterval;
        }

        [System.Serializable]
        internal sealed class DronePattern1Settings
        {
            [SerializeField, Tooltip("켜면 보스 일반 공격 탄환 설정을 드론도 그대로 사용합니다.")] private bool useBossProjectile = true;
            [SerializeField] private ProjectileSettings droneProjectile = new();
            [SerializeField, Min(1), Tooltip("패턴1 동안 각 드론이 발사할 탄환 수입니다.")] private int bulletCount = 6;
            [SerializeField, Min(0f), Tooltip("패턴1 드론 발사 간격입니다.")] private float fireInterval = 0.22f;

            public bool UseBossProjectile => useBossProjectile;
            public ProjectileSettings DroneProjectile => droneProjectile;
            public int BulletCount => bulletCount;
            public float FireInterval => fireInterval;
        }

        [System.Serializable]
        internal sealed class DronePattern2Settings
        {
            [SerializeField] private ProjectileSettings orbitProjectile = new();
            [SerializeField] private ProjectileSettings stationaryProjectile = new();
            [SerializeField, Min(0.1f), Tooltip("회전 드론이 플레이어 주변을 도는 반지름입니다.")] private float orbitRadius = 2.6f;
            [SerializeField, Min(0.1f), Tooltip("한 바퀴 회전에 걸리는 시간입니다.")] private float orbitSeconds = 3f;
            [SerializeField, Min(1f), Tooltip("회전 중 이 각도마다 탄환을 발사합니다.")] private float fireAngleStepDegrees = 30f;
            [SerializeField, Min(1), Tooltip("나머지 드론이 제자리에서 발사할 탄환 수입니다.")] private int stationaryBulletCount = 5;
            [SerializeField, Min(0f), Tooltip("나머지 드론의 제자리 발사 간격입니다.")] private float stationaryFireInterval = 0.25f;

            public ProjectileSettings OrbitProjectile => orbitProjectile;
            public ProjectileSettings StationaryProjectile => stationaryProjectile;
            public float OrbitRadius => orbitRadius;
            public float OrbitSeconds => orbitSeconds;
            public float FireAngleStepDegrees => fireAngleStepDegrees;
            public int StationaryBulletCount => stationaryBulletCount;
            public float StationaryFireInterval => stationaryFireInterval;
        }

        [System.Serializable]
        internal sealed class DronePattern3Settings
        {
            [SerializeField] private ProjectileSettings projectile = new();
            [SerializeField, Min(1), Tooltip("반복 발사 횟수입니다.")] private int volleyCount = 1;
            [SerializeField, Min(1), Tooltip("한 번에 발사할 방향 수입니다.")] private int directionCount = 5;
            [SerializeField, Min(0f), Tooltip("반복 발사 간격입니다.")] private float volleyInterval = 0.35f;
            [SerializeField, Range(0f, 360f), Tooltip("플레이어 방향을 중심으로 퍼질 각도입니다. 0이면 360도입니다.")] private float spreadDegrees = 75f;

            public ProjectileSettings Projectile => projectile;
            public int VolleyCount => volleyCount;
            public int DirectionCount => directionCount;
            public float VolleyInterval => volleyInterval;
            public float SpreadDegrees => spreadDegrees;
        }

        [System.Serializable]
        internal sealed class DronePattern4Settings
        {
            [SerializeField] private ProjectileSettings projectile = new();
            [SerializeField, Min(0.05f), Tooltip("돌진 지속 시간입니다.")] private float chargeSeconds = 1f;
            [SerializeField, Min(0f), Tooltip("돌진 속도입니다.")] private float chargeSpeed = 7f;
            [SerializeField, Range(0f, 85f), Tooltip("플레이어 방향에서 틀어지는 각도입니다.")] private float aimOffsetDegrees = 22f;
            [SerializeField, Min(0.01f), Tooltip("돌진 중 양옆 탄환 발사 간격입니다.")] private float sideFireInterval = 0.18f;
            [SerializeField, Range(1f, 179f), Tooltip("돌진 방향 기준 양옆 발사 각도입니다.")] private float sideFireAngleDegrees = 90f;

            public ProjectileSettings Projectile => projectile;
            public float ChargeSeconds => chargeSeconds;
            public float ChargeSpeed => chargeSpeed;
            public float AimOffsetDegrees => aimOffsetDegrees;
            public float SideFireInterval => sideFireInterval;
            public float SideFireAngleDegrees => sideFireAngleDegrees;
        }

        [System.Serializable]
        internal sealed class DronePattern5Settings
        {
            [SerializeField] private ProjectileSettings bossProjectile = new();
            [SerializeField] private ProjectileSettings droneProjectile = new();
            [SerializeField, Min(1), Tooltip("패턴5 동안 보스가 발사할 탄환 수입니다.")] private int bossBulletCount = 6;
            [SerializeField, Min(0f), Tooltip("패턴5 보스 발사 간격입니다.")] private float bossFireInterval = 0.22f;
            [SerializeField, Min(0.1f), Tooltip("플레이어 기준 드론 대형 반지름입니다.")] private float formationRadius = 2.8f;
            [SerializeField, Min(1f), Tooltip("드론들이 양옆으로 벌어지는 각도입니다.")] private float formationAngleSpacingDegrees = 28f;
            [SerializeField, Min(0f), Tooltip("대형을 잡기 위해 기다리는 시간입니다.")] private float settleSeconds = 1f;
            [SerializeField, Min(0f), Tooltip("대형 이동 속도 배율입니다.")] private float formationSpeedMultiplier = 1.2f;
            [SerializeField, Min(0f), Tooltip("보스 탄환을 총구 옆으로 번갈아 벌리는 거리입니다.")] private float bossSpawnSpacing = 0.1f;

            [SerializeField, Min(1), Tooltip("패턴5 동안 드론이 독립적으로 발사할 횟수입니다.")] private int droneFireCount = 6;
            [SerializeField, Min(0f), Tooltip("패턴5 드론 독립 발사 간격입니다.")] private float droneFireInterval = 0.22f;

            public ProjectileSettings BossProjectile => bossProjectile;
            public ProjectileSettings DroneProjectile => droneProjectile;
            public int BossBulletCount => bossBulletCount;
            public float BossFireInterval => bossFireInterval;
            public int DroneFireCount => droneFireCount;
            public float DroneFireInterval => droneFireInterval;
            public float FormationRadius => formationRadius;
            public float FormationAngleSpacingDegrees => formationAngleSpacingDegrees;
            public float SettleSeconds => settleSeconds;
            public float FormationSpeedMultiplier => formationSpeedMultiplier;
            public float BossSpawnSpacing => bossSpawnSpacing;
        }

        [Header("Drone Pilot Patterns")]
        [SerializeField] private BossBurstSettings bossBurst = new();
        [SerializeField] private SummonSettings summon = new();
        [SerializeField] private DronePattern1Settings dronePattern1 = new();
        [SerializeField] private DronePattern2Settings dronePattern2 = new();
        [SerializeField] private DronePattern3Settings dronePattern3 = new();
        [SerializeField] private DronePattern4Settings dronePattern4 = new();
        [SerializeField] private DronePattern5Settings dronePattern5 = new();
        [SerializeField] private List<PatternKind> patternSequence = new()
        {
            PatternKind.BossBurst,
            PatternKind.SummonDrone,
            PatternKind.DronePattern1,
            PatternKind.DronePattern2,
            PatternKind.DronePattern3,
            PatternKind.DronePattern4,
            PatternKind.DronePattern5
        };
        [SerializeField] private bool randomizePatterns;
        [SerializeField, Min(0f)] private float minPatternRecoverySeconds = 0.45f;
        [SerializeField, Min(0f)] private float maxPatternRecoverySeconds = 0.75f;
        [SerializeField, Tooltip("유도 기능이 꺼진 드론파일럿 투사체 발사 전 색입니다.")] private Color normalProjectileChargeColor = new(0.35f, 0.8f, 1f, 1f);
        [SerializeField, Tooltip("유도 기능이 꺼진 드론파일럿 투사체 발사 후 색입니다.")] private Color normalProjectileColor = new(1f, 0.95f, 0.25f, 1f);
        [SerializeField, Tooltip("유도 기능이 켜진 드론파일럿 투사체 발사 전 색입니다.")] private Color homingProjectileChargeColor = new(0.35f, 0.8f, 1f, 1f);
        [SerializeField, Tooltip("유도 기능이 켜진 드론파일럿 투사체 발사 후 색입니다.")] private Color homingProjectileColor = new(0.35f, 0.75f, 1f, 1f);

        private readonly List<Drone> controlledDrones = new();
        private readonly List<Drone> spawnedDrones = new();
        private readonly DronePatternSelector patternSelector = new();
        private readonly BossPatternMovement patternMovement = new();
        private readonly BossPatternRecovery patternRecovery = new();
        private DronePatternContext patternContext;
        private Coroutine bossPatternRoutine;
        private Coroutine dronePatternRoutine;
        private float nextAutoSummonAt;

        private DronePatternContext PatternContext => patternContext ??= CreatePatternContext();

        protected override void OnBossStarted()
        {
            PatternContext.RefreshControlledDrones();
            ScheduleNextAutoSummon();
        }

        protected override void OnBossTick()
        {
            if (!IsPlayerDetected())
            {
                return;
            }

            if (bossPatternRoutine == null)
            {
                bossPatternRoutine = StartCoroutine(RunBossPatternLoop());
            }

            if (dronePatternRoutine == null)
            {
                dronePatternRoutine = StartCoroutine(RunDronePatternLoop());
            }
        }

        protected override void CancelBossAction()
        {
            if (bossPatternRoutine != null)
            {
                StopCoroutine(bossPatternRoutine);
                bossPatternRoutine = null;
            }

            if (dronePatternRoutine != null)
            {
                StopCoroutine(dronePatternRoutine);
                dronePatternRoutine = null;
            }

            PatternContext.ClearSynchronizedDroneFire();
            PatternContext.StopAllDrones();
        }

        protected override void OnBossDied()
        {
            CancelBossAction();
            PatternContext.KillSpawnedDrones();
            PatternContext.ReleaseDrones();
        }

        public EnemyProjectile FireDroneProjectile(Drone source, ProjectileSettings settings, Vector3 origin, Vector2 direction, bool playMuzzleFlash)
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
                SpawnDroneProjectile,
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

            EnemyProjectile SpawnDroneProjectile(
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

        private PatternKind SelectBossPattern()
        {
            return patternSelector.SelectBossPattern(CreatePatternSelectorSettings());
        }

        private PatternKind SelectDronePattern()
        {
            return patternSelector.SelectDronePattern(CreatePatternSelectorSettings());
        }

        private DronePatternSelector.Settings CreatePatternSelectorSettings()
        {
            return new DronePatternSelector.Settings(patternSequence, randomizePatterns);
        }

        private IEnumerator RunBossPatternLoop()
        {
            PatternKind pattern = SelectBossPattern();
            while (true)
            {
                yield return RunBossPattern(pattern);
                yield return FinishBossPattern();

                PatternKind nextPattern = SelectBossPattern();
                yield return WaitBossPatternRecovery();
                pattern = nextPattern;
            }
        }

        private IEnumerator RunDronePatternLoop()
        {
            PatternKind pattern = SelectDronePattern();
            while (true)
            {
                yield return RunDronePattern(pattern);
                yield return FinishDronePattern();

                PatternKind nextPattern = SelectDronePattern();
                yield return WaitDronePatternRecovery();
                pattern = nextPattern;
            }
        }

        private IEnumerator FinishBossPattern()
        {
            if (ShouldRunAutoSummon())
            {
                yield return RunSummonPattern();
                ScheduleNextAutoSummon();
            }

            // 패턴이 끝난 직후(다음 패턴 시작 전)의 안전 지점에서만 광폭화 진입을 적용합니다.
            yield return ApplyPendingEnrageIfAny();
        }

        private IEnumerator FinishDronePattern()
        {
            // 패턴이 끝난 직후(다음 패턴 시작 전)의 안전 지점에서만 광폭화 진입을 적용합니다.
            yield return ApplyPendingEnrageIfAny();
        }

        private IEnumerator RunBossPattern(PatternKind pattern)
        {
            switch (pattern)
            {
                case PatternKind.SummonDrone:
                    yield return RunSummonPattern();
                    break;
                default:
                    yield return RunBossBurst(false, bossBurst.Projectile, bossBurst.BulletCount, bossBurst.FireInterval, bossBurst.SpawnSpacing, null);
                    break;
            }
        }

        private IEnumerator RunDronePattern(PatternKind pattern)
        {
            switch (pattern)
            {
                case PatternKind.DronePattern1:
                    yield return RunDronePattern1();
                    break;
                case PatternKind.DronePattern2:
                    yield return RunDronePattern2();
                    break;
                case PatternKind.DronePattern3:
                    yield return RunDronePattern3();
                    break;
                case PatternKind.DronePattern4:
                    yield return RunDronePattern4();
                    break;
                case PatternKind.DronePattern5:
                    yield return RunDronePattern5();
                    break;
                default:
                    yield return RunDronePattern1();
                    break;
            }
        }

        private float GetPatternRecoverySeconds()
        {
            return patternRecovery.GetRecoverySeconds(minPatternRecoverySeconds, maxPatternRecoverySeconds);
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

            PatternContext.RefreshControlledDrones();
            return summon.MaxOwnedDrones <= 0 || PatternContext.ControlledDroneCount < summon.MaxOwnedDrones;
        }

        private IEnumerator WaitBossPatternRecovery()
        {
            yield return patternRecovery.RunRecovery(GetPatternRecoverySeconds(), null, () => IsExecutionPaused, Stop);
        }

        private IEnumerator WaitDronePatternRecovery()
        {
            yield return patternRecovery.RunRecovery(GetPatternRecoverySeconds(), null, () => IsExecutionPaused, Stop);
        }

        private IEnumerator RunSummonPattern()
        {
            return DroneSummonRunner.Run(
                summon,
                PatternContext);
        }

        private IEnumerator RunDronePattern1()
        {
            return DronePattern1Runner.Run(
                dronePattern1,
                bossBurst,
                PatternContext);
        }

        private IEnumerator RunDronePattern2()
        {
            return DronePattern2Runner.Run(
                dronePattern2,
                PatternContext);
        }

        private IEnumerator RunDronePattern3()
        {
            return DronePattern3Runner.Run(
                dronePattern3,
                PatternContext);
        }

        private IEnumerator RunDronePattern4()
        {
            return DronePattern4Runner.Run(
                dronePattern4,
                PatternContext);
        }

        private IEnumerator RunDronePattern5()
        {
            return DronePattern5Runner.Run(
                dronePattern5,
                PatternContext);
        }

        private IEnumerator RunBossBurst(
            bool notifyDrones,
            ProjectileSettings bossProjectile,
            int bulletCount,
            float fireInterval,
            float spawnSpacing,
            ProjectileSettings droneProjectile)
        {
            return DroneBossBurstRunner.Run(
                bossBurst,
                notifyDrones,
                bossProjectile,
                bulletCount,
                fireInterval,
                spawnSpacing,
                droneProjectile,
                PatternContext);
        }

        private DronePatternContext CreatePatternContext()
        {
            return new DronePatternContext(
                this,
                summon,
                patternMovement,
                controlledDrones,
                spawnedDrones,
                () => IsExecutionPaused,
                Stop,
                GetProjectileOrigin,
                GetDirectionToPlayer,
                FireBossProjectile);
        }

        private EnemyProjectile FireBossProjectile(ProjectileSettings settings, Vector3 origin, Vector2 direction, Vector3 muzzleOrigin)
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

        private Color GetProjectileColor(ProjectileSettings settings)
        {
            return settings != null && settings.HomingEnabled
                ? homingProjectileColor
                : normalProjectileColor;
        }

        private Color GetProjectileChargeColor(ProjectileSettings settings)
        {
            return settings != null && settings.HomingEnabled
                ? homingProjectileChargeColor
                : normalProjectileChargeColor;
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
