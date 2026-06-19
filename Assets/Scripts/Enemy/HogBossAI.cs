using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class HogBossAI : BossAI
    {
        private static readonly Color DefaultHogEffectColor = new(0.278f, 0.451f, 0.188f, 1f);

        private enum PatternKind
        {
            Pattern1,
            Pattern2,
            Pattern3,
            Pattern4,
            Pattern5
        }

        [System.Serializable]
        private sealed class PhasePatternSet
        {
            [SerializeField, Min(1), Tooltip("인스펙터에서 구분하기 위한 페이즈 번호입니다. 실제 적용은 배열 순서 기준입니다.")]
            private int phase = 1;

            [SerializeField, Tooltip("이 페이즈에서 사용할 패턴 목록입니다. 비어 있으면 모든 패턴을 사용합니다.")]
            private List<PatternKind> patterns = new()
            {
                PatternKind.Pattern1,
                PatternKind.Pattern2,
                PatternKind.Pattern3,
                PatternKind.Pattern4,
                PatternKind.Pattern5
            };

            public int Phase
            {
                get => phase;
                set => phase = Mathf.Max(1, value);
            }

            public List<PatternKind> Patterns => patterns;
        }

        [System.Serializable]
        private sealed class ProjectileSettings
        {
            [SerializeField, Tooltip("이 설정으로 생성할 적 탄환 프리팹입니다.")] private EnemyProjectile prefab;
            [SerializeField, Min(0), Tooltip("플레이어에게 적중했을 때 플레이어 탄환을 감소시키는 양입니다.")] private int bulletDamage = 1;
            [SerializeField, Min(0f), Tooltip("발사 전 대기하는 시간입니다.")] private float chargeSeconds = 0.35f;
            [SerializeField, Min(0f), Tooltip("대기 시간 동안 탄환이 천천히 이동하는 속도입니다.")] private float chargeDriftSpeed = 0.65f;
            [SerializeField, Tooltip("대기 중에도 플레이어를 계속 향해 방향을 갱신할지 여부입니다.")] private bool aimAtPlayerWhileCharging = true;
            [SerializeField, Min(0f), Tooltip("대기 시간이 끝난 뒤 탄환 이동 속도입니다.")] private float speed = 7f;
            [SerializeField, Min(0f), Tooltip("탄환이 자동으로 사라지기까지 걸리는 시간입니다.")] private float lifetime = 3f;
            [SerializeField, Min(0.01f), Tooltip("탄환 충돌 반지름과 시각 크기 기준입니다.")] private float radius = 0.12f;
            [SerializeField, Tooltip("대기 중인 탄환 색입니다.")] private Color chargingColor = new(0.45f, 0.7f, 0.25f, 1f);
            [SerializeField, FormerlySerializedAs("color"), Tooltip("발사된 탄환 색입니다.")] private Color launchedColor = new(1f, 0.95f, 0.25f, 1f);
            [SerializeField, Min(0.01f), Tooltip("탄환 궤적이 남아 있는 시간입니다.")] private float trailSeconds = 0.1f;
            [SerializeField, Min(0.1f), Tooltip("탄환 궤적 두께 배율입니다.")] private float trailWidthMultiplier = 3f;
            [SerializeField] private bool homingEnabled;
            [SerializeField, Min(0f), Tooltip("발사 후 플레이어를 추적하는 시간입니다.")] private float homingSeconds = 0.8f;
            [SerializeField, Min(0f), Tooltip("추적 중 초당 회전 가능한 최대 각도입니다.")] private float homingTurnDegreesPerSecond = 540f;

            public EnemyProjectile Prefab => prefab;
            public int BulletDamage => bulletDamage;
            public float ChargeSeconds => chargeSeconds;
            public float ChargeDriftSpeed => chargeDriftSpeed;
            public bool AimAtPlayerWhileCharging => aimAtPlayerWhileCharging;
            public float Speed => speed;
            public float Lifetime => lifetime;
            public float Radius => radius;
            public Color ChargingColor => chargingColor;
            public Color LaunchedColor => launchedColor;
            public float TrailSeconds => trailSeconds;
            public float TrailWidthMultiplier => trailWidthMultiplier;
            public bool HomingEnabled => homingEnabled;
            public float HomingSeconds => homingSeconds;
            public float HomingTurnDegreesPerSecond => homingTurnDegreesPerSecond;
        }

        [System.Serializable]
        private sealed class Pattern1Settings
        {
            [SerializeField, Tooltip("패턴1에서 사용할 일반 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("패턴 시작 시 추격 속도 배율입니다.")] private float initialChaseSpeedMultiplier = 0.65f;
            [SerializeField, Min(0f), Tooltip("패턴 종료 시점의 추격 속도 배율입니다.")] private float finalChaseSpeedMultiplier = 1.8f;
            [SerializeField, Min(4), Tooltip("한 번의 사방 발사에서 생성할 탄환 수입니다.")] private int radialBulletCount = 4;
            [SerializeField, Min(0.01f), Tooltip("사방 탄환을 반복 발사하는 간격입니다.")] private float burstInterval = 0.65f;
            [SerializeField, Min(0f), Tooltip("보스 중심에서 패턴1 탄환을 원형으로 배치할 거리입니다.")] private float spawnRadius = 0.85f;
            [SerializeField, Tooltip("탄환을 발사할 때마다 회전시킬 각도입니다.")] private float angleStepDegrees = 25f;

            public ProjectileSettings Projectile => projectile;
            public float InitialChaseSpeedMultiplier => initialChaseSpeedMultiplier;
            public float FinalChaseSpeedMultiplier => finalChaseSpeedMultiplier;
            public int RadialBulletCount => radialBulletCount;
            public float BurstInterval => burstInterval;
            public float SpawnRadius => spawnRadius;
            public float AngleStepDegrees => angleStepDegrees;
        }

        [System.Serializable]
        private sealed class Pattern2Settings
        {
            [System.Serializable]
            public sealed class VolleySettings
            {
                [SerializeField, Min(1), Tooltip("이 묶음에서 연속으로 발사할 탄환 수입니다.")]
                private int bulletCount = 4;

                [SerializeField, Min(0f), Tooltip("이 묶음 안에서 탄환 사이의 발사 간격입니다.")]
                private float fireInterval = 0.12f;

                [SerializeField, Min(0f), Tooltip("이 묶음이 끝난 뒤 다음 묶음 전까지 쉬는 시간입니다.")]
                private float restSeconds = 0.35f;

                public int BulletCount => bulletCount;
                public float FireInterval => fireInterval;
                public float RestSeconds => restSeconds;
            }

            [SerializeField, Tooltip("패턴2에서 머신건처럼 발사할 특수 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("패턴2 중 느려진 이동 속도 배율입니다.")] private float moveSpeedMultiplier = 0.25f;
            [SerializeField, Min(1), Tooltip("머신건처럼 연속 발사할 탄환 수입니다.")] private int bulletCount = 14;
            [SerializeField, Min(0.01f), Tooltip("머신건 탄환 발사 간격입니다.")] private float fireInterval = 0.12f;
            [SerializeField, Min(0f), Tooltip("연속 탄환들이 겹치지 않도록 옆으로 벌리는 거리입니다.")] private float spawnSpacing = 0.18f;

            [SerializeField, Tooltip("발사 묶음 목록입니다. 각 묶음은 n발을 m초 간격으로 쏜 뒤 l초 쉽니다.")]
            private List<VolleySettings> volleys = new() { new VolleySettings() };

            public ProjectileSettings Projectile => projectile;
            public float MoveSpeedMultiplier => moveSpeedMultiplier;
            public int BulletCount => bulletCount;
            public float FireInterval => fireInterval;
            public float SpawnSpacing => spawnSpacing;
            public IReadOnlyList<VolleySettings> Volleys => volleys;
            public int TotalBulletCount
            {
                get
                {
                    int total = 0;
                    if (volleys == null)
                    {
                        return total;
                    }

                    for (int i = 0; i < volleys.Count; i++)
                    {
                        if (volleys[i] != null)
                        {
                            total += Mathf.Max(1, volleys[i].BulletCount);
                        }
                    }

                    return total;
                }
            }
        }

        [System.Serializable]
        private sealed class Pattern3Settings
        {
            [SerializeField, Tooltip("패턴3에서 커졌다가 분열하는 특수 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("특수 탄환이 보스에게 붙어서 점점 커지는 시간입니다.")] private float windupSeconds = 1.6f;
            [SerializeField, Min(0), Tooltip("벽에 부딪힌 특수 탄환이 몇 단계까지 분열할지 정합니다.")] private int splitDepth = 3;
            [SerializeField, Min(0f), Tooltip("분열된 두 탄환이 벌어지는 각도입니다.")] private float splitAngleDegrees = 48f;
            [SerializeField, Min(0.01f), Tooltip("분열될 때마다 적용할 속도 배율입니다.")] private float splitSpeedMultiplier = 0.92f;
            [SerializeField, Range(0.05f, 1f), Tooltip("분열될 때마다 적용할 크기 배율입니다.")] private float splitRadiusMultiplier = 0.62f;
            [SerializeField, Range(0.05f, 1f), Tooltip("분열될 때마다 적용할 수명 배율입니다.")] private float splitLifetimeMultiplier = 0.85f;
            [SerializeField, Min(1f), Tooltip("패턴3 특수 탄환 충돌 크기에 곱할 배율입니다.")] private float projectileRadiusMultiplier = 4f;
            [SerializeField, Min(1f), Tooltip("패턴3 특수 탄환 프리팹 루트 Scale에 곱할 최종 배율입니다.")] private float finalScaleMultiplier = 4f;
            [SerializeField, Range(0.01f, 1f), Tooltip("패턴3 특수 탄환이 처음 붙어 있을 때 최종 Scale에 곱할 비율입니다.")] private float startScaleRatio = 0.18f;
            [SerializeField, Min(0.1f), Tooltip("패턴3 특수 탄환 발사 순간 보글보글 이펙트 크기입니다.")] private float launchBubbleScale = 2.3f;
            [SerializeField, Range(0f, 180f), Tooltip("패턴3 특수 탄환이 플레이어 방향 근처로 빗나갈 수 있는 각도입니다.")] private float aimSpreadDegrees = 24f;
            [SerializeField, Min(0f), Tooltip("발사 전 플레이어를 따라가며 조준(회전)하는 시간입니다. 이 시간이 지나면 멈추고 대기합니다.")] private float aimTrackingSeconds = 1.0f;
            
            public ProjectileSettings Projectile => projectile;
            public float WindupSeconds => windupSeconds;
            public float AimTrackingSeconds => aimTrackingSeconds;
            public int SplitDepth => splitDepth;
            public float SplitAngleDegrees => splitAngleDegrees;
            public float SplitSpeedMultiplier => splitSpeedMultiplier;
            public float SplitRadiusMultiplier => splitRadiusMultiplier;
            public float SplitLifetimeMultiplier => splitLifetimeMultiplier;
            public float ProjectileRadiusMultiplier => projectileRadiusMultiplier;
            public float StartScaleMultiplier => finalScaleMultiplier * startScaleRatio;
            public float FinalScaleMultiplier => finalScaleMultiplier;
            public float LaunchBubbleScale => launchBubbleScale;
            [SerializeField, Min(0.01f), Tooltip("기 모으는 동안 버블 이펙트를 반복하는 간격입니다.")] private float windupBubbleInterval = 0.14f;
            [SerializeField, Min(0.1f), Tooltip("기 모으는 동안 반복되는 버블 이펙트 크기 배율입니다.")] private float windupBubbleScale = 1.2f;
            [SerializeField, Min(1), Tooltip("기 모으는 동안 한 번에 생성할 버블 수입니다.")] private int windupBubbleCount = 8;
            [SerializeField, Min(0.1f), Tooltip("특수 탄환 발사 순간 총구 화염 크기 배율입니다.")] private float launchMuzzleFlashScale = 2.2f;

            public float WindupBubbleInterval => windupBubbleInterval;
            public float WindupBubbleScale => windupBubbleScale;
            public int WindupBubbleCount => windupBubbleCount;
            public float LaunchMuzzleFlashScale => launchMuzzleFlashScale;
            public float AimSpreadDegrees => aimSpreadDegrees;
            [SerializeField, Min(1), Tooltip("대기 종료 시 전방위로 분열되어 생성할 탄환 수입니다.")] private int radialSplitBulletCount = 12;
            [SerializeField, Tooltip("전방위 분열의 시작 각도 오프셋입니다.")] private float radialSplitStartAngleOffset;
            [SerializeField, Min(0f), Tooltip("발사된 뒤 전방위로 분열되기까지 기다리는 시간입니다.")] private float splitDelaySeconds = 0.8f;

            public int RadialSplitBulletCount => radialSplitBulletCount;
            public float RadialSplitStartAngleOffset => radialSplitStartAngleOffset;
            public float SplitDelaySeconds => splitDelaySeconds;
        }

        [System.Serializable]
        private sealed class Pattern4Settings
        {
            [SerializeField, Tooltip("패턴4에서 랜덤 순서로 전방위 발사할 특수 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(1), Tooltip("한 웨이브에서 360도로 발사할 탄환 수입니다.")] private int bulletCount = 32;
            [SerializeField, Min(1), Tooltip("전방위 원형 파동 발사를 몇 번 반복할지 정합니다.")] private int waveCount = 3;
            [SerializeField, Min(0f), Tooltip("전방위 웨이브 사이의 대기 시간입니다.")] private float waveInterval = 0.2f;
            [SerializeField, Tooltip("첫 탄환의 시작 각도 오프셋입니다.")] private float startAngleOffset;
            [SerializeField, Min(0f), Tooltip("보스 중심에서 전방위 탄환을 원형으로 배치할 거리입니다.")] private float spawnRadius = 0.85f;

            public ProjectileSettings Projectile => projectile;
            public int BulletCount => bulletCount;
            public int WaveCount => waveCount;
            public float WaveInterval => waveInterval;
            public float StartAngleOffset => startAngleOffset;
            public float SpawnRadius => spawnRadius;
        }

        [System.Serializable]
        private sealed class Pattern5Settings
        {
            [SerializeField, Tooltip("패턴5에서 미니건처럼 발사할 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("제자리에서 기를 모으는 시간입니다.")] private float windupSeconds = 1.4f;
            [SerializeField, Min(1), Tooltip("기 모으기가 끝난 뒤 발사할 탄환 수입니다.")] private int bulletCount = 36;
            [SerializeField, Min(0.01f), Tooltip("미니건 탄환 발사 간격입니다.")] private float fireInterval = 0.045f;
            [SerializeField, Min(0f), Tooltip("연속 탄환들이 겹치지 않도록 옆으로 벌리는 거리입니다.")] private float spawnSpacing = 0.12f;
            [SerializeField, Min(0f), Tooltip("매 탄환마다 회전할 각도입니다.")] private float sweepStepDegrees = 5f;
            [SerializeField, Min(0f), Tooltip("중심 방향(플레이어)을 기준으로 좌우로 꺾이는 최대 부채꼴 각도입니다.")] private float maxSweepAngle = 35f;
            [SerializeField, Min(0.01f), Tooltip("기 모으는 동안 버블 이펙트를 반복하는 간격입니다.")] private float windupBubbleInterval = 0.12f;
            [SerializeField, Min(0.1f), Tooltip("기 모으는 동안 반복되는 버블 이펙트 크기 배율입니다.")] private float windupBubbleScale = 0.85f;
            [SerializeField, Min(1), Tooltip("기 모으는 동안 한 번에 생성할 버블 수입니다.")] private int windupBubbleCount = 6;

            public ProjectileSettings Projectile => projectile;
            public float WindupSeconds => windupSeconds;
            public int BulletCount => bulletCount;
            public float FireInterval => fireInterval;
            public float SpawnSpacing => spawnSpacing;
            public float SweepStepDegrees => sweepStepDegrees;
            public float MaxSweepAngle => maxSweepAngle;
            public float WindupBubbleInterval => windupBubbleInterval;
            public float WindupBubbleScale => windupBubbleScale;
            public int WindupBubbleCount => windupBubbleCount;
        }

        [Header("Hog Patterns")]
        [SerializeField, Tooltip("플레이어 방향 기준 사방 탄환을 발사하며 가속 추격하는 패턴 설정입니다.")] private Pattern1Settings pattern1 = new();
        [SerializeField, Tooltip("느려진 상태로 플레이어를 향해 머신건처럼 발사하는 패턴 설정입니다.")] private Pattern2Settings pattern2 = new();
        [SerializeField, Tooltip("벽에 부딪히면 분열하는 거대 특수 탄환 패턴 설정입니다.")] private Pattern3Settings pattern3 = new();
        [SerializeField, Tooltip("360도 전방위 탄환을 발사하는 패턴 설정입니다.")] private Pattern4Settings pattern4 = new();
        [SerializeField, Tooltip("제자리에서 기를 모은 뒤 미니건처럼 다수의 탄환을 발사하는 패턴 설정입니다.")] private Pattern5Settings pattern5 = new();
        [SerializeField, Tooltip("페이즈별로 포함할 패턴 목록입니다. 1번 요소가 페이즈 1, 2번 요소가 페이즈 2입니다.")]
        private List<PhasePatternSet> phasePatterns = new()
        {
            new PhasePatternSet { Phase = 1 },
            new PhasePatternSet { Phase = 2 },
            new PhasePatternSet { Phase = 3 }
        };
        [SerializeField, FormerlySerializedAs("patternRecoverySeconds"), Min(0f), Tooltip("패턴 하나가 끝난 뒤 다음 패턴 전까지 쉬는 최소 시간입니다.")] private float minPatternRecoverySeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("패턴 하나가 끝난 뒤 다음 패턴 전까지 쉬는 최대 시간입니다.")] private float maxPatternRecoverySeconds = 0.9f;
        [SerializeField, Tooltip("켜면 패턴을 순서대로 쓰지 않고 무작위로 선택합니다.")] private bool randomizePatterns;

        [Header("Hog Effects")]
        [SerializeField, Tooltip("호그 보글보글 이펙트 대표색입니다. 기본값은 #477330입니다.")] private Color hogEffectColor = new(0.278f, 0.451f, 0.188f, 1f);
        [SerializeField, Min(0.1f), Tooltip("패턴1, 패턴2, 패턴4에서 생성되는 보글보글 이펙트 크기입니다.")] private float bubbleEffectScale = 1f;
        [SerializeField, Tooltip("유도 기능이 꺼진 보스 투사체 발사 전 색입니다.")] private Color normalProjectileChargeColor = new(0.45f, 0.7f, 0.25f, 1f);
        [SerializeField, Tooltip("유도 기능이 꺼진 보스 투사체 발사 후 색입니다.")] private Color normalProjectileColor = new(1f, 0.95f, 0.25f, 1f);
        [SerializeField, Tooltip("유도 기능이 켜진 보스 투사체 발사 전 색입니다.")] private Color homingProjectileChargeColor = new(0.35f, 0.8f, 1f, 1f);
        [SerializeField, Tooltip("유도 기능이 켜진 보스 투사체 발사 후 색입니다.")] private Color homingProjectileColor = new(0.35f, 0.75f, 1f, 1f);

        [Header("Debug")]
        [SerializeField, Tooltip("켜면 아래에서 고른 패턴만 반복 실행합니다.")] private bool debugUseFixedPattern;
        [SerializeField, Tooltip("디버그용으로 고정 실행할 패턴입니다.")] private PatternKind debugPattern = PatternKind.Pattern1;

        private Coroutine patternRoutine;
        private int nextPatternIndex;
        private int currentPatternBulletTotal;
        private int currentPatternBulletRemaining;

        private void OnValidate()
        {
            EnsurePhasePatternSlots();
            EnsurePhasePatternLabels();
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || !IsPlayerDetected())
            {
                return;
            }

            PatternKind pattern = SelectPattern();
            PreviewPatternBulletUi(pattern);
            patternRoutine = StartCoroutine(RunPattern(pattern));
        }

        protected override void CancelBossAction()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            HideAttackTiming();
            currentPatternBulletTotal = 0;
            currentPatternBulletRemaining = 0;
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            nextPatternIndex = 0;
            EnsurePhasePatternLabels();
        }

        private PatternKind SelectPattern()
        {
            if (debugUseFixedPattern)
            {
                return debugPattern;
            }

            List<PatternKind> availablePatterns = GetCurrentPhasePatterns();
            if (randomizePatterns)
            {
                return availablePatterns[Random.Range(0, availablePatterns.Count)];
            }

            PatternKind pattern = availablePatterns[nextPatternIndex % availablePatterns.Count];
            nextPatternIndex++;
            return pattern;
        }

        private List<PatternKind> GetCurrentPhasePatterns()
        {
            EnsurePhasePatternSlots();
            PhasePatternSet phasePatternSet = GetCurrentPhasePatternSet();
            if (phasePatternSet != null && phasePatternSet.Patterns != null && phasePatternSet.Patterns.Count > 0)
            {
                return phasePatternSet.Patterns;
            }

            return GetDefaultPatternList();
        }

        private PhasePatternSet GetCurrentPhasePatternSet()
        {
            if (phasePatterns == null || phasePatterns.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(CurrentPhaseIndex, 0, phasePatterns.Count - 1);
            return phasePatterns[index];
        }

        private void EnsurePhasePatternSlots()
        {
            phasePatterns ??= new List<PhasePatternSet>();
            while (phasePatterns.Count < MaxLives)
            {
                phasePatterns.Add(new PhasePatternSet { Phase = phasePatterns.Count + 1 });
            }
        }

        private void EnsurePhasePatternLabels()
        {
            if (phasePatterns == null)
            {
                return;
            }

            for (int i = 0; i < phasePatterns.Count; i++)
            {
                if (phasePatterns[i] != null)
                {
                    phasePatterns[i].Phase = i + 1;
                }
            }
        }

        private static List<PatternKind> GetDefaultPatternList()
        {
            return new List<PatternKind>
            {
                PatternKind.Pattern1,
                PatternKind.Pattern2,
                PatternKind.Pattern3,
                PatternKind.Pattern4,
                PatternKind.Pattern5
            };
        }

        private IEnumerator RunPattern(PatternKind pattern)
        {
            switch (pattern)
            {
                case PatternKind.Pattern1:
                    yield return RunPattern1();
                    break;
                case PatternKind.Pattern2:
                    yield return RunPattern2();
                    break;
                case PatternKind.Pattern3:
                    yield return RunPattern3();
                    break;
                case PatternKind.Pattern4:
                    yield return RunPattern4();
                    break;
                case PatternKind.Pattern5:
                    yield return RunPattern5();
                    break;
                default:
                    yield return RunPattern1();
                    break;
            }

            // 패턴이 끝난 직후(다음 패턴 시작 전)의 안전 지점에서만 광폭화 진입을 적용합니다.
            yield return ApplyPendingEnrageIfAny();

            Stop();
            PatternKind nextPattern = SelectPattern();
            PreviewPatternBulletUi(nextPattern);
            float recoverySeconds = GetPatternRecoverySeconds();
            if (recoverySeconds > 0f)
            {
                yield return RunPatternRecovery(recoverySeconds);
            }

            patternRoutine = StartCoroutine(RunPattern(nextPattern));
        }

        private float GetPatternRecoverySeconds()
        {
            float min = Mathf.Max(0f, minPatternRecoverySeconds);
            float max = Mathf.Max(min, maxPatternRecoverySeconds);
            return Random.Range(min, max);
        }

        private IEnumerator RunPatternRecovery(float duration)
        {
            float remaining = duration;

            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                ShowAttackTiming(remaining, duration, currentPatternBulletRemaining, currentPatternBulletTotal);
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator RunPattern1()
        {
            float elapsed = 0f;
            float nextBurstAt = 0f;
            int fired = 0;
            int totalBullets = Mathf.Max(1, pattern1.RadialBulletCount);

            // 첫 탄환의 시작 각도를 무작위로 설정 (항상 같은 방향에서 시작하려면 0f 등으로 고정)
            float currentAngle = Random.Range(0f, 360f);

            while (fired < totalBullets)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                float t = totalBullets <= 1 ? 1f : Mathf.Clamp01((float)fired / (totalBullets - 1));
                float speedMultiplier = Mathf.Lerp(
                    pattern1.InitialChaseSpeedMultiplier,
                    pattern1.FinalChaseSpeedMultiplier,
                    t);
                MoveTowardPlayer(speedMultiplier);

                if (fired < totalBullets && elapsed >= nextBurstAt)
                {
                    Vector2 direction = AngleToDirection(currentAngle);
                    Vector3 origin = GetPattern1SpawnPosition(direction);
            
                    EnemyProjectile projectile = FireConfiguredProjectileWithPlayerLaunchAim(
                        pattern1.Projectile,
                        origin,
                        direction,
                        fired == 0,
                        useOwnColors: true);
                
                    PlayBubbleEffectIfSpawned(projectile, origin, 1f, 10);
                    fired++;
                    ConsumePatternBulletUi();
                    nextBurstAt += Mathf.Max(0.01f, pattern1.BurstInterval);
                    
                    currentAngle += pattern1.AngleStepDegrees;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator RunPattern2()
        {
            int fired = 0;

            IReadOnlyList<Pattern2Settings.VolleySettings> volleys = pattern2.Volleys;
            if (volleys == null || volleys.Count == 0)
            {
                yield break;
            }

            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Pattern2Settings.VolleySettings volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                int volleyBulletCount = Mathf.Max(1, volley.BulletCount);
                for (int bulletIndex = 0; bulletIndex < volleyBulletCount; bulletIndex++)
                {
                    yield return WaitWhileExecutionPaused();

                    MoveTowardPlayer(pattern2.MoveSpeedMultiplier);
                    FireMachinegunBullet(fired);
                    fired++;
                    ConsumePatternBulletUi();

                    if (bulletIndex < volleyBulletCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return WaitPattern2Seconds(volley.FireInterval);
                    }
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return WaitPattern2Seconds(volley.RestSeconds);
                }
            }
        }

        private IEnumerator RunPattern3()
        {
            Stop();

            Vector3 origin = GetProjectileOrigin();
            float radius = pattern3.Projectile.Radius * pattern3.ProjectileRadiusMultiplier;
            
            // 👇 변경: 5번째 인자(차징 중 조준)를 true로, 6번째 인자(발사 순간 조준)를 false로 변경
            EnemyProjectile projectile = FireConfiguredProjectile(
                pattern3.Projectile,
                origin,
                GetPattern3Direction(origin),
                true,
                true,   // 차징 중 조준 켬
                false,  // 발사 순간 조준 끔
                true,
                Mathf.Max(0f, pattern3.WindupSeconds),
                radius,
                null,
                0f);
            ConsumePatternBulletUi();

            if (projectile == null)
            {
                yield break;
            }

            projectile.ConfigureProjectileSize(radius);
            
            // 👇 변경: 차징 중 조준(true), 발사 시 조준(false)
            projectile.ConfigureChargeMotion(0f, true, false, pattern3.AimSpreadDegrees);
            projectile.ConfigureChargeGrowth(
                pattern3.StartScaleMultiplier,
                pattern3.FinalScaleMultiplier,
                GetHogEffectColor(),
                pattern3.LaunchBubbleScale);
            projectile.ConfigureLaunchMuzzleFlash(pattern3.LaunchMuzzleFlashScale);
            projectile.ConfigureInterceptable(false);
            projectile.ConfigureRadialSplitOnLaunch(
                pattern3.RadialSplitBulletCount,
                pattern3.RadialSplitStartAngleOffset,
                pattern3.SplitDelaySeconds,
                pattern3.SplitSpeedMultiplier,
                pattern3.SplitRadiusMultiplier,
                pattern3.SplitLifetimeMultiplier);

            float nextBubbleAt = Time.time;
            float elapsed = 0f;
            bool trackingStopped = false; // 👇 추적 정지 여부 체크 변수

            while (projectile != null && projectile.IsCharging)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                // 👇 추가된 로직: 설정한 조준 시간이 지나면 조준을 멈춤
                if (!trackingStopped && elapsed >= pattern3.AimTrackingSeconds)
                {
                    projectile.ConfigureChargeMotion(0f, false, false, pattern3.AimSpreadDegrees);
                    trackingStopped = true;
                }

                PlayWindupBubbleIfDue(ref nextBubbleAt, projectile.transform.position, pattern3.WindupBubbleInterval, pattern3.WindupBubbleScale, pattern3.WindupBubbleCount);
                
                elapsed += Time.deltaTime; // 👇 시간 누적
                yield return null;
            }
        }

        private IEnumerator RunPattern4()
        {
            Stop();

            for (int wave = 0; wave < pattern4.WaveCount; wave++)
            {
                yield return WaitWhileExecutionPaused();

                BeginPatternBulletUi(Mathf.Max(1, pattern4.BulletCount));
                
                float offset = pattern4.StartAngleOffset + wave * (360f / Mathf.Max(1, pattern4.BulletCount) * 0.5f);
                
                FirePattern4Wave(offset);

                if (wave < pattern4.WaveCount - 1 && pattern4.WaveInterval > 0f)
                {
                    yield return WaitPattern5Seconds(pattern4.WaveInterval);
                }
            }
        }

        private IEnumerator RunPattern5()
        {
            Stop();

            int bulletCount = Mathf.Max(1, pattern5.BulletCount);

            float windupSeconds = Mathf.Max(0f, pattern5.WindupSeconds);
            float elapsed = 0f;
            float nextBubbleAt = Time.time;
            while (elapsed < windupSeconds)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                Stop();
                PlayWindupBubbleIfDue(ref nextBubbleAt, GetProjectileOrigin(), pattern5.WindupBubbleInterval, pattern5.WindupBubbleScale, pattern5.WindupBubbleCount);

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetPatternBulletUiLoaded(bulletCount);

            float currentSweepOffset = 0f;
            float sweepDirection = 1f;

            for (int i = 0; i < bulletCount; i++)
            {
                yield return WaitWhileExecutionPaused();

                Stop();
                
                Vector3 currentOrigin = GetProjectileOrigin();
                Vector2 dynamicBaseDirection = GetDirectionToPlayer(currentOrigin);
                
                float dynamicBaseAngle = Mathf.Atan2(dynamicBaseDirection.y, dynamicBaseDirection.x) * Mathf.Rad2Deg;
                float finalAngle = dynamicBaseAngle + currentSweepOffset;
                
                FirePattern5Bullet(i, finalAngle); 
                ConsumePatternBulletUi();
                currentSweepOffset += pattern5.SweepStepDegrees * sweepDirection;
        
                if (Mathf.Abs(currentSweepOffset) >= pattern5.MaxSweepAngle)
                {
                    sweepDirection *= -1f; 
                    currentSweepOffset = Mathf.Sign(currentSweepOffset) * pattern5.MaxSweepAngle; // 오차 보정
                }

                if (pattern5.FireInterval > 0f && i < bulletCount - 1)
                {
                    yield return WaitPattern5Seconds(pattern5.FireInterval);
                }
            }
        }

        private EnemyProjectile FireProjectileAtPlayer(ProjectileSettings settings, Vector3 origin, bool playRecoil)
        {
            Vector2 direction = GetDirectionToPlayer(origin);
            return FireConfiguredProjectileWithPlayerLaunchAim(settings, origin, direction, playRecoil);
        }

        private void FireRadialBurst(ProjectileSettings settings, int bulletCount, float startAngleDegrees)
        {
            Vector3 origin = GetProjectileOrigin();
            int count = Mathf.Max(1, bulletCount);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                FireConfiguredProjectile(settings, origin, AngleToDirection(startAngleDegrees + step * i), i == 0);
            }
        }
        
        private void FirePattern4Wave(float startAngleDegrees)
        {
            Vector3 center = transform.position;
            int count = Mathf.Max(1, pattern4.BulletCount);
            
            float step = 360f / count;
            float radius = Mathf.Max(0f, pattern4.SpawnRadius);

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(startAngleDegrees + step * i);
                Vector3 origin = center + (Vector3)(direction * radius);
        
                EnemyProjectile projectile = FireConfiguredProjectileWithoutPlayerAim(pattern4.Projectile, origin, direction, i == 0);
                PlayBubbleEffectIfSpawned(projectile, origin, 0.75f, 7);
                ConsumePatternBulletUi();
            }
        }

        private void FireMachinegunBullet(int bulletIndex)
        {
            Vector3 origin = GetProjectileOrigin();
            Vector2 direction = GetDirectionToPlayer(origin);
            Vector2 side = new(-direction.y, direction.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern2.SpawnSpacing);
            Vector3 spawnPosition = origin + offset;
            EnemyProjectile projectile = FireConfiguredProjectile(
                pattern2.Projectile,
                spawnPosition,
                direction,
                bulletIndex == 0,
                pattern2.Projectile.AimAtPlayerWhileCharging,
                false,
                false,
                -1f,
                -1f,
                origin,
                useOwnColors: true);
            PlayBubbleEffectIfSpawned(projectile, spawnPosition, 0.9f, 9);
        }
        
        private void FirePattern5Bullet(int bulletIndex, float finalAngleDegrees)
        {
            Vector3 origin = GetProjectileOrigin();
            Vector2 finalDirection = AngleToDirection(finalAngleDegrees);
            
            Vector2 side = new(-finalDirection.y, finalDirection.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern5.SpawnSpacing);
            Vector3 spawnPosition = origin + offset;

            FireConfiguredProjectile(
                pattern5.Projectile,
                spawnPosition,
                finalDirection,
                bulletIndex == 0,
                false,
                false,
                false,
                0f,
                -1f,
                origin);
        }

        private IEnumerator WaitPattern2Seconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                MoveTowardPlayer(pattern2.MoveSpeedMultiplier);
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitPattern5Seconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                Stop();
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitWhileExecutionPaused()
        {
            while (IsExecutionPaused)
            {
                Stop();
                yield return null;
            }
        }
        private Vector3 GetPattern1SpawnPosition(Vector2 direction)
        {
            float radius = Mathf.Max(0f, pattern1.SpawnRadius);
            if (radius <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return GetProjectileOrigin();
            }

            return transform.position + (Vector3)(direction.normalized * radius);
        }

        private Vector2 GetPattern3Direction(Vector3 origin)
        {
            Vector2 direction = GetDirectionToPlayer(origin);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = pattern3.AimSpreadDegrees * 0.5f;
            return AngleToDirection(baseAngle + Random.Range(-halfSpread, halfSpread));
        }

        private void BeginPatternBulletUi(int totalBulletCount)
        {
            currentPatternBulletTotal = Mathf.Max(0, totalBulletCount);
            currentPatternBulletRemaining = currentPatternBulletTotal;
            ShowCurrentPatternBullets();
        }

        private void PreviewPatternBulletUi(PatternKind pattern)
        {
            BeginPatternBulletUi(GetPatternBulletCount(pattern));
        }

        private void SetPatternBulletUiLoaded(int loadedBulletCount)
        {
            if (currentPatternBulletTotal <= 0)
            {
                return;
            }

            currentPatternBulletRemaining = Mathf.Clamp(loadedBulletCount, 0, currentPatternBulletTotal);
            ShowCurrentPatternBullets();
        }

        private int GetPatternBulletCount(PatternKind pattern)
        {
            switch (pattern)
            {
                case PatternKind.Pattern1:
                    return Mathf.Max(1, pattern1.RadialBulletCount);
                case PatternKind.Pattern2:
                    return GetPattern2BulletCount();
                case PatternKind.Pattern3:
                    return 1;
                case PatternKind.Pattern4:
                    return Mathf.Max(0, pattern4.WaveCount);
                case PatternKind.Pattern5:
                    return Mathf.Max(1, pattern5.BulletCount);
                default:
                    return 0;
            }
        }

        private int GetPattern2BulletCount()
        {
            int count = 0;
            IReadOnlyList<Pattern2Settings.VolleySettings> volleys = pattern2.Volleys;
            if (volleys == null)
            {
                return count;
            }

            for (int i = 0; i < volleys.Count; i++)
            {
                if (volleys[i] != null)
                {
                    count += Mathf.Max(1, volleys[i].BulletCount);
                }
            }

            return count;
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

        private static int[] BuildShuffledOrder(int count)
        {
            int[] order = new int[Mathf.Max(1, count)];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            for (int i = order.Length - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (order[i], order[swapIndex]) = (order[swapIndex], order[i]);
            }

            return order;
        }

        private void PlayBubbleEffectIfSpawned(EnemyProjectile projectile, Vector3 position, float scaleMultiplier, int bubbleCount)
        {
            if (projectile == null)
            {
                return;
            }

            ProjectileVfx.PlayHogBubbleBurst(position, GetHogEffectColor(), bubbleEffectScale * scaleMultiplier, bubbleCount);
        }

        private void PlayWindupBubbleIfDue(ref float nextBubbleAt, Vector3 position, float interval, float scaleMultiplier, int bubbleCount)
        {
            if (Time.time < nextBubbleAt)
            {
                return;
            }

            ProjectileVfx.PlayHogBubbleBurst(position, GetHogEffectColor(), bubbleEffectScale * scaleMultiplier, Mathf.Max(1, bubbleCount));
            nextBubbleAt = Time.time + Mathf.Max(0.01f, interval);
        }

        private Color GetHogEffectColor()
        {
            return hogEffectColor.a > 0f ? hogEffectColor : DefaultHogEffectColor;
        }

        private Color GetProjectileColor(ProjectileSettings settings, bool suppressHoming, bool useOwnColors)
        {
            if (useOwnColors && settings != null)
            {
                return settings.LaunchedColor;
            }

            return settings != null && settings.HomingEnabled && !suppressHoming
                ? homingProjectileColor
                : normalProjectileColor;
        }

        private Color GetProjectileChargeColor(ProjectileSettings settings, bool suppressHoming, bool useOwnColors)
        {
            if (useOwnColors && settings != null)
            {
                return settings.ChargingColor;
            }

            return settings != null && settings.HomingEnabled && !suppressHoming
                ? homingProjectileChargeColor
                : normalProjectileChargeColor;
        }

        private EnemyProjectile FireConfiguredProjectileWithoutPlayerAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playRecoil)
        {
            return FireConfiguredProjectile(settings, origin, direction, playRecoil, false, false, true, -1f, -1f, useOwnColors: false);
        }

        private EnemyProjectile FireConfiguredProjectileWithPlayerLaunchAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playRecoil,
            bool useOwnColors = false)
        {
            return FireConfiguredProjectile(settings, origin, direction, playRecoil, false, true, false, -1f, -1f, useOwnColors: useOwnColors);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playRecoil)
        {
            if (settings == null)
            {
                return null;
            }

            return FireConfiguredProjectile(settings, origin, direction, playRecoil, settings.AimAtPlayerWhileCharging, false, false, -1f, -1f, useOwnColors: false);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playRecoil,
            bool aimAtPlayerWhileCharging,
            bool aimAtPlayerOnLaunch,
            bool suppressHoming,
            float chargeSecondsOverride,
            float radiusOverride,
            Vector3? muzzleFlashPosition = null,
            float muzzleFlashScale = 0.9f,
            bool useOwnColors = false)
        {
            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            float chargeSeconds = chargeSecondsOverride >= 0f ? chargeSecondsOverride : settings.ChargeSeconds;
            float radius = radiusOverride > 0f ? radiusOverride : settings.Radius;
            Color chargeColor = GetProjectileChargeColor(settings, suppressHoming, useOwnColors);
            Color projectileColor = GetProjectileColor(settings, suppressHoming, useOwnColors);
            EnemyProjectile projectile = SpawnBossProjectile(
                settings.Prefab,
                origin,
                direction,
                settings.BulletDamage,
                chargeSeconds,
                settings.Speed,
                settings.Lifetime,
                radius,
                projectileColor,
                settings.TrailSeconds,
                settings.TrailWidthMultiplier,
                settings.HomingEnabled && !suppressHoming,
                settings.HomingSeconds,
                settings.HomingTurnDegreesPerSecond,
                playRecoil,
                muzzleFlashPosition,
                muzzleFlashScale);

            projectile?.ConfigureStateColors(chargeColor, projectileColor);
            projectile?.ConfigureChargeMotion(settings.ChargeDriftSpeed, aimAtPlayerWhileCharging, aimAtPlayerOnLaunch);
            return projectile;
        }

        private void MoveTowardPlayer(float speedMultiplier)
        {
            if (Player == null || Body == null)
            {
                return;
            }

            Vector2 direction = (Vector2)Player.position - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                Body.linearVelocity = Vector2.zero;
                return;
            }

            Body.linearVelocity = direction.normalized * (MoveSpeed * Mathf.Max(0f, speedMultiplier));
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

        private float GetPlayerAngle()
        {
            Vector2 direction = GetDirectionToPlayer(GetProjectileOrigin());
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
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
