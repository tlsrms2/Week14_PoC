using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Bootstrap;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class HogBossAI : BossAI
    {
        private static readonly Color DefaultHogSmokeColor = new(0.38f, 0.48f, 0.34f, 0.72f);
        private static readonly Color DefaultHogExplosionColor = new(1f, 0.62f, 0.18f, 1f);
        private static readonly Color DefaultHogMuzzleFlashColor = new(1f, 0.78f, 0.26f, 1f);

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
        private sealed class AlternatingProjectileOrigins
        {
            [SerializeField] private Transform firstProjectileOrigin;
            [SerializeField] private Transform secondProjectileOrigin;

            public bool HasAny => firstProjectileOrigin != null || secondProjectileOrigin != null;

            public Transform Get(int shotIndex)
            {
                if (firstProjectileOrigin == null)
                {
                    return secondProjectileOrigin;
                }

                if (secondProjectileOrigin == null)
                {
                    return firstProjectileOrigin;
                }

                return shotIndex % 2 == 0 ? firstProjectileOrigin : secondProjectileOrigin;
            }
        }

        [System.Serializable]
        private sealed class FirePoint
        {
            [SerializeField] private Transform fireOrigin;
            [SerializeField] private Transform projectileOrigin;

            [System.NonSerialized] private bool hasBaseLocalScale;
            [System.NonSerialized] private Vector3 baseLocalScale;

            public Transform FireOrigin => fireOrigin;
            public Transform ProjectileOrigin => projectileOrigin;

            public void SetActive(bool active)
            {
                if (fireOrigin != null)
                {
                    fireOrigin.gameObject.SetActive(active);
                }
            }

            public void RotateRight(Vector2 direction)
            {
                if (fireOrigin == null || direction.sqrMagnitude <= 0.0001f)
                {
                    return;
                }

                CacheBaseLocalScale();

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                fireOrigin.rotation = Quaternion.Euler(0f, 0f, angle);

                bool flipY = angle < -90f || angle > 90f;
                Vector3 nextScale = baseLocalScale;
                float authoredSign = baseLocalScale.y < 0f ? -1f : 1f;
                nextScale.y = Mathf.Abs(baseLocalScale.y) * authoredSign * (flipY ? -1f : 1f);
                fireOrigin.localScale = nextScale;
            }

            public bool Contains(Transform target, Transform bodyRoot)
            {
                return fireOrigin != null
                    && fireOrigin != bodyRoot
                    && target != null
                    && (target == fireOrigin || target.IsChildOf(fireOrigin));
            }

            private void CacheBaseLocalScale()
            {
                if (hasBaseLocalScale || fireOrigin == null)
                {
                    return;
                }

                baseLocalScale = fireOrigin.localScale;
                hasBaseLocalScale = true;
            }
        }

        [System.Serializable]
        private sealed class ParticleEffectSettings
        {
            [SerializeField] private bool enabled = true;
            [SerializeField] private Color color = Color.white;
            [SerializeField, Min(0.1f)] private float scale = 1f;
            [SerializeField, Min(0)] private int count = 12;

            public bool Enabled => enabled;
            public Color Color => color;
            public float Scale => scale;
            public int Count => count;

            public static ParticleEffectSettings Create(bool enabled, Color color, float scale, int count)
            {
                return new ParticleEffectSettings
                {
                    enabled = enabled,
                    color = color,
                    scale = Mathf.Max(0.1f, scale),
                    count = Mathf.Max(0, count)
                };
            }

            public void SetValues(bool nextEnabled, Color nextColor, float nextScale, int nextCount)
            {
                enabled = nextEnabled;
                color = nextColor;
                scale = Mathf.Max(0.1f, nextScale);
                count = Mathf.Max(0, nextCount);
            }
        }

        [System.Serializable]
        private sealed class CameraShakeSettings
        {
            [SerializeField] private bool enabled;
            [SerializeField, Min(0f)] private float seconds = 0.14f;
            [SerializeField, Min(0f)] private float distance = 0.22f;
            [SerializeField, Min(0f)] private float frequency = 0.12f;

            public bool Enabled => enabled;
            public float Seconds => seconds;
            public float Distance => distance;
            public float Frequency => frequency;

            public static CameraShakeSettings Create(bool enabled, float seconds, float distance, float frequency)
            {
                return new CameraShakeSettings
                {
                    enabled = enabled,
                    seconds = Mathf.Max(0f, seconds),
                    distance = Mathf.Max(0f, distance),
                    frequency = Mathf.Max(0f, frequency)
                };
            }

            public void SetValues(bool nextEnabled, float nextSeconds, float nextDistance, float nextFrequency)
            {
                enabled = nextEnabled;
                seconds = Mathf.Max(0f, nextSeconds);
                distance = Mathf.Max(0f, nextDistance);
                frequency = Mathf.Max(0f, nextFrequency);
            }
        }

        [System.Serializable]
        private sealed class PatternEffectSettings
        {
            [Header("Explosion")]
            [SerializeField] private ParticleEffectSettings explosion = ParticleEffectSettings.Create(true, DefaultHogExplosionColor, 1f, 18);
            [Header("Smoke")]
            [SerializeField] private ParticleEffectSettings smoke = ParticleEffectSettings.Create(true, DefaultHogSmokeColor, 1f, 12);
            [SerializeField, Min(0.01f)] private float smokeInterval = 0.12f;
            [Header("Muzzle Flash")]
            [SerializeField] private ParticleEffectSettings muzzleFlash = ParticleEffectSettings.Create(false, DefaultHogMuzzleFlashColor, 1f, 0);
            [Header("Camera Shake")]
            [SerializeField] private CameraShakeSettings cameraShake = CameraShakeSettings.Create(false, 0.14f, 0.22f, 0.12f);

            public ParticleEffectSettings Explosion => explosion;
            public ParticleEffectSettings Smoke => smoke;
            public float SmokeInterval => smokeInterval;
            public ParticleEffectSettings MuzzleFlash => muzzleFlash;
            public CameraShakeSettings CameraShake => cameraShake;

            public static PatternEffectSettings OriginBurst(
                float explosionScale,
                int explosionCount,
                float smokeScale,
                int smokeCount,
                bool muzzleFlashEnabled = false,
                float muzzleFlashScale = 1f)
            {
                return new PatternEffectSettings
                {
                    explosion = ParticleEffectSettings.Create(true, DefaultHogExplosionColor, explosionScale, explosionCount),
                    smoke = ParticleEffectSettings.Create(true, DefaultHogSmokeColor, smokeScale, smokeCount),
                    muzzleFlash = ParticleEffectSettings.Create(muzzleFlashEnabled, DefaultHogMuzzleFlashColor, muzzleFlashScale, 0),
                    cameraShake = CameraShakeSettings.Create(false, 0.14f, 0.22f, 0.12f)
                };
            }

            public static PatternEffectSettings Slam()
            {
                return new PatternEffectSettings
                {
                    explosion = ParticleEffectSettings.Create(true, DefaultHogExplosionColor, 1.45f, 28),
                    smoke = ParticleEffectSettings.Create(false, DefaultHogSmokeColor, 1.1f, 10),
                    muzzleFlash = ParticleEffectSettings.Create(false, DefaultHogMuzzleFlashColor, 1f, 0),
                    cameraShake = CameraShakeSettings.Create(true, 0.16f, 0.25f, 0.12f)
                };
            }

            public static PatternEffectSettings WindupMuzzle()
            {
                return new PatternEffectSettings
                {
                    explosion = ParticleEffectSettings.Create(false, DefaultHogExplosionColor, 1f, 0),
                    smoke = ParticleEffectSettings.Create(true, DefaultHogSmokeColor, 0.9f, 8),
                    muzzleFlash = ParticleEffectSettings.Create(true, DefaultHogMuzzleFlashColor, 1f, 0),
                    cameraShake = CameraShakeSettings.Create(true, 0.05f, 0.06f, 0.08f)
                };
            }

            public void SetMuzzleFlashDefaults(bool enabled, Color color, float scale)
            {
                muzzleFlash ??= ParticleEffectSettings.Create(enabled, color, scale, 0);
                muzzleFlash.SetValues(enabled, color, scale, 0);
            }

            public void SetCameraShakeDefaults(bool enabled, float seconds, float distance, float frequency)
            {
                cameraShake ??= CameraShakeSettings.Create(enabled, seconds, distance, frequency);
                cameraShake.SetValues(enabled, seconds, distance, frequency);
            }
        }

        [System.Serializable]
        private sealed class Pattern1Settings
        {
            [SerializeField] private AlternatingProjectileOrigins projectileOrigins = new();
            [SerializeField, Tooltip("패턴1에서 사용할 일반 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("패턴 시작 시 추격 속도 배율입니다.")] private float initialChaseSpeedMultiplier = 0.65f;
            [SerializeField, Min(0f), Tooltip("패턴 종료 시점의 추격 속도 배율입니다.")] private float finalChaseSpeedMultiplier = 1.8f;
            [SerializeField, Min(4), Tooltip("한 번의 사방 발사에서 생성할 탄환 수입니다.")] private int radialBulletCount = 4;
            [SerializeField, Min(0.01f), Tooltip("사방 탄환을 반복 발사하는 간격입니다.")] private float burstInterval = 0.65f;
            [SerializeField, Min(0f), Tooltip("보스 중심에서 패턴1 탄환을 원형으로 배치할 거리입니다.")] private float spawnRadius = 0.85f;
            [SerializeField, Tooltip("탄환을 발사할 때마다 회전시킬 각도입니다.")] private float angleStepDegrees = 25f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴1 원점 폭발/연기 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.OriginBurst(1f, 18, 1f, 12);

            public ProjectileSettings Projectile => projectile;
            public AlternatingProjectileOrigins ProjectileOrigins => projectileOrigins;
            public float InitialChaseSpeedMultiplier => initialChaseSpeedMultiplier;
            public float FinalChaseSpeedMultiplier => finalChaseSpeedMultiplier;
            public int RadialBulletCount => radialBulletCount;
            public float BurstInterval => burstInterval;
            public float SpawnRadius => spawnRadius;
            public float AngleStepDegrees => angleStepDegrees;
            public PatternEffectSettings Effects => effects;
        }

        [System.Serializable]
        private sealed class Pattern2Settings
        {
            [SerializeField] private AlternatingProjectileOrigins projectileOrigins = new();

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
            [Header("Effects")]
            [SerializeField, Tooltip("패턴2 원점 폭발/연기 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.OriginBurst(0.9f, 14, 0.9f, 10);

            public ProjectileSettings Projectile => projectile;
            public AlternatingProjectileOrigins ProjectileOrigins => projectileOrigins;
            public float MoveSpeedMultiplier => moveSpeedMultiplier;
            public int BulletCount => bulletCount;
            public float FireInterval => fireInterval;
            public float SpawnSpacing => spawnSpacing;
            public IReadOnlyList<VolleySettings> Volleys => volleys;
            public PatternEffectSettings Effects => effects;
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
            [SerializeField] private FirePoint firePoint = new();

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
            [SerializeField, Range(0f, 180f), Tooltip("패턴3 특수 탄환이 플레이어 방향 근처로 빗나갈 수 있는 각도입니다.")] private float aimSpreadDegrees = 24f;
            [SerializeField, Min(0f), Tooltip("발사 전 플레이어를 따라가며 조준(회전)하는 시간입니다. 이 시간이 지나면 멈추고 대기합니다.")] private float aimTrackingSeconds = 1.0f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴3 원점 폭발/연기/총구화염 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.OriginBurst(1.35f, 24, 1.25f, 16, true, 1.25f);
            [SerializeField, HideInInspector] private bool pattern3MuzzleFlashDefaultApplied;
            
            public ProjectileSettings Projectile => projectile;
            public FirePoint FirePoint => firePoint;
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
            public float AimSpreadDegrees => aimSpreadDegrees;
            public PatternEffectSettings Effects => effects;
            public void EnsureMuzzleFlashDefault()
            {
                if (pattern3MuzzleFlashDefaultApplied)
                {
                    return;
                }

                effects ??= PatternEffectSettings.OriginBurst(1.35f, 24, 1.25f, 16, true, 1.25f);
                effects.SetMuzzleFlashDefaults(true, DefaultHogMuzzleFlashColor, 1.25f);
                pattern3MuzzleFlashDefaultApplied = true;
            }

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
            [SerializeField] private Transform projectileOrigin;
            [SerializeField, Min(0f)] private float slamUpOffset = 1.2f;
            [SerializeField, Min(0f)] private float slamDownOffset = 0.25f;
            [SerializeField, Min(0.01f)] private float slamRiseSeconds = 0.18f;
            [SerializeField, Min(0.01f)] private float slamDropSeconds = 0.12f;
            [SerializeField, Min(0f)] private float slamRecoverSeconds = 0.12f;

            [SerializeField, Tooltip("패턴4에서 랜덤 순서로 전방위 발사할 특수 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(1), Tooltip("한 웨이브에서 360도로 발사할 탄환 수입니다.")] private int bulletCount = 32;
            [SerializeField, Min(1), Tooltip("전방위 원형 파동 발사를 몇 번 반복할지 정합니다.")] private int waveCount = 3;
            [SerializeField, Min(0f), Tooltip("전방위 웨이브 사이의 대기 시간입니다.")] private float waveInterval = 0.2f;
            [SerializeField, Tooltip("첫 탄환의 시작 각도 오프셋입니다.")] private float startAngleOffset;
            [SerializeField, Min(0f), Tooltip("보스 중심에서 전방위 탄환을 원형으로 배치할 거리입니다.")] private float spawnRadius = 0.85f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴4 내려찍기 폭발/진동 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.Slam();

            public ProjectileSettings Projectile => projectile;
            public Transform ProjectileOrigin => projectileOrigin;
            public int BulletCount => bulletCount;
            public int WaveCount => waveCount;
            public float WaveInterval => waveInterval;
            public float StartAngleOffset => startAngleOffset;
            public float SpawnRadius => spawnRadius;
            public float SlamUpOffset => slamUpOffset;
            public float SlamDownOffset => slamDownOffset;
            public float SlamRiseSeconds => slamRiseSeconds;
            public float SlamDropSeconds => slamDropSeconds;
            public float SlamRecoverSeconds => slamRecoverSeconds;
            public PatternEffectSettings Effects => effects;
        }

        [System.Serializable]
        private sealed class Pattern5Settings
        {
            [SerializeField] private FirePoint firePoint = new();

            [SerializeField, Tooltip("패턴5에서 미니건처럼 발사할 탄환 설정입니다.")] private ProjectileSettings projectile = new();
            [SerializeField, Min(0f), Tooltip("제자리에서 기를 모으는 시간입니다.")] private float windupSeconds = 1.4f;
            [SerializeField, Min(1), Tooltip("기 모으기가 끝난 뒤 발사할 탄환 수입니다.")] private int bulletCount = 36;
            [SerializeField, Min(0.01f), Tooltip("미니건 탄환 발사 간격입니다.")] private float fireInterval = 0.045f;
            [SerializeField, Min(0f), Tooltip("연속 탄환들이 겹치지 않도록 옆으로 벌리는 거리입니다.")] private float spawnSpacing = 0.12f;
            [SerializeField, Min(0f), Tooltip("매 탄환마다 회전할 각도입니다.")] private float sweepStepDegrees = 5f;
            [SerializeField, Min(0f), Tooltip("중심 방향(플레이어)을 기준으로 좌우로 꺾이는 최대 부채꼴 각도입니다.")] private float maxSweepAngle = 35f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴5 준비 연기/발사 총구화염 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.WindupMuzzle();
            [SerializeField, HideInInspector] private bool pattern5BulletShakeDefaultApplied;

            public ProjectileSettings Projectile => projectile;
            public FirePoint FirePoint => firePoint;
            public float WindupSeconds => windupSeconds;
            public int BulletCount => bulletCount;
            public float FireInterval => fireInterval;
            public float SpawnSpacing => spawnSpacing;
            public float SweepStepDegrees => sweepStepDegrees;
            public float MaxSweepAngle => maxSweepAngle;
            public PatternEffectSettings Effects => effects;
            public void EnsureBulletShakeDefault()
            {
                if (pattern5BulletShakeDefaultApplied)
                {
                    return;
                }

                effects ??= PatternEffectSettings.WindupMuzzle();
                effects.SetCameraShakeDefaults(true, 0.05f, 0.06f, 0.08f);
                pattern5BulletShakeDefaultApplied = true;
            }
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
        [SerializeField, Tooltip("랜덤 패턴 선택 시 직전에 실행한 패턴을 다시 고르지 않습니다. 후보가 1개면 같은 패턴을 허용합니다.")] private bool preventRandomRepeatPattern = true;

        [Header("Debug")]
        [SerializeField, Tooltip("켜면 아래에서 고른 패턴만 반복 실행합니다.")] private bool debugUseFixedPattern;
        [SerializeField, Tooltip("디버그용으로 고정 실행할 패턴입니다.")] private PatternKind debugPattern = PatternKind.Pattern1;

        private Coroutine patternRoutine;
        private int nextPatternIndex;
        private bool hasLastPattern;
        private PatternKind lastPattern;
        private bool isPattern4BodyRootMoved;
        private Vector3 pattern4BodyRootBaseLocalPosition;

        protected override bool RotatesBodyToPlayer => false;

        private void OnValidate()
        {
            EnsurePhasePatternSlots();
            EnsurePhasePatternLabels();
            pattern3?.EnsureMuzzleFlashDefault();
            pattern5?.EnsureBulletShakeDefault();
        }

        protected override void OnBossStarted()
        {
            DeactivatePatternFirePoints();
        }

        protected override void OnBossDied()
        {
            DeactivatePatternFirePoints();
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || !IsPlayerDetected())
            {
                return;
            }

            PatternKind pattern = SelectPattern();
            patternRoutine = StartCoroutine(RunPattern(pattern));
        }

        protected override void CancelBossAction()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            ResetPattern4BodyRoot();
            DeactivatePatternFirePoints();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            nextPatternIndex = 0;
            hasLastPattern = false;
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
                return SelectRandomPattern(availablePatterns);
            }

            PatternKind pattern = availablePatterns[nextPatternIndex % availablePatterns.Count];
            nextPatternIndex++;
            return pattern;
        }

        private PatternKind SelectRandomPattern(List<PatternKind> availablePatterns)
        {
            if (availablePatterns == null || availablePatterns.Count == 0)
            {
                return PatternKind.Pattern1;
            }

            if (!preventRandomRepeatPattern || !hasLastPattern || availablePatterns.Count <= 1)
            {
                return availablePatterns[Random.Range(0, availablePatterns.Count)];
            }

            int repeatCount = 0;
            for (int i = 0; i < availablePatterns.Count; i++)
            {
                if (availablePatterns[i] == lastPattern)
                {
                    repeatCount++;
                }
            }

            int candidateCount = availablePatterns.Count - repeatCount;
            if (candidateCount <= 0)
            {
                return availablePatterns[Random.Range(0, availablePatterns.Count)];
            }

            int selectedIndex = Random.Range(0, candidateCount);
            for (int i = 0; i < availablePatterns.Count; i++)
            {
                if (availablePatterns[i] == lastPattern)
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return availablePatterns[i];
                }

                selectedIndex--;
            }

            return availablePatterns[Random.Range(0, availablePatterns.Count)];
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
            lastPattern = pattern;
            hasLastPattern = true;

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

            Stop();
            PatternKind nextPattern = SelectPattern();
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
                    Vector3 origin = GetPattern1SpawnPosition(fired, direction);
            
                    EnemyProjectile projectile = FireConfiguredProjectileWithPlayerLaunchAim(
                        pattern1.Projectile,
                        origin,
                        direction);
                
                    if (projectile != null)
                    {
                        PlayOriginBurstEffects(pattern1.Effects, origin);
                    }
                    fired++;
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
            SetFirePointActive(pattern3.FirePoint, true);

            RotateFirePointToPlayer(pattern3.FirePoint);
            Transform projectileAnchor = GetFirePointProjectileTransform(pattern3.FirePoint);
            Vector3 origin = projectileAnchor != null ? projectileAnchor.position : GetFirePointProjectilePosition(pattern3.FirePoint);
            float radius = pattern3.Projectile.Radius * pattern3.ProjectileRadiusMultiplier;
            
            EnemyProjectile projectile = FireConfiguredProjectile(
                pattern3.Projectile,
                origin,
                GetPattern3Direction(origin),
                true,
                false,
                true,
                Mathf.Max(0f, pattern3.WindupSeconds),
                radius,
                null,
                0f);
            if (projectile == null)
            {
                SetFirePointActive(pattern3.FirePoint, false);
                yield break;
            }

            PlayOriginBurstEffects(pattern3.Effects, origin);
            projectile.ConfigureChargeAnchor(projectileAnchor);
            projectile.ConfigureProjectileSize(radius);
            projectile.ConfigureChargeMotion(0f, true, false, pattern3.AimSpreadDegrees);
            projectile.ConfigureChargeGrowth(
                pattern3.StartScaleMultiplier,
                pattern3.FinalScaleMultiplier);
            projectile.ConfigureInterceptable(false);
            projectile.ConfigureRadialSplitOnLaunch(
                pattern3.RadialSplitBulletCount,
                pattern3.RadialSplitStartAngleOffset,
                pattern3.SplitDelaySeconds,
                pattern3.SplitSpeedMultiplier,
                pattern3.SplitRadiusMultiplier,
                pattern3.SplitLifetimeMultiplier);

            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            bool trackingStopped = false;

            while (projectile != null && projectile.IsCharging)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                if (!trackingStopped)
                {
                    RotateFirePointToPlayer(pattern3.FirePoint);
                }

                if (!trackingStopped && elapsed >= pattern3.AimTrackingSeconds)
                {
                    projectile.ConfigureChargeMotion(0f, false, false, pattern3.AimSpreadDegrees);
                    trackingStopped = true;
                }

                PlaySmokeIfDue(ref nextSmokeAt, pattern3.Effects, GetFirePointProjectilePosition(pattern3.FirePoint));

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (projectile != null)
            {
                Vector3 launchOrigin = GetFirePointProjectilePosition(pattern3.FirePoint);
                Vector2 launchDirection = projectile.IncomingDirection.sqrMagnitude > 0.0001f
                    ? projectile.IncomingDirection
                    : GetDirectionToPlayer(launchOrigin);
                PlayMuzzleFlashIfEnabled(pattern3.Effects, launchOrigin, launchDirection);
            }

            SetFirePointActive(pattern3.FirePoint, false);
        }

        private IEnumerator RunPattern4()
        {
            Stop();

            for (int wave = 0; wave < pattern4.WaveCount; wave++)
            {
                yield return WaitWhileExecutionPaused();

                yield return SlamPattern4BodyRoot();
                
                float offset = pattern4.StartAngleOffset + wave * (360f / Mathf.Max(1, pattern4.BulletCount) * 0.5f);
                
                FirePattern4Wave(offset);
                yield return RecoverPattern4BodyRoot();

                if (wave < pattern4.WaveCount - 1 && pattern4.WaveInterval > 0f)
                {
                    yield return WaitPattern5Seconds(pattern4.WaveInterval);
                }
            }

            ResetPattern4BodyRoot();
        }

        private IEnumerator RunPattern5()
        {
            Stop();
            SetFirePointActive(pattern5.FirePoint, true);

            int bulletCount = Mathf.Max(1, pattern5.BulletCount);

            float windupSeconds = Mathf.Max(0f, pattern5.WindupSeconds);
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            while (elapsed < windupSeconds)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                Stop();
                RotateFirePointToPlayer(pattern5.FirePoint);
                PlaySmokeIfDue(ref nextSmokeAt, pattern5.Effects, GetFirePointProjectilePosition(pattern5.FirePoint));

                elapsed += Time.deltaTime;
                yield return null;
            }

            float currentSweepOffset = 0f;
            float sweepDirection = 1f;

            for (int i = 0; i < bulletCount; i++)
            {
                yield return WaitWhileExecutionPaused();

                Stop();
                
                RotateFirePointToPlayer(pattern5.FirePoint);
                Vector3 currentOrigin = GetFirePointProjectilePosition(pattern5.FirePoint);
                Vector2 dynamicBaseDirection = GetDirectionToPlayer(currentOrigin);
                
                float dynamicBaseAngle = Mathf.Atan2(dynamicBaseDirection.y, dynamicBaseDirection.x) * Mathf.Rad2Deg;
                float finalAngle = dynamicBaseAngle + currentSweepOffset;
                RotateFirePoint(pattern5.FirePoint, AngleToDirection(finalAngle));
                currentOrigin = GetFirePointProjectilePosition(pattern5.FirePoint);
                
                FirePattern5Bullet(i, finalAngle, currentOrigin);
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

            SetFirePointActive(pattern5.FirePoint, false);
        }

        private EnemyProjectile FireProjectileAtPlayer(ProjectileSettings settings, Vector3 origin)
        {
            Vector2 direction = GetDirectionToPlayer(origin);
            return FireConfiguredProjectileWithPlayerLaunchAim(settings, origin, direction);
        }

        private void FireRadialBurst(ProjectileSettings settings, int bulletCount, float startAngleDegrees)
        {
            Vector3 origin = GetDefaultProjectileOrigin();
            int count = Mathf.Max(1, bulletCount);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                FireConfiguredProjectile(settings, origin, AngleToDirection(startAngleDegrees + step * i));
            }
        }
        
        private void FirePattern4Wave(float startAngleDegrees)
        {
            Vector3 center = GetPattern4ProjectilePosition();
            int count = Mathf.Max(1, pattern4.BulletCount);
            
            float step = 360f / count;
            float radius = Mathf.Max(0f, pattern4.SpawnRadius);

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(startAngleDegrees + step * i);
                Vector3 origin = center + (Vector3)(direction * radius);
        
                FireConfiguredProjectileWithoutPlayerAim(pattern4.Projectile, origin, direction);
            }
        }

        private void FireMachinegunBullet(int bulletIndex)
        {
            bool hasConfiguredOrigin = pattern2.ProjectileOrigins != null && pattern2.ProjectileOrigins.HasAny;
            Vector3 origin = GetAlternatingProjectilePosition(pattern2.ProjectileOrigins, bulletIndex);
            Vector2 direction = GetDirectionToPlayer(origin);
            Vector2 side = new(-direction.y, direction.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern2.SpawnSpacing);
            Vector3 spawnPosition = hasConfiguredOrigin ? origin : origin + offset;
            EnemyProjectile projectile = FireConfiguredProjectile(
                pattern2.Projectile,
                spawnPosition,
                direction,
                pattern2.Projectile.AimAtPlayerWhileCharging,
                false,
                false,
                -1f,
                -1f,
                origin);
            if (projectile != null)
            {
                PlayOriginBurstEffects(pattern2.Effects, origin);
            }
        }
        
        private void FirePattern5Bullet(int bulletIndex, float finalAngleDegrees, Vector3 origin)
        {
            Vector2 finalDirection = AngleToDirection(finalAngleDegrees);
            
            Vector2 side = new(-finalDirection.y, finalDirection.x);
            Vector3 offset = side * GetAlternatingOffset(bulletIndex, pattern5.SpawnSpacing);
            Vector3 spawnPosition = origin + offset;

            EnemyProjectile projectile = FireConfiguredProjectile(
                pattern5.Projectile,
                spawnPosition,
                finalDirection,
                false,
                false,
                false,
                0f,
                -1f,
                origin,
                0f);
            if (projectile != null)
            {
                PlayMuzzleFlashIfEnabled(pattern5.Effects, origin, finalDirection);
                PlayCameraShakeIfEnabled(pattern5.Effects, finalDirection);
            }
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
        private Vector3 GetPattern1SpawnPosition(int shotIndex, Vector2 direction)
        {
            if (pattern1.ProjectileOrigins != null && pattern1.ProjectileOrigins.HasAny)
            {
                return GetAlternatingProjectilePosition(pattern1.ProjectileOrigins, shotIndex);
            }

            float radius = Mathf.Max(0f, pattern1.SpawnRadius);
            if (radius <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return GetDefaultProjectileOrigin();
            }

            return GetDefaultProjectileOrigin() + (Vector3)(direction.normalized * radius);
        }

        private Vector2 GetPattern3Direction(Vector3 origin)
        {
            Vector2 direction = GetDirectionToPlayer(origin);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = pattern3.AimSpreadDegrees * 0.5f;
            return AngleToDirection(baseAngle + Random.Range(-halfSpread, halfSpread));
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

        private void PlayOriginBurstEffects(PatternEffectSettings effects, Vector3 position)
        {
            if (effects == null)
            {
                return;
            }

            PlayExplosionIfEnabled(effects, position);
            PlaySmokeIfEnabled(effects, position);
        }

        private void PlaySmokeIfDue(ref float nextSmokeAt, PatternEffectSettings effects, Vector3 position)
        {
            if (effects == null || Time.time < nextSmokeAt)
            {
                return;
            }

            PlaySmokeIfEnabled(effects, position);
            nextSmokeAt = Time.time + Mathf.Max(0.01f, effects.SmokeInterval);
        }

        private void PlayExplosionIfEnabled(PatternEffectSettings effects, Vector3 position)
        {
            ParticleEffectSettings explosion = effects.Explosion;
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogExplosion(position, explosion.Color, explosion.Scale, explosion.Count);
        }

        private void PlaySmokeIfEnabled(PatternEffectSettings effects, Vector3 position)
        {
            ParticleEffectSettings smoke = effects.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }

        private void PlayMuzzleFlashIfEnabled(PatternEffectSettings effects, Vector3 position, Vector2 direction)
        {
            ParticleEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(position, direction, muzzleFlash.Color, muzzleFlash.Scale);
        }

        private void PlayCameraShakeIfEnabled(PatternEffectSettings effects, Vector2 direction)
        {
            CameraShakeSettings shake = effects?.CameraShake;
            if (shake == null || !shake.Enabled)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, shake.Seconds, shake.Distance, shake.Frequency);
        }

        private static Color GetProjectileColor(ProjectileSettings settings)
        {
            return settings != null ? settings.LaunchedColor : Color.white;
        }

        private static Color GetProjectileChargeColor(ProjectileSettings settings)
        {
            return settings != null ? settings.ChargingColor : Color.white;
        }

        private EnemyProjectile FireConfiguredProjectileWithoutPlayerAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return FireConfiguredProjectile(settings, origin, direction, false, false, true, -1f, -1f);
        }

        private EnemyProjectile FireConfiguredProjectileWithPlayerLaunchAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return FireConfiguredProjectile(settings, origin, direction, false, true, false, -1f, -1f);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            if (settings == null)
            {
                return null;
            }

            return FireConfiguredProjectile(settings, origin, direction, settings.AimAtPlayerWhileCharging, false, false, -1f, -1f);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool aimAtPlayerWhileCharging,
            bool aimAtPlayerOnLaunch,
            bool suppressHoming,
            float chargeSecondsOverride,
            float radiusOverride,
            Vector3? muzzleFlashPosition = null,
            float muzzleFlashScale = 0f)
        {
            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            float chargeSeconds = chargeSecondsOverride >= 0f ? chargeSecondsOverride : settings.ChargeSeconds;
            float radius = radiusOverride > 0f ? radiusOverride : settings.Radius;
            Color chargeColor = GetProjectileChargeColor(settings);
            Color projectileColor = GetProjectileColor(settings);
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

        private IEnumerator SlamPattern4BodyRoot()
        {
            Transform target = BodyRoot;
            if (target == null || target == transform)
            {
                yield break;
            }

            if (!isPattern4BodyRootMoved)
            {
                pattern4BodyRootBaseLocalPosition = target.localPosition;
                isPattern4BodyRootMoved = true;
            }

            Vector3 basePosition = pattern4BodyRootBaseLocalPosition;
            Vector3 upPosition = basePosition + Vector3.up * pattern4.SlamUpOffset;
            Vector3 downPosition = basePosition + Vector3.down * pattern4.SlamDownOffset;

            yield return MovePattern4BodyRoot(target, target.localPosition, upPosition, pattern4.SlamRiseSeconds);
            yield return MovePattern4BodyRoot(target, target.localPosition, downPosition, pattern4.SlamDropSeconds);
            PlayExplosionIfEnabled(pattern4.Effects, target.position);
            PlayCameraShakeIfEnabled(pattern4.Effects, Vector2.down);
        }

        private IEnumerator RecoverPattern4BodyRoot()
        {
            if (!isPattern4BodyRootMoved || BodyRoot == null || BodyRoot == transform)
            {
                yield break;
            }

            yield return MovePattern4BodyRoot(BodyRoot, BodyRoot.localPosition, pattern4BodyRootBaseLocalPosition, pattern4.SlamRecoverSeconds);
            ResetPattern4BodyRoot();
        }

        private IEnumerator MovePattern4BodyRoot(Transform target, Vector3 from, Vector3 to, float seconds)
        {
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                Stop();
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }

            target.localPosition = to;
        }

        private void ResetPattern4BodyRoot()
        {
            if (!isPattern4BodyRootMoved)
            {
                return;
            }

            if (BodyRoot != null)
            {
                BodyRoot.localPosition = pattern4BodyRootBaseLocalPosition;
            }

            isPattern4BodyRootMoved = false;
        }

        private Vector3 GetAlternatingProjectilePosition(AlternatingProjectileOrigins origins, int shotIndex)
        {
            Transform origin = origins != null ? origins.Get(shotIndex) : null;
            return origin != null ? origin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetFirePointProjectilePosition(FirePoint firePoint)
        {
            Transform origin = GetFirePointProjectileTransform(firePoint);
            return origin != null ? origin.position : GetDefaultProjectileOrigin();
        }

        private Transform GetFirePointProjectileTransform(FirePoint firePoint)
        {
            if (firePoint == null)
            {
                return null;
            }

            return firePoint.ProjectileOrigin != null ? firePoint.ProjectileOrigin : firePoint.FireOrigin;
        }

        private Vector3 GetPattern4ProjectilePosition()
        {
            return pattern4.ProjectileOrigin != null ? pattern4.ProjectileOrigin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetDefaultProjectileOrigin()
        {
            return BodyRoot != null ? BodyRoot.position : transform.position;
        }

        private void RotateFirePointToPlayer(FirePoint firePoint)
        {
            if (firePoint == null || firePoint.FireOrigin == null || Player == null)
            {
                return;
            }

            Vector3 origin = GetFirePointProjectilePosition(firePoint);
            Vector2 direction = (Vector2)(Player.position - origin);
            RotateFirePoint(firePoint, direction);
        }

        private void RotateFirePoint(FirePoint firePoint, Vector2 direction)
        {
            firePoint?.RotateRight(direction);
        }

        private void SetFirePointActive(FirePoint firePoint, bool active)
        {
            firePoint?.SetActive(active);
        }

        private void DeactivatePatternFirePoints()
        {
            SetFirePointActive(pattern3.FirePoint, false);
            SetFirePointActive(pattern5.FirePoint, false);
        }

        protected override bool ShouldIgnoreBodyStateRenderer(SpriteRenderer renderer)
        {
            return renderer != null
                && (IsUnderFirePoint(pattern3.FirePoint, renderer.transform)
                    || IsUnderFirePoint(pattern5.FirePoint, renderer.transform));
        }

        private bool IsUnderFirePoint(FirePoint firePoint, Transform target)
        {
            return firePoint != null && firePoint.Contains(target, BodyRoot);
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
