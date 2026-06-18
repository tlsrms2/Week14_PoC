using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class DronePilot : BossAI
    {
        private enum PatternKind
        {
            BossBurst,
            SummonDrone,
            DronePattern1,
            DronePattern2,
            DronePattern3,
            DronePattern4,
            DronePattern5
        }

        [System.Serializable]
        public sealed class ProjectileSettings
        {
            [SerializeField, Tooltip("발사할 적 탄환 프리팹입니다.")] private EnemyProjectile prefab;
            [SerializeField, Min(0), Tooltip("플레이어에게 적중했을 때 감소시킬 플레이어 탄환 수입니다.")] private int bulletDamage = 1;
            [SerializeField, Min(0f), Tooltip("발사 전 충전 시간입니다.")] private float chargeSeconds = 0.15f;
            [SerializeField, Min(0f), Tooltip("충전 중 탄환이 천천히 움직이는 속도입니다.")] private float chargeDriftSpeed = 0.25f;
            [SerializeField, Tooltip("충전 중 플레이어 방향을 계속 갱신합니다.")] private bool aimAtPlayerWhileCharging = true;
            [SerializeField, Tooltip("발사 순간 플레이어 방향으로 다시 조준합니다.")] private bool aimAtPlayerOnLaunch;
            [SerializeField, Min(0f), Tooltip("탄환 이동 속도입니다.")] private float speed = 7f;
            [SerializeField, Min(0f), Tooltip("탄환 수명입니다.")] private float lifetime = 3f;
            [SerializeField, Min(0.01f), Tooltip("탄환 충돌 반지름입니다.")] private float radius = 0.12f;
            [SerializeField, Tooltip("충전 중 색입니다.")] private Color chargingColor = new(0.35f, 0.8f, 1f, 1f);
            [SerializeField, Tooltip("발사 후 색입니다.")] private Color launchedColor = new(1f, 0.95f, 0.25f, 1f);
            [SerializeField, Min(0.01f), Tooltip("탄환 궤적 유지 시간입니다.")] private float trailSeconds = 0.1f;
            [SerializeField, Min(0.1f), Tooltip("탄환 궤적 두께 배율입니다.")] private float trailWidthMultiplier = 3f;
            [SerializeField, Min(0f), Tooltip("플레이어를 추적하는 시간입니다.")] private float homingSeconds;
            [SerializeField, Min(0f), Tooltip("초당 추적 회전 각도입니다.")] private float homingTurnDegreesPerSecond;

            public EnemyProjectile Prefab => prefab;
            public int BulletDamage => bulletDamage;
            public float ChargeSeconds => chargeSeconds;
            public float ChargeDriftSpeed => chargeDriftSpeed;
            public bool AimAtPlayerWhileCharging => aimAtPlayerWhileCharging;
            public bool AimAtPlayerOnLaunch => aimAtPlayerOnLaunch;
            public float Speed => speed;
            public float Lifetime => lifetime;
            public float Radius => radius;
            public Color ChargingColor => chargingColor;
            public Color LaunchedColor => launchedColor;
            public float TrailSeconds => trailSeconds;
            public float TrailWidthMultiplier => trailWidthMultiplier;
            public float HomingSeconds => homingSeconds;
            public float HomingTurnDegreesPerSecond => homingTurnDegreesPerSecond;
        }

        [System.Serializable]
        private sealed class BossBurstSettings
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
        private sealed class SummonSettings
        {
            [SerializeField, Tooltip("소환할 드론 프리팹입니다.")] private Drone prefab;
            [SerializeField, Tooltip("씬에 이미 배치된 소유자 없는 드론도 이 보스가 함께 지휘합니다.")] private bool claimSceneDrones = true;
            [SerializeField, Min(0), Tooltip("소유 드론 최대 수입니다. 0이면 제한하지 않습니다.")] private int maxOwnedDrones = 5;
            [SerializeField, Min(1), Tooltip("소환 패턴 한 번에 생성할 드론 수입니다.")] private int summonCount = 1;
            [SerializeField, Min(0f), Tooltip("보스 주변 소환 반지름입니다.")] private float spawnRadius = 1.2f;
            [SerializeField, Min(0f), Tooltip("드론을 여러 마리 소환할 때 사이 간격입니다.")] private float summonInterval = 0.2f;

            [SerializeField, Min(0f), Tooltip("보스 중심에서 소환 위치까지 이동하며 커지는 시간입니다.")] private float introSeconds = 0.55f;
            [SerializeField, Range(0f, 1f), Tooltip("소환 시작 시 드론 크기 비율입니다.")] private float introStartScale = 0.05f;

            public Drone Prefab => prefab;
            public bool ClaimSceneDrones => claimSceneDrones;
            public int MaxOwnedDrones => maxOwnedDrones;
            public int SummonCount => summonCount;
            public float SpawnRadius => spawnRadius;
            public float SummonInterval => summonInterval;
            public float IntroSeconds => introSeconds;
            public float IntroStartScale => introStartScale;
        }

        [System.Serializable]
        private sealed class DronePattern1Settings
        {
            [SerializeField, Tooltip("켜면 보스 일반 공격 탄환 설정을 드론도 그대로 사용합니다.")] private bool useBossProjectile = true;
            [SerializeField] private ProjectileSettings droneProjectile = new();

            public bool UseBossProjectile => useBossProjectile;
            public ProjectileSettings DroneProjectile => droneProjectile;
        }

        [System.Serializable]
        private sealed class DronePattern2Settings
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
        private sealed class DronePattern3Settings
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
        private sealed class DronePattern4Settings
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
        private sealed class DronePattern5Settings
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

            public ProjectileSettings BossProjectile => bossProjectile;
            public ProjectileSettings DroneProjectile => droneProjectile;
            public int BossBulletCount => bossBulletCount;
            public float BossFireInterval => bossFireInterval;
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

        private readonly List<Drone> controlledDrones = new();
        private Coroutine bossPatternRoutine;
        private Coroutine dronePatternRoutine;
        private ProjectileSettings synchronizedDroneProjectile;
        private int synchronizedDroneShotsRemaining;
        private int synchronizedDroneSyncVersion;
        private int nextBossPatternIndex;
        private int nextDronePatternIndex;
        private int currentPatternBulletTotal;
        private int currentPatternBulletRemaining;

        protected override void OnBossStarted()
        {
            RefreshControlledDrones();
        }

        protected override void OnBossTick()
        {
            if (!IsPlayerDetected())
            {
                return;
            }

            if (bossPatternRoutine == null)
            {
                bossPatternRoutine = StartCoroutine(RunBossPattern(SelectBossPattern()));
            }

            if (dronePatternRoutine == null)
            {
                dronePatternRoutine = StartCoroutine(RunDronePattern(SelectDronePattern()));
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

            ClearSynchronizedDroneFire();
            ClearPatternBulletUi();
            StopAllDrones();
        }

        protected override void OnBossDied()
        {
            CancelBossAction();
            ReleaseDrones();
        }

        public EnemyProjectile FireDroneProjectile(Drone source, ProjectileSettings settings, Vector3 origin, Vector2 direction, bool playMuzzleFlash)
        {
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

            EnemyProjectile projectile = EnemyProjectile.Spawn(
                settings.Prefab,
                ownerBullets,
                origin,
                direction,
                settings.BulletDamage,
                settings.ChargeSeconds,
                settings.Speed,
                settings.Lifetime,
                settings.Radius,
                settings.LaunchedColor,
                settings.TrailSeconds,
                settings.TrailWidthMultiplier,
                settings.HomingSeconds,
                settings.HomingTurnDegreesPerSecond);

            projectile?.ConfigureStateColors(settings.ChargingColor, settings.LaunchedColor);
            projectile?.ConfigureChargeMotion(settings.ChargeDriftSpeed, settings.AimAtPlayerWhileCharging, settings.AimAtPlayerOnLaunch);

            if (projectile != null && playMuzzleFlash)
            {
                ProjectileVfx.PlayMuzzleFlash(origin, direction, settings.LaunchedColor, 0.75f);
            }

            return projectile;
        }

        private PatternKind SelectBossPattern()
        {
            return SelectPattern(true, ref nextBossPatternIndex, PatternKind.BossBurst);
        }

        private PatternKind SelectDronePattern()
        {
            return SelectPattern(false, ref nextDronePatternIndex, PatternKind.DronePattern1);
        }

        private PatternKind SelectPattern(bool bossPattern, ref int nextIndex, PatternKind fallback)
        {
            if (patternSequence == null || patternSequence.Count == 0)
            {
                return fallback;
            }

            if (randomizePatterns)
            {
                int matchCount = 0;
                for (int i = 0; i < patternSequence.Count; i++)
                {
                    if (IsPatternGroup(patternSequence[i], bossPattern))
                    {
                        matchCount++;
                    }
                }

                if (matchCount <= 0)
                {
                    return fallback;
                }

                int selected = Random.Range(0, matchCount);
                for (int i = 0; i < patternSequence.Count; i++)
                {
                    if (!IsPatternGroup(patternSequence[i], bossPattern))
                    {
                        continue;
                    }

                    if (selected-- <= 0)
                    {
                        return patternSequence[i];
                    }
                }

                return fallback;
            }

            for (int i = 0; i < patternSequence.Count; i++)
            {
                int index = nextIndex % patternSequence.Count;
                PatternKind pattern = patternSequence[index];
                nextIndex++;
                if (IsPatternGroup(pattern, bossPattern))
                {
                    return pattern;
                }
            }

            return fallback;
        }

        private static bool IsPatternGroup(PatternKind pattern, bool bossPattern)
        {
            bool isBossPattern = pattern == PatternKind.BossBurst || pattern == PatternKind.SummonDrone;
            return bossPattern ? isBossPattern : !isBossPattern;
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

            ClearPatternBulletUi();
            yield return WaitPatternRecovery();
            bossPatternRoutine = null;
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

            yield return WaitPatternRecovery();
            dronePatternRoutine = null;
        }

        private IEnumerator WaitPatternRecovery()
        {
            float recovery = Random.Range(
                Mathf.Min(minPatternRecoverySeconds, maxPatternRecoverySeconds),
                Mathf.Max(minPatternRecoverySeconds, maxPatternRecoverySeconds));
            if (recovery > 0f)
            {
                yield return new WaitForSeconds(recovery);
            }
        }

        private IEnumerator RunSummonPattern()
        {
            RefreshControlledDrones();
            if (summon.Prefab == null)
            {
                yield break;
            }

            int maxOwned = summon.MaxOwnedDrones;
            int currentCount = GetControlledDrones().Count;
            int summonCount = Mathf.Max(1, summon.SummonCount);
            if (maxOwned > 0)
            {
                summonCount = Mathf.Min(summonCount, Mathf.Max(0, maxOwned - currentCount));
            }

            Stop();
            float longestIntro = 0f;
            for (int i = 0; i < summonCount; i++)
            {
                longestIntro = Mathf.Max(longestIntro, SpawnDrone(i, currentCount + summonCount));
                if (i < summonCount - 1 && summon.SummonInterval > 0f)
                {
                    yield return WaitStoppedSeconds(summon.SummonInterval);
                }
            }

            if (longestIntro > 0f)
            {
                yield return WaitStoppedSeconds(longestIntro);
            }
        }

        private IEnumerator RunDronePattern1()
        {
            if (!EnsureAnyDrone())
            {
                yield break;
            }

            ProjectileSettings droneProjectile = dronePattern1.UseBossProjectile
                ? bossBurst.Projectile
                : dronePattern1.DroneProjectile;
            int syncVersion = BeginSynchronizedDroneFire(droneProjectile, bossBurst.BulletCount);
            yield return WaitSynchronizedDroneFire(syncVersion);
        }

        private IEnumerator RunDronePattern2()
        {
            List<Drone> drones = GetControlledDrones();
            if (!EnsureAnyDrone(drones))
            {
                yield break;
            }

            Drone orbitDrone = drones[0];
            bool clockwise = Random.value > 0.5f;
            float duration = orbitDrone.CommandOrbitFire(
                dronePattern2.OrbitProjectile,
                dronePattern2.OrbitRadius,
                dronePattern2.OrbitSeconds,
                dronePattern2.FireAngleStepDegrees,
                clockwise);

            for (int i = 1; i < drones.Count; i++)
            {
                duration = Mathf.Max(duration, drones[i].CommandStopAndFire(
                    dronePattern2.StationaryProjectile,
                    dronePattern2.StationaryBulletCount,
                    dronePattern2.StationaryFireInterval,
                    true));
            }

            yield return WaitDronePatternSeconds(duration);
        }

        private IEnumerator RunDronePattern3()
        {
            List<Drone> drones = GetControlledDrones();
            if (!EnsureAnyDrone(drones))
            {
                yield break;
            }

            float duration = 0f;
            for (int i = 0; i < drones.Count; i++)
            {
                duration = Mathf.Max(duration, drones[i].CommandRadialBurst(
                    dronePattern3.Projectile,
                    dronePattern3.VolleyCount,
                    dronePattern3.DirectionCount,
                    dronePattern3.VolleyInterval,
                    dronePattern3.SpreadDegrees,
                    true));
            }

            yield return WaitDronePatternSeconds(duration);
        }

        private IEnumerator RunDronePattern4()
        {
            List<Drone> drones = GetControlledDrones();
            if (!EnsureAnyDrone(drones))
            {
                yield break;
            }

            float duration = 0f;
            float offset = Mathf.Max(0f, dronePattern4.AimOffsetDegrees);
            for (int i = 0; i < drones.Count; i++)
            {
                float sign = i % 2 == 0 ? 1f : -1f;
                float ring = 1f + i / 2;
                duration = Mathf.Max(duration, drones[i].CommandChargeSideFire(
                    dronePattern4.Projectile,
                    dronePattern4.ChargeSeconds,
                    dronePattern4.ChargeSpeed,
                    sign * offset * ring,
                    dronePattern4.SideFireInterval,
                    dronePattern4.SideFireAngleDegrees));
            }

            yield return WaitDronePatternSeconds(duration);
        }

        private IEnumerator RunDronePattern5()
        {
            List<Drone> drones = GetControlledDrones();
            if (!EnsureAnyDrone(drones))
            {
                yield break;
            }

            for (int i = 0; i < drones.Count; i++)
            {
                drones[i].CommandFormation(
                    GetPattern5FormationAngle(i, dronePattern5.FormationAngleSpacingDegrees),
                    dronePattern5.FormationRadius,
                    dronePattern5.FormationSpeedMultiplier);
            }

            if (dronePattern5.SettleSeconds > 0f)
            {
                yield return WaitDronePatternSeconds(dronePattern5.SettleSeconds);
            }

            int syncVersion = BeginSynchronizedDroneFire(dronePattern5.DroneProjectile, dronePattern5.BossBulletCount);
            yield return WaitSynchronizedDroneFire(syncVersion);

            ResumeAllDrones();
        }

        private IEnumerator RunBossBurst(
            bool notifyDrones,
            ProjectileSettings bossProjectile,
            int bulletCount,
            float fireInterval,
            float spawnSpacing,
            ProjectileSettings droneProjectile)
        {
            if (bossBurst.WindupSeconds > 0f)
            {
                yield return WaitPatternSeconds(bossBurst.WindupSeconds);
            }

            int count = Mathf.Max(1, bulletCount);
            BeginPatternBulletUi(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = GetProjectileOrigin();
                Vector2 direction = GetDirectionToPlayer(origin);
                Vector2 side = new(-direction.y, direction.x);
                Vector3 spawnPosition = origin + (Vector3)(side * GetAlternatingOffset(i, spawnSpacing));
                FireBossProjectile(bossProjectile, spawnPosition, direction, i == 0, origin);

                if (notifyDrones)
                {
                    FireAllDrones(droneProjectile);
                }

                TryFireSynchronizedDrones();
                ConsumePatternBulletUi();
                if (i < count - 1 && fireInterval > 0f)
                {
                    yield return new WaitForSeconds(fireInterval);
                }
            }
        }

        private EnemyProjectile FireBossProjectile(ProjectileSettings settings, Vector3 origin, Vector2 direction, bool playRecoil, Vector3 muzzleOrigin)
        {
            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            EnemyProjectile projectile = SpawnBossProjectile(
                settings.Prefab,
                origin,
                direction,
                settings.BulletDamage,
                settings.ChargeSeconds,
                settings.Speed,
                settings.Lifetime,
                settings.Radius,
                settings.LaunchedColor,
                settings.TrailSeconds,
                settings.TrailWidthMultiplier,
                settings.HomingSeconds,
                settings.HomingTurnDegreesPerSecond,
                playRecoil,
                muzzleOrigin);

            projectile?.ConfigureStateColors(settings.ChargingColor, settings.LaunchedColor);
            projectile?.ConfigureChargeMotion(settings.ChargeDriftSpeed, settings.AimAtPlayerWhileCharging, settings.AimAtPlayerOnLaunch);
            return projectile;
        }

        private float SpawnDrone(int index, int totalCount)
        {
            float angle = totalCount <= 0 ? Random.Range(0f, 360f) : 360f * index / Mathf.Max(1, totalCount);
            Vector3 startPosition = transform.position;
            Vector3 position = startPosition + (Vector3)(AngleToDirection(angle) * Mathf.Max(0f, summon.SpawnRadius));
            Drone drone = Instantiate(summon.Prefab, startPosition, Quaternion.identity);
            if (drone == null)
            {
                return 0f;
            }

            drone.SetOwner(this);
            float introSeconds = drone.BeginSummonIntro(startPosition, position, summon.IntroSeconds, summon.IntroStartScale);
            if (!controlledDrones.Contains(drone))
            {
                controlledDrones.Add(drone);
            }

            return introSeconds;
        }

        private List<Drone> GetControlledDrones()
        {
            RefreshControlledDrones();
            return controlledDrones;
        }

        private void RefreshControlledDrones()
        {
            for (int i = controlledDrones.Count - 1; i >= 0; i--)
            {
                if (controlledDrones[i] == null || controlledDrones[i].Owner != this)
                {
                    controlledDrones.RemoveAt(i);
                }
            }

            IReadOnlyList<Drone> allDrones = Drone.All;
            for (int i = 0; i < allDrones.Count; i++)
            {
                Drone drone = allDrones[i];
                if (drone == null)
                {
                    continue;
                }

                bool canClaim = drone.Owner == this || (summon.ClaimSceneDrones && drone.Owner == null);
                if (!canClaim)
                {
                    continue;
                }

                drone.SetOwner(this);
                if (!controlledDrones.Contains(drone))
                {
                    controlledDrones.Add(drone);
                }
            }
        }

        private bool EnsureAnyDrone()
        {
            return EnsureAnyDrone(GetControlledDrones());
        }

        private bool EnsureAnyDrone(List<Drone> drones)
        {
            if (drones != null && drones.Count > 0)
            {
                return true;
            }

            return false;
        }

        private void FireAllDrones(ProjectileSettings projectile)
        {
            if (projectile == null)
            {
                return;
            }

            List<Drone> drones = GetControlledDrones();
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.FireOnceAtPlayer(projectile);
            }
        }

        private int BeginSynchronizedDroneFire(ProjectileSettings projectile, int shotCount)
        {
            synchronizedDroneProjectile = projectile;
            synchronizedDroneShotsRemaining = projectile != null ? Mathf.Max(0, shotCount) : 0;
            synchronizedDroneSyncVersion++;
            return synchronizedDroneSyncVersion;
        }

        private IEnumerator WaitSynchronizedDroneFire(int syncVersion)
        {
            while (syncVersion == synchronizedDroneSyncVersion && synchronizedDroneShotsRemaining > 0)
            {
                yield return null;
            }
        }

        private void TryFireSynchronizedDrones()
        {
            if (synchronizedDroneProjectile == null || synchronizedDroneShotsRemaining <= 0)
            {
                return;
            }

            FireAllDrones(synchronizedDroneProjectile);
            synchronizedDroneShotsRemaining--;
            if (synchronizedDroneShotsRemaining <= 0)
            {
                ClearSynchronizedDroneFire();
            }
        }

        private void ClearSynchronizedDroneFire()
        {
            synchronizedDroneProjectile = null;
            synchronizedDroneShotsRemaining = 0;
            synchronizedDroneSyncVersion++;
        }

        private void StopAllDrones()
        {
            List<Drone> drones = GetControlledDrones();
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.StopCommand();
            }
        }

        private void ResumeAllDrones()
        {
            List<Drone> drones = GetControlledDrones();
            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.ResumeIdle();
            }
        }

        private void ReleaseDrones()
        {
            for (int i = controlledDrones.Count - 1; i >= 0; i--)
            {
                if (controlledDrones[i] != null)
                {
                    controlledDrones[i].ClearOwner(this);
                }
            }

            controlledDrones.Clear();
        }

        private IEnumerator WaitPatternSeconds(float seconds)
        {
            float duration = Mathf.Max(0f, seconds);
            float remaining = duration;
            while (remaining > 0f)
            {
                Stop();
                ShowAttackTiming(remaining, duration);
                remaining -= Time.deltaTime;
                yield return null;
            }

            HideAttackTiming();
        }

        private IEnumerator WaitStoppedSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                Stop();
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private static IEnumerator WaitDronePatternSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private Vector3 GetProjectileOrigin()
        {
            if (ProjectileOrigin != null)
            {
                return ProjectileOrigin.position;
            }

            if (FireOrigin != null)
            {
                return FireOrigin.position;
            }

            return transform.position;
        }

        private void BeginPatternBulletUi(int totalBulletCount)
        {
            currentPatternBulletTotal = Mathf.Max(0, totalBulletCount);
            currentPatternBulletRemaining = currentPatternBulletTotal;
            ShowCurrentPatternBullets();
        }

        private void ConsumePatternBulletUi()
        {
            if (currentPatternBulletTotal <= 0)
            {
                return;
            }

            currentPatternBulletRemaining = Mathf.Max(0, currentPatternBulletRemaining - 1);
            ShowCurrentPatternBullets();
        }

        private void ShowCurrentPatternBullets()
        {
            if (currentPatternBulletTotal <= 0)
            {
                HideAttackTiming();
                return;
            }

            ShowAttackBullets(currentPatternBulletRemaining, currentPatternBulletTotal);
        }

        private void ClearPatternBulletUi()
        {
            currentPatternBulletTotal = 0;
            currentPatternBulletRemaining = 0;
            HideAttackTiming();
        }

        private static float GetPattern5FormationAngle(int index, float spacingDegrees)
        {
            if (index <= 0)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 1 ? 1f : -1f;
            return sign * ring * Mathf.Max(1f, spacingDegrees);
        }

        private static float GetAlternatingOffset(int index, float spacing)
        {
            if (index <= 0 || spacing <= 0f)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 0 ? -1f : 1f;
            return ring * spacing * sign;
        }
    }
}
