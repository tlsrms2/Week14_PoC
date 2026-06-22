using Action = System.Action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class HogBossAI : BossAI
    {
        private static readonly Color DefaultHogSmokeColor = new(0.38f, 0.48f, 0.34f, 0.72f);
        private static readonly Color DefaultHogExplosionColor = new(1f, 0.62f, 0.18f, 1f);
        private static readonly Color DefaultHogMuzzleFlashColor = new(1f, 0.78f, 0.26f, 1f);

        internal enum PatternKind
        {
            Pattern1,
            Pattern2,
            Pattern3,
            Pattern4,
            Pattern5,
            Pattern6,
            Pattern7
        }

        [System.Serializable]
        internal sealed class PhasePatternSet
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
                PatternKind.Pattern5,
                PatternKind.Pattern6,
                PatternKind.Pattern7
            };

            public int Phase
            {
                get => phase;
                set => phase = Mathf.Max(1, value);
            }

            public List<PatternKind> Patterns => patterns;
        }

        [System.Serializable]
        internal sealed class ProjectileSettings
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
            [SerializeField, Tooltip("유도탄이 대기 중 깜빡일 때 번갈아 표시할 색입니다.")] private Color homingBlinkColor = new(1f, 0.25f, 0.15f, 1f);
            [SerializeField, Tooltip("탄환 궤적 색입니다. 알파값은 시작 투명도로 사용됩니다.")] private Color trailColor = new(1f, 0.82f, 0.18f, 0.55f);
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
            public Color HomingBlinkColor => homingBlinkColor;
            public Color TrailColor => trailColor;
            public float TrailSeconds => trailSeconds;
            public float TrailWidthMultiplier => trailWidthMultiplier;
            public bool HomingEnabled => homingEnabled;
            public float HomingSeconds => homingSeconds;
            public float HomingTurnDegreesPerSecond => homingTurnDegreesPerSecond;
        }

        [System.Serializable]
        internal sealed class AlternatingProjectileOrigins
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
        internal sealed class FirePoint
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
        internal sealed class ParticleEffectSettings
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
        internal sealed class CameraShakeSettings
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
        internal sealed class PatternEffectSettings
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
        internal sealed class Pattern1Settings
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
        internal sealed class Pattern2Settings
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
            [SerializeField, Min(0f), Tooltip("BossBomb 사운드를 실제 분열(SplitDelaySeconds)보다 몇 초 일찍 재생할지 정합니다.")] private float bombSfxLeadSeconds = 0.15f;

            public int RadialSplitBulletCount => radialSplitBulletCount;
            public float RadialSplitStartAngleOffset => radialSplitStartAngleOffset;
            public float SplitDelaySeconds => splitDelaySeconds;
            public float BombSfxLeadSeconds => bombSfxLeadSeconds;
        }

        [System.Serializable]
        internal sealed class Pattern4Settings
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
        internal sealed class Pattern5Settings
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

        [System.Serializable]
        internal sealed class Pattern7Settings
        {
            [SerializeField] private FirePoint firePoint = new();
            [SerializeField, Tooltip("특수 탄환별 소환 위치 목록입니다. 발사 개수보다 부족한 요소는 FirePoint의 Projectile Origin을 사용합니다.")]
            private List<Transform> specialProjectileOrigins = new();

            [SerializeField, Tooltip("패턴7에서 세 갈래로 발사할 일반 탄환 설정입니다.")] private ProjectileSettings normalProjectile = new();
            [SerializeField, Tooltip("패턴7에서 일반 탄환과 동시에 소환할 특수 탄환 설정입니다.")] private ProjectileSettings specialProjectile = new();
            [SerializeField, Min(0f), Tooltip("발사 직전 플레이어를 조준하며 대기하는 시간입니다.")] private float windupSeconds = 1.0f;
            [SerializeField, Min(1), Tooltip("일반 탄환 3갈래 묶음을 몇 번 발사할지 정합니다.")] private int normalVolleyCount = 3;
            [SerializeField, Min(0f), Tooltip("일반 탄환 3갈래 묶음 사이의 발사 간격입니다. 0이면 한 번에 모두 발사합니다.")] private float normalVolleyInterval = 0.18f;
            [SerializeField, Min(0), Tooltip("특수 탄환을 몇 발 소환할지 정합니다. 0이면 특수 탄환을 발사하지 않습니다.")] private int specialBulletCount = 1;
            [SerializeField, Range(0f, 180f), Tooltip("전방 부채꼴 전체 각도입니다. 3갈래 탄환은 -절반, 중앙, +절반 방향으로 발사됩니다.")] private float fanAngleDegrees = 42f;
            [SerializeField, Min(0f), Tooltip("세 갈래 일반 탄환이 시작 위치에서 옆으로 벌어지는 거리입니다.")] private float normalSpawnSpacing = 0.16f;
            [SerializeField, Min(0f), Tooltip("특수 탄환을 발사 방향 앞쪽으로 소환할 거리입니다.")] private float specialSpawnForwardOffset = 0f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴7 준비 연기/발사 총구화염 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.WindupMuzzle();

            public ProjectileSettings NormalProjectile => normalProjectile;
            public ProjectileSettings SpecialProjectile => specialProjectile;
            public FirePoint FirePoint => firePoint;
            public IReadOnlyList<Transform> SpecialProjectileOrigins => specialProjectileOrigins;
            public float WindupSeconds => windupSeconds;
            public int NormalVolleyCount => normalVolleyCount;
            public float NormalVolleyInterval => normalVolleyInterval;
            public int SpecialBulletCount => specialBulletCount;
            public float FanAngleDegrees => fanAngleDegrees;
            public float NormalSpawnSpacing => normalSpawnSpacing;
            public float SpecialSpawnForwardOffset => specialSpawnForwardOffset;
            public PatternEffectSettings Effects => effects;
        }

        [Header("Hog Patterns")]
        [SerializeField, Tooltip("플레이어 방향 기준 사방 탄환을 발사하며 가속 추격하는 패턴 설정입니다.")] private Pattern1Settings pattern1 = new();
        [SerializeField, Tooltip("느려진 상태로 플레이어를 향해 머신건처럼 발사하는 패턴 설정입니다.")] private Pattern2Settings pattern2 = new();
        [SerializeField, Tooltip("벽에 부딪히면 분열하는 거대 특수 탄환 패턴 설정입니다.")] private Pattern3Settings pattern3 = new();
        [SerializeField, Tooltip("360도 전방위 탄환을 발사하는 패턴 설정입니다.")] private Pattern4Settings pattern4 = new();
        [SerializeField, Tooltip("제자리에서 기를 모은 뒤 미니건처럼 다수의 탄환을 발사하는 패턴 설정입니다.")] private Pattern5Settings pattern5 = new();
        [SerializeField, Tooltip("패턴4와 동일한 360도 전방위 탄환 패턴 설정입니다. 인스펙터 수치를 다르게 조절해 변형 패턴으로 사용합니다.")] private Pattern4Settings pattern6 = new();
        [SerializeField, Tooltip("발사 직전 플레이어 방향으로 고정한 뒤 전방 세 갈래 일반 탄환과 특수 탄환을 동시에 발사하는 패턴 설정입니다.")] private Pattern7Settings pattern7 = new();
        [SerializeField, Tooltip("페이즈별로 포함할 패턴 목록입니다. 1번 요소가 페이즈 1, 2번 요소가 페이즈 2입니다.")]
        private List<PhasePatternSet> phasePatterns = new()
        {
            new PhasePatternSet { Phase = 1 },
            new PhasePatternSet { Phase = 2 },
            new PhasePatternSet { Phase = 3 }
        };
        [SerializeField, FormerlySerializedAs("patternRecoverySeconds"), Min(0f), Tooltip("패턴 하나가 끝난 뒤 다음 패턴 전까지 쉬는 최소 시간입니다.")] private float minPatternRecoverySeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("패턴 하나가 끝난 뒤 다음 패턴 전까지 쉬는 최대 시간입니다.")] private float maxPatternRecoverySeconds = 0.9f;
        [Header("Pattern Preview UI")]
        [SerializeField, Tooltip("켜면 다음 패턴의 발사 탄환 수를 대기 시간 동안 LineRenderer 총알 줄로 표시합니다.")] private bool showPatternBulletPreview = true;
        [SerializeField, Min(0f), Tooltip("총알이 모두 찬 뒤 패턴 시작 전 유지할 시간입니다.")] private float patternBulletPreviewFullHoldSeconds = 0.18f;
        [SerializeField, Range(0f, 0.95f), Tooltip("한 번에 모든 총알을 쓰는 패턴은 대기 시간 중 이 비율만큼을 다 찬 상태로 유지합니다.")]
        private float patternBulletPreviewSingleGroupFullHoldRatio = 0.55f;
        [SerializeField, Tooltip("켜면 패턴을 순서대로 쓰지 않고 무작위로 선택합니다.")] private bool randomizePatterns;
        [SerializeField, Tooltip("랜덤 패턴 선택 시 직전에 실행한 패턴을 다시 고르지 않습니다. 후보가 1개면 같은 패턴을 허용합니다.")] private bool preventRandomRepeatPattern = true;

        [Header("Debug")]
        [SerializeField, Tooltip("켜면 아래에서 고른 패턴만 반복 실행합니다.")] private bool debugUseFixedPattern;
        [SerializeField, Tooltip("디버그용으로 고정 실행할 패턴입니다.")] private PatternKind debugPattern = PatternKind.Pattern1;

        private Coroutine patternRoutine;
        private readonly HogPatternSelector patternSelector = new();
        private readonly HogPatternPreviewPresenter patternPreviewPresenter = new();
        private readonly HogPattern7GuideView pattern7GuideView = new();
        private readonly HogBodyRootSlamController bodyRootSlamController = new();
        private readonly HogPatternRecoveryController patternRecoveryController = new();
        private readonly HogPatternMovement patternMovement = new();
        private readonly List<int> patternBulletPreviewGroups = new();

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
            HidePatternBulletPreview();
            HidePattern7GuideLines();
        }

        protected override void OnBossDied()
        {
            DeactivatePatternFirePoints();
            HidePatternBulletPreview();
            HidePattern7GuideLines();
        }

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("HogBgm");
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || !IsPlayerDetected())
            {
                return;
            }

            patternRoutine = StartCoroutine(RunPatternLoop());
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
            HidePatternBulletPreview();
            HidePattern7GuideLines();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            patternSelector.Reset();
            EnsurePhasePatternLabels();
            HidePatternBulletPreview();
        }

        private PatternKind SelectPattern()
        {
            EnsurePhasePatternSlots();
            return patternSelector.Select(
                phasePatterns,
                CurrentPhaseIndex,
                randomizePatterns,
                preventRandomRepeatPattern,
                debugUseFixedPattern,
                debugPattern);
        }

        private void EnsurePhasePatternSlots()
        {
            phasePatterns = patternSelector.EnsurePhasePatternSlots(phasePatterns, MaxLives);
        }

        private void EnsurePhasePatternLabels()
        {
            patternSelector.EnsurePhasePatternLabels(phasePatterns);
        }

        private IEnumerator RunPatternLoop()
        {
            PatternKind pattern = SelectPattern();
            while (true)
            {
                yield return RunPattern(pattern);
                yield return ApplyPendingEnrageIfAny();

                Stop();
                PatternKind nextPattern = SelectPattern();
                float recoverySeconds = patternRecoveryController.GetRecoverySeconds(
                    minPatternRecoverySeconds,
                    maxPatternRecoverySeconds);
                if (recoverySeconds > 0f)
                {
                    yield return RunPatternRecovery(nextPattern, recoverySeconds);
                }

                pattern = nextPattern;
            }
        }

        private IEnumerator RunPattern(PatternKind pattern)
        {
            patternSelector.RecordExecuted(pattern);
            BeginPatternBulletPreviewPlayback(pattern);

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
                case PatternKind.Pattern6:
                    yield return RunPattern6();
                    break;
                case PatternKind.Pattern7:
                    yield return RunPattern7();
                    break;
                default:
                    yield return RunPattern1();
                    break;
            }
        }

        private IEnumerator RunPatternRecovery(PatternKind nextPattern, float duration)
        {
            yield return patternRecoveryController.RunRecovery(
                nextPattern,
                duration,
                pattern5.FirePoint,
                pattern7.FirePoint,
                BeginPatternBulletPreview,
                UpdatePatternBulletPreviewLoading,
                SetFirePointActive,
                RotateFirePointToPlayer,
                IsBossExecutionPaused,
                Stop);
        }

        private IEnumerator ReloadPatternWavePreview(PatternKind pattern, float duration)
        {
            yield return patternRecoveryController.ReloadWavePreview(
                pattern,
                duration,
                BeginPatternBulletPreview,
                UpdatePatternBulletPreviewLoading,
                BeginPatternBulletPreviewPlayback,
                IsBossExecutionPaused,
                Stop);
        }

        private void BeginPatternBulletPreview(PatternKind nextPattern, float duration)
        {
            BuildPatternBulletPreviewGroups(nextPattern, patternBulletPreviewGroups);
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.BeginLoading(patternBulletPreviewGroups, duration);
        }

        private void UpdatePatternBulletPreviewLoading(float duration, float remaining)
        {
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.UpdateLoading(duration, remaining);
        }

        private void BeginPatternBulletPreviewPlayback(PatternKind pattern)
        {
            BuildPatternBulletPreviewGroups(pattern, patternBulletPreviewGroups);
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.BeginPlayback(patternBulletPreviewGroups);
        }

        private void AdvancePatternBulletPreviewGroup()
        {
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.AdvanceGroup();
        }

        private void HidePatternBulletPreview()
        {
            patternPreviewPresenter.Hide();
        }

        private void ConfigurePatternPreviewPresenter()
        {
            patternPreviewPresenter.Configure(
                this,
                BodyRoot != null ? BodyRoot : transform,
                showPatternBulletPreview,
                patternBulletPreviewFullHoldSeconds,
                patternBulletPreviewSingleGroupFullHoldRatio);
        }

        private void BuildPatternBulletPreviewGroups(PatternKind pattern, List<int> groups)
        {
            HogPatternPreviewGroupBuilder.Build(
                pattern,
                pattern1,
                pattern2,
                pattern4,
                pattern5,
                pattern6,
                pattern7,
                groups);
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
                        HogPatternEffects.PlayOriginBurst(pattern1.Effects, origin);
                        HogPatternEffects.PlaySfxOnLaunch(projectile, "BossSpecialShot");
                        SoundManager.PlaySfx("BossNormalShot");
                    }
                    fired++;
                    AdvancePatternBulletPreviewGroup();
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
                bool groupedVolley = volley.FireInterval <= 0f;
                for (int bulletIndex = 0; bulletIndex < volleyBulletCount; bulletIndex++)
                {
                    yield return WaitWhileExecutionPaused();

                    MoveTowardPlayer(pattern2.MoveSpeedMultiplier);
                    FireMachinegunBullet(fired);
                    fired++;
                    if (!groupedVolley)
                    {
                        AdvancePatternBulletPreviewGroup();
                    }

                    if (bulletIndex < volleyBulletCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return WaitPatternSeconds(volley.FireInterval, MoveDuringPattern2Delay);
                    }
                }

                if (groupedVolley)
                {
                    AdvancePatternBulletPreviewGroup();
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return WaitPatternSeconds(volley.RestSeconds, MoveDuringPattern2Delay);
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

            HogPatternEffects.PlayOriginBurst(pattern3.Effects, origin);
            HogPatternEffects.PlaySfxOnLaunch(projectile, "BossNormalShot");
            HogPatternEffects.PlayBossBombOnRadialSplit(projectile);
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
            projectile.ConfigureRadialSplitSfxLead(pattern3.BombSfxLeadSeconds);

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

                HogPatternEffects.PlaySmokeIfDue(ref nextSmokeAt, pattern3.Effects, GetFirePointProjectilePosition(pattern3.FirePoint));

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (projectile != null)
            {
                Vector3 launchOrigin = GetFirePointProjectilePosition(pattern3.FirePoint);
                Vector2 launchDirection = projectile.IncomingDirection.sqrMagnitude > 0.0001f
                    ? projectile.IncomingDirection
                    : GetDirectionToPlayer(launchOrigin);
                HogPatternEffects.PlayMuzzleFlashIfEnabled(pattern3.Effects, launchOrigin, launchDirection);
            }

            AdvancePatternBulletPreviewGroup();
            SetFirePointActive(pattern3.FirePoint, false);
        }

        private IEnumerator RunPattern4()
        {
            yield return RunPattern4Like(pattern4, PatternKind.Pattern4);
        }

        private IEnumerator RunPattern6()
        {
            yield return RunPattern4Like(pattern6, PatternKind.Pattern6);
        }

        private IEnumerator RunPattern4Like(Pattern4Settings settings, PatternKind patternKind)
        {
            Stop();

            int waveCount = Mathf.Max(1, settings.WaveCount);
            for (int wave = 0; wave < waveCount; wave++)
            {
                yield return WaitWhileExecutionPaused();

                yield return SlamPattern4BodyRoot(settings);
                
                float offset = settings.StartAngleOffset + wave * (360f / Mathf.Max(1, settings.BulletCount) * 0.5f);
                
                FirePattern4Wave(settings, offset);
                AdvancePatternBulletPreviewGroup();
                yield return RecoverPattern4BodyRoot(settings);

                if (wave < waveCount - 1)
                {
                    yield return ReloadPatternWavePreview(patternKind, settings.WaveInterval);
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
                HogPatternEffects.PlaySmokeIfDue(ref nextSmokeAt, pattern5.Effects, GetFirePointProjectilePosition(pattern5.FirePoint));

                elapsed += Time.deltaTime;
                yield return null;
            }

            float currentSweepOffset = 0f;
            float sweepDirection = 1f;
            bool groupedFire = pattern5.FireInterval <= 0f;

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
                if (!groupedFire)
                {
                    AdvancePatternBulletPreviewGroup();
                }
                currentSweepOffset += pattern5.SweepStepDegrees * sweepDirection;
        
                if (Mathf.Abs(currentSweepOffset) >= pattern5.MaxSweepAngle)
                {
                    sweepDirection *= -1f; 
                    currentSweepOffset = Mathf.Sign(currentSweepOffset) * pattern5.MaxSweepAngle; // 오차 보정
                }

                if (pattern5.FireInterval > 0f && i < bulletCount - 1)
                {
                    yield return WaitPatternSeconds(pattern5.FireInterval, Stop);
                }
            }

            if (groupedFire)
            {
                AdvancePatternBulletPreviewGroup();
            }

            SetFirePointActive(pattern5.FirePoint, false);
        }

        private IEnumerator RunPattern7()
        {
            Stop();
            SetFirePointActive(pattern7.FirePoint, true);

            float windupSeconds = Mathf.Max(0f, pattern7.WindupSeconds);
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
                RotateFirePointToPlayer(pattern7.FirePoint);
                Vector3 guideOrigin = GetPattern7NormalProjectilePosition();
                Vector2 guideDirection = GetDirectionToPlayer(guideOrigin);
                UpdatePattern7GuideLines(guideOrigin, guideDirection);
                HogPatternEffects.PlaySmokeIfDue(ref nextSmokeAt, pattern7.Effects, guideOrigin);

                elapsed += Time.deltaTime;
                yield return null;
            }

            HidePattern7GuideLines();
            yield return WaitWhileExecutionPaused();

            Vector3 aimOrigin = GetPattern7NormalProjectilePosition();
            Vector2 lockedDirection = GetDirectionToPlayer(aimOrigin);
            RotateFirePoint(pattern7.FirePoint, lockedDirection);
            aimOrigin = GetPattern7NormalProjectilePosition();

            int volleyCount = Mathf.Max(1, pattern7.NormalVolleyCount);
            bool groupedFire = pattern7.NormalVolleyInterval <= 0f;
            for (int volleyIndex = 0; volleyIndex < volleyCount; volleyIndex++)
            {
                yield return WaitWhileExecutionPaused();

                Stop();
                FirePattern7NormalVolley(GetPattern7NormalProjectilePosition(), lockedDirection);
                if (volleyIndex == 0)
                {
                    FirePattern7SpecialProjectiles(lockedDirection);
                }

                if (!groupedFire)
                {
                    AdvancePatternBulletPreviewGroup();
                }

                if (pattern7.NormalVolleyInterval > 0f && volleyIndex < volleyCount - 1)
                {
                    yield return WaitPatternSeconds(pattern7.NormalVolleyInterval, Stop);
                }
            }

            if (groupedFire)
            {
                AdvancePatternBulletPreviewGroup();
            }

            SetFirePointActive(pattern7.FirePoint, false);
            HidePattern7GuideLines();
        }

        private void FirePattern4Wave(Pattern4Settings settings, float startAngleDegrees)
        {
            HogPatternShotEmitter.FirePattern4Wave(
                settings,
                GetPattern4ProjectilePosition(settings),
                startAngleDegrees,
                FireConfiguredProjectileWithoutPlayerAim);
        }

        private void FireMachinegunBullet(int bulletIndex)
        {
            bool hasConfiguredOrigin = pattern2.ProjectileOrigins != null && pattern2.ProjectileOrigins.HasAny;
            Vector3 origin = GetAlternatingProjectilePosition(pattern2.ProjectileOrigins, bulletIndex);
            Vector2 direction = GetDirectionToPlayer(origin);
            HogPatternShotEmitter.FireMachinegunBullet(
                pattern2,
                bulletIndex,
                origin,
                direction,
                hasConfiguredOrigin,
                FireConfiguredProjectile);
        }

        private void FirePattern7NormalVolley(Vector3 origin, Vector2 lockedDirection)
        {
            HogPatternShotEmitter.FirePattern7NormalVolley(
                pattern7,
                origin,
                lockedDirection,
                FireConfiguredProjectileWithoutPlayerAim);
        }

        private void FirePattern7SpecialProjectiles(Vector2 lockedDirection)
        {
            HogPatternShotEmitter.FirePattern7SpecialProjectiles(
                pattern7,
                lockedDirection,
                GetPattern7SpecialProjectilePosition,
                FireConfiguredProjectile);
        }

        private void UpdatePattern7GuideLines(Vector3 origin, Vector2 baseDirection)
        {
            pattern7GuideView.Show(
                this,
                origin,
                baseDirection,
                pattern7.FanAngleDegrees,
                pattern7.NormalProjectile.Speed,
                pattern7.NormalProjectile.Lifetime,
                pattern7.NormalProjectile.Radius,
                pattern7.NormalProjectile.ChargingColor);
        }

        private void HidePattern7GuideLines()
        {
            pattern7GuideView.Hide();
        }
        
        private void FirePattern5Bullet(int bulletIndex, float finalAngleDegrees, Vector3 origin)
        {
            HogPatternShotEmitter.FirePattern5Bullet(
                pattern5,
                bulletIndex,
                finalAngleDegrees,
                origin,
                FireConfiguredProjectile);
        }

        private IEnumerator WaitPatternSeconds(float seconds, Action onTick)
        {
            yield return patternMovement.WaitSeconds(seconds, onTick, IsBossExecutionPaused, Stop);
        }

        private IEnumerator WaitWhileExecutionPaused()
        {
            yield return patternMovement.WaitWhileExecutionPaused(IsBossExecutionPaused, Stop);
        }

        private void MoveDuringPattern2Delay()
        {
            MoveTowardPlayer(pattern2.MoveSpeedMultiplier);
        }

        private Vector3 GetPattern1SpawnPosition(int shotIndex, Vector2 direction)
        {
            return HogProjectileOriginResolver.GetPattern1SpawnPosition(
                pattern1,
                shotIndex,
                direction,
                GetDefaultProjectileOrigin());
        }

        private Vector2 GetPattern3Direction(Vector3 origin)
        {
            Vector2 direction = GetDirectionToPlayer(origin);
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float halfSpread = pattern3.AimSpreadDegrees * 0.5f;
            return AngleToDirection(baseAngle + Random.Range(-halfSpread, halfSpread));
        }

        private EnemyProjectile FireConfiguredProjectileWithoutPlayerAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return HogProjectileEmitter.FireWithoutPlayerAim(SpawnBossProjectile, settings, origin, direction);
        }

        private EnemyProjectile FireConfiguredProjectileWithPlayerLaunchAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return HogProjectileEmitter.FireWithPlayerLaunchAim(SpawnBossProjectile, settings, origin, direction);
        }

        private EnemyProjectile FireConfiguredProjectile(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return HogProjectileEmitter.Fire(SpawnBossProjectile, settings, origin, direction);
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
            return HogProjectileEmitter.Fire(
                SpawnBossProjectile,
                settings,
                origin,
                direction,
                aimAtPlayerWhileCharging,
                aimAtPlayerOnLaunch,
                suppressHoming,
                chargeSecondsOverride,
                radiusOverride,
                muzzleFlashPosition,
                muzzleFlashScale);
        }

        private void MoveTowardPlayer(float speedMultiplier)
        {
            patternMovement.MoveTowardPlayer(Player, Body, transform, MoveSpeed, speedMultiplier);
        }

        private IEnumerator SlamPattern4BodyRoot(Pattern4Settings settings)
        {
            yield return bodyRootSlamController.Slam(BodyRoot, transform, settings, IsBossExecutionPaused, Stop);
        }

        private IEnumerator RecoverPattern4BodyRoot(Pattern4Settings settings)
        {
            yield return bodyRootSlamController.Recover(BodyRoot, transform, settings, IsBossExecutionPaused, Stop);
        }

        private void ResetPattern4BodyRoot()
        {
            bodyRootSlamController.Reset(BodyRoot);
        }

        private static bool IsBossExecutionPaused()
        {
            return IsExecutionPaused;
        }

        private Vector3 GetAlternatingProjectilePosition(AlternatingProjectileOrigins origins, int shotIndex)
        {
            return HogProjectileOriginResolver.GetAlternatingPosition(origins, shotIndex, GetDefaultProjectileOrigin());
        }

        private Vector3 GetFirePointProjectilePosition(FirePoint firePoint)
        {
            return HogProjectileOriginResolver.GetFirePointPosition(firePoint, GetDefaultProjectileOrigin());
        }

        private Vector3 GetPattern7NormalProjectilePosition()
        {
            return HogProjectileOriginResolver.GetPattern7NormalPosition(pattern7, GetDefaultProjectileOrigin());
        }

        private Vector3 GetPattern7SpecialProjectilePosition(int index)
        {
            return HogProjectileOriginResolver.GetPattern7SpecialPosition(pattern7, index, GetDefaultProjectileOrigin());
        }

        private Transform GetFirePointProjectileTransform(FirePoint firePoint)
        {
            return HogFirePointUtility.GetProjectileTransform(firePoint);
        }

        private Vector3 GetPattern4ProjectilePosition(Pattern4Settings settings)
        {
            return HogProjectileOriginResolver.GetPattern4Position(settings, GetDefaultProjectileOrigin());
        }

        private Vector3 GetDefaultProjectileOrigin()
        {
            return BodyRoot != null ? BodyRoot.position : transform.position;
        }

        private void RotateFirePointToPlayer(FirePoint firePoint)
        {
            HogFirePointUtility.RotateToPlayer(firePoint, Player, GetDefaultProjectileOrigin());
        }

        private void RotateFirePoint(FirePoint firePoint, Vector2 direction)
        {
            HogFirePointUtility.Rotate(firePoint, direction);
        }

        private void SetFirePointActive(FirePoint firePoint, bool active)
        {
            HogFirePointUtility.SetActive(firePoint, active);
        }

        private void DeactivatePatternFirePoints()
        {
            SetFirePointActive(pattern3.FirePoint, false);
            SetFirePointActive(pattern5.FirePoint, false);
            SetFirePointActive(pattern7.FirePoint, false);
        }

        protected override bool ShouldIgnoreBodyStateRenderer(SpriteRenderer renderer)
        {
            return renderer != null
                && (HogFirePointUtility.Contains(pattern3.FirePoint, renderer.transform, BodyRoot)
                    || HogFirePointUtility.Contains(pattern5.FirePoint, renderer.transform, BodyRoot)
                    || HogFirePointUtility.Contains(pattern7.FirePoint, renderer.transform, BodyRoot));
        }

    }
}
