using Action = System.Action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.UI;

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
        internal sealed class ProjectileSettings : BossProjectileSettings
        {
            public ProjectileSettings()
                : base(
                    1,
                    0.35f,
                    0.65f,
                    true,
                    false,
                    7f,
                    3f,
                    0.12f,
                    new Color(0.45f, 0.7f, 0.25f, 1f),
                    new Color(1f, 0.95f, 0.25f, 1f),
                    0.1f,
                    3f,
                    false,
                    0.8f,
                    540f,
                    null,
                    new Color(1f, 0.25f, 0.15f, 1f),
                    new Color(1f, 0.82f, 0.18f, 0.55f),
                    new Color(1f, 0.95f, 0.25f, 1f))
            {
            }
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
        internal sealed class Pattern3Settings
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
        private readonly PatternPreviewPresenter patternPreviewPresenter = new();
        private readonly Pattern7GuideView pattern7GuideView = new();
        private readonly BodyRootSlamController bodyRootSlamController = new();
        private readonly BossPatternRecovery patternRecovery = new();
        private readonly BossPatternMovement patternMovement = new();
        private readonly List<int> patternBulletPreviewGroups = new();

        protected override bool RotatesBodyToPlayer => false;

        private void OnValidate()
        {
            patternSelector.EnsurePhasePatterns(ref phasePatterns, MaxLives);
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
            patternSelector.EnsurePhasePatterns(ref phasePatterns, MaxLives);
            HidePatternBulletPreview();
        }

        private PatternKind SelectPattern()
        {
            patternSelector.EnsurePhasePatterns(ref phasePatterns, MaxLives);
            return patternSelector.SelectNext(new HogPatternSelector.Settings(
                phasePatterns,
                CurrentPhaseIndex,
                randomizePatterns,
                preventRandomRepeatPattern,
                debugUseFixedPattern,
                debugPattern));
        }

        private IEnumerator RunPatternLoop()
        {
            PatternKind pattern = SelectPattern();
            while (true)
            {
                yield return RunPattern(pattern);
                yield return FinishPattern();

                PatternKind nextPattern = SelectPattern();
                yield return RecoverBeforeNextPattern(nextPattern);
                pattern = nextPattern;
            }
        }

        private IEnumerator FinishPattern()
        {
            yield return ApplyPendingEnrageIfAny();
            Stop();
        }

        private IEnumerator RecoverBeforeNextPattern(PatternKind nextPattern)
        {
            float recoverySeconds = patternRecovery.GetRecoverySeconds(minPatternRecoverySeconds, maxPatternRecoverySeconds);
            if (recoverySeconds > 0f)
            {
                yield return RunPatternRecovery(nextPattern, recoverySeconds);
            }
        }

        private IEnumerator RunPattern(PatternKind pattern)
        {
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
            float recoveryDuration = Mathf.Max(0f, duration);
            BeginPatternBulletPreview(nextPattern, recoveryDuration);
            BeginRecoveryTelegraph(nextPattern);

            yield return patternRecovery.RunRecovery(
                recoveryDuration,
                (total, remaining) =>
                {
                    UpdatePatternBulletPreviewLoading(total, remaining);
                    UpdateRecoveryTelegraph(nextPattern);
                },
                IsBossExecutionPaused,
                Stop);
        }

        private IEnumerator ReloadPatternWavePreview(PatternKind pattern, float duration)
        {
            float reloadDuration = Mathf.Max(0f, duration);
            if (reloadDuration <= 0f)
            {
                BeginPatternBulletPreviewPlayback(pattern);
                yield break;
            }

            BeginPatternBulletPreview(pattern, reloadDuration);
            yield return patternRecovery.RunRecovery(reloadDuration, UpdatePatternBulletPreviewLoading, IsBossExecutionPaused, Stop);
            BeginPatternBulletPreviewPlayback(pattern);
        }

        private void BeginRecoveryTelegraph(PatternKind nextPattern)
        {
            if (nextPattern != PatternKind.Pattern5 && nextPattern != PatternKind.Pattern7)
            {
                return;
            }

            FirePoint firePoint = nextPattern == PatternKind.Pattern7 ? pattern7.FirePoint : pattern5.FirePoint;
            SetFirePointActive(firePoint, true);
            RotateFirePointToPlayer(firePoint);
        }

        private void UpdateRecoveryTelegraph(PatternKind nextPattern)
        {
            if (nextPattern == PatternKind.Pattern5)
            {
                RotateFirePointToPlayer(pattern5.FirePoint);
            }
            else if (nextPattern == PatternKind.Pattern7)
            {
                RotateFirePointToPlayer(pattern7.FirePoint);
            }
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
            if (groups == null)
            {
                return;
            }

            groups.Clear();
            switch (pattern)
            {
                case PatternKind.Pattern1:
                    AddRepeatedPreviewGroups(groups, 1, Mathf.Max(1, pattern1.RadialBulletCount));
                    break;
                case PatternKind.Pattern2:
                    AddPattern2PreviewGroups(groups);
                    break;
                case PatternKind.Pattern3:
                    groups.Add(1);
                    break;
                case PatternKind.Pattern4:
                    groups.Add(Mathf.Max(1, pattern4.BulletCount));
                    break;
                case PatternKind.Pattern5:
                    AddRepeatedPreviewGroups(
                        groups,
                        pattern5.FireInterval <= 0f ? Mathf.Max(1, pattern5.BulletCount) : 1,
                        pattern5.FireInterval <= 0f ? 1 : Mathf.Max(1, pattern5.BulletCount));
                    break;
                case PatternKind.Pattern6:
                    groups.Add(Mathf.Max(1, pattern6.BulletCount));
                    break;
                case PatternKind.Pattern7:
                    AddPattern7PreviewGroups(groups);
                    break;
            }
        }

        private void AddPattern2PreviewGroups(List<int> groups)
        {
            IReadOnlyList<Pattern2Settings.VolleySettings> volleys = pattern2.Volleys;
            if (volleys == null || volleys.Count == 0)
            {
                return;
            }

            for (int i = 0; i < volleys.Count; i++)
            {
                Pattern2Settings.VolleySettings volley = volleys[i];
                if (volley == null)
                {
                    continue;
                }

                int bulletCount = Mathf.Max(1, volley.BulletCount);
                if (volley.FireInterval <= 0f)
                {
                    groups.Add(bulletCount);
                }
                else
                {
                    AddRepeatedPreviewGroups(groups, 1, bulletCount);
                }
            }
        }

        private void AddPattern7PreviewGroups(List<int> groups)
        {
            int volleyCount = Mathf.Max(1, pattern7.NormalVolleyCount);
            int specialBulletCount = Mathf.Max(0, pattern7.SpecialBulletCount);
            if (pattern7.NormalVolleyInterval <= 0f)
            {
                groups.Add(volleyCount * 3 + specialBulletCount);
                return;
            }

            groups.Add(3 + specialBulletCount);
            AddRepeatedPreviewGroups(groups, 3, volleyCount - 1);
        }

        private static void AddRepeatedPreviewGroups(List<int> groups, int groupSize, int groupCount)
        {
            int count = Mathf.Max(0, groupCount);
            int size = Mathf.Max(1, groupSize);
            for (int i = 0; i < count; i++)
            {
                groups.Add(size);
            }
        }

        private IEnumerator RunPattern1()
        {
            yield return HogPattern1Runner.Run(pattern1, CreatePatternContext());
        }

        private IEnumerator RunPattern2()
        {
            yield return HogPattern2Runner.Run(pattern2, CreatePatternContext());
        }

        private IEnumerator RunPattern3()
        {
            yield return HogPattern3Runner.Run(pattern3, CreatePatternContext());
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
            yield return HogPattern4Runner.Run(
                settings,
                patternKind,
                CreatePatternContext());
        }

        private IEnumerator RunPattern5()
        {
            yield return HogPattern5Runner.Run(
                pattern5,
                CreatePatternContext());
        }

        private IEnumerator RunPattern7()
        {
            yield return HogPattern7Runner.Run(
                pattern7,
                CreatePatternContext());
        }

        private HogPatternContext CreatePatternContext()
        {
            return new HogPatternContext(
                Stop,
                IsBossExecutionPaused,
                WaitWhileExecutionPaused,
                WaitPatternSeconds,
                MoveTowardPlayer,
                FirePattern4Wave,
                FireMachinegunBullet,
                FirePattern5Bullet,
                FirePattern7NormalVolley,
                FirePattern7SpecialProjectiles,
                SlamPattern4BodyRoot,
                RecoverPattern4BodyRoot,
                ReloadPatternWavePreview,
                ResetPattern4BodyRoot,
                AdvancePatternBulletPreviewGroup,
                SetFirePointActive,
                RotateFirePointToPlayer,
                RotateFirePoint,
                GetFirePointProjectilePosition,
                GetFirePointProjectileTransform,
                GetDirectionToPlayer,
                AngleToDirection,
                GetPattern1SpawnPosition,
                GetPattern3Direction,
                GetPattern7NormalProjectilePosition,
                GetPattern7SpecialProjectilePosition,
                UpdatePattern7GuideLines,
                HidePattern7GuideLines,
                FireConfiguredProjectileWithoutPlayerAim,
                FireConfiguredProjectileWithPlayerLaunchAim,
                (HogPatternContext.ProjectileFire)FireConfiguredProjectile,
                (HogPatternContext.ConfiguredProjectileFire)FireConfiguredProjectile);
        }

        private void FirePattern4Wave(Pattern4Settings settings, float startAngleDegrees)
        {
            if (settings == null)
            {
                return;
            }

            SoundManager.PlaySfx("Smash");
            int count = Mathf.Max(1, settings.BulletCount);
            float step = 360f / count;
            float radius = Mathf.Max(0f, settings.SpawnRadius);
            Vector3 center = GetPattern4ProjectilePosition(settings);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(startAngleDegrees + step * i);
                Vector3 origin = center + (Vector3)(direction * radius);
                FireConfiguredProjectileWithoutPlayerAim(settings.Projectile, origin, direction);
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
            bool aimAtPlayerWhileCharging = pattern2.Projectile != null && pattern2.Projectile.AimAtPlayerWhileCharging;
            EnemyProjectile projectile = FireConfiguredProjectile(
                pattern2.Projectile,
                spawnPosition,
                direction,
                aimAtPlayerWhileCharging,
                false,
                false,
                -1f,
                -1f,
                origin,
                0f);
            if (projectile == null)
            {
                return;
            }

            PlayOriginBurst(pattern2.Effects, origin);
            PlaySfxOnLaunch(projectile, "BossSpecialShot");
            SoundManager.PlaySfx("BossNormalShot");
        }

        private void FirePattern7NormalVolley(Vector3 origin, Vector2 lockedDirection)
        {
            if (lockedDirection.sqrMagnitude <= 0.0001f)
            {
                lockedDirection = Vector2.right;
            }

            float baseAngle = Mathf.Atan2(lockedDirection.y, lockedDirection.x) * Mathf.Rad2Deg;
            float halfFanAngle = pattern7.FanAngleDegrees * 0.5f;
            float spacing = Mathf.Max(0f, pattern7.NormalSpawnSpacing);
            for (int i = 0; i < 3; i++)
            {
                float t = i - 1f;
                Vector2 direction = AngleToDirection(baseAngle + halfFanAngle * t);
                Vector2 side = new(-lockedDirection.y, lockedDirection.x);
                Vector3 spawnPosition = origin + (Vector3)(side * spacing * t);
                FireConfiguredProjectileWithoutPlayerAim(pattern7.NormalProjectile, spawnPosition, direction);
            }

            PlayMuzzleFlashIfEnabled(pattern7.Effects, origin, lockedDirection);
            PlayCameraShakeIfEnabled(pattern7.Effects, lockedDirection);
            SoundManager.PlaySfx("BossNormalShot");
        }

        private void FirePattern7SpecialProjectiles(Vector2 lockedDirection)
        {
            if (lockedDirection.sqrMagnitude <= 0.0001f)
            {
                lockedDirection = Vector2.right;
            }

            int count = Mathf.Max(0, pattern7.SpecialBulletCount);
            bool firedAny = false;
            for (int i = 0; i < count; i++)
            {
                Vector3 origin = GetPattern7SpecialProjectilePosition(i);
                Vector3 specialOrigin = origin + (Vector3)(lockedDirection.normalized * Mathf.Max(0f, pattern7.SpecialSpawnForwardOffset));
                EnemyProjectile specialProjectile = FireConfiguredProjectile(pattern7.SpecialProjectile, specialOrigin, lockedDirection);
                if (specialProjectile != null)
                {
                    firedAny = true;
                    PlaySfxOnLaunch(specialProjectile, "BossSpecialShot");
                }
            }

            if (firedAny)
            {
                SoundManager.PlaySfx("BossNormalShot");
            }
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
            if (projectile == null)
            {
                return;
            }

            PlayMuzzleFlashIfEnabled(pattern5.Effects, origin, finalDirection);
            PlayCameraShakeIfEnabled(pattern5.Effects, finalDirection);
            SoundManager.PlaySfx("BossNormalShot");
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

        private static void PlayOriginBurst(PatternEffectSettings effects, Vector3 position)
        {
            PlayExplosionIfEnabled(effects, position);
            PlaySmokeIfEnabled(effects, position);
        }

        private static void PlayExplosionIfEnabled(PatternEffectSettings effects, Vector3 position)
        {
            ParticleEffectSettings explosion = effects?.Explosion;
            if (explosion == null || !explosion.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogExplosion(position, explosion.Color, explosion.Scale, explosion.Count);
        }

        private static void PlaySmokeIfEnabled(PatternEffectSettings effects, Vector3 position)
        {
            ParticleEffectSettings smoke = effects?.Smoke;
            if (smoke == null || !smoke.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayHogSmokeBurst(position, smoke.Color, smoke.Scale, smoke.Count);
        }

        private static void PlayMuzzleFlashIfEnabled(PatternEffectSettings effects, Vector3 origin, Vector2 direction)
        {
            ParticleEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled)
            {
                return;
            }

            ProjectileVfx.PlayMuzzleFlash(origin, direction, muzzleFlash.Color, muzzleFlash.Scale);
        }

        private static void PlayCameraShakeIfEnabled(PatternEffectSettings effects, Vector2 direction)
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

        private static void PlaySfxOnLaunch(EnemyProjectile projectile, string sfxId)
        {
            if (projectile == null)
            {
                return;
            }

            void HandleLaunched(EnemyProjectile launchedProjectile)
            {
                launchedProjectile.Launched -= HandleLaunched;
                SoundManager.PlaySfx(sfxId);
            }

            projectile.Launched += HandleLaunched;
        }

        private IEnumerator WaitPatternSeconds(float seconds, Action onTick)
        {
            yield return patternMovement.WaitSeconds(seconds, onTick, IsBossExecutionPaused, Stop);
        }

        private IEnumerator WaitWhileExecutionPaused()
        {
            yield return patternMovement.WaitWhileExecutionPaused(IsBossExecutionPaused, Stop);
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

        private EnemyProjectile FireConfiguredProjectileWithoutPlayerAim(
            ProjectileSettings settings,
            Vector3 origin,
            Vector2 direction)
        {
            return FireConfiguredProjectile(settings, origin, direction, false, false, false, -1f, -1f);
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

            return FireConfiguredProjectile(
                settings,
                origin,
                direction,
                settings.AimAtPlayerWhileCharging,
                false,
                false,
                -1f,
                -1f);
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
            return BossProjectileEmitter.Fire(
                SpawnBossProjectile,
                settings,
                origin,
                direction,
                settings.ChargingColor,
                settings.LaunchedColor,
                aimAtPlayerWhileCharging,
                aimAtPlayerOnLaunch,
                suppressHoming,
                chargeSeconds,
                radius,
                muzzleFlashPosition,
                muzzleFlashScale,
                null);
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
            Transform origin = origins != null ? origins.Get(shotIndex) : null;
            return origin != null ? origin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetFirePointProjectilePosition(FirePoint firePoint)
        {
            Transform origin = GetFirePointProjectileTransform(firePoint);
            return origin != null ? origin.position : GetDefaultProjectileOrigin();
        }

        private Vector3 GetPattern7NormalProjectilePosition()
        {
            return GetFirePointProjectilePosition(pattern7.FirePoint);
        }

        private Vector3 GetPattern7SpecialProjectilePosition(int index)
        {
            IReadOnlyList<Transform> origins = pattern7.SpecialProjectileOrigins;
            Transform origin = origins != null && index >= 0 && index < origins.Count ? origins[index] : null;
            return origin != null ? origin.position : GetFirePointProjectilePosition(pattern7.FirePoint);
        }

        private Transform GetFirePointProjectileTransform(FirePoint firePoint)
        {
            if (firePoint == null)
            {
                return null;
            }

            return firePoint.ProjectileOrigin != null ? firePoint.ProjectileOrigin : firePoint.FireOrigin;
        }

        private Vector3 GetPattern4ProjectilePosition(Pattern4Settings settings)
        {
            return settings.ProjectileOrigin != null ? settings.ProjectileOrigin.position : GetDefaultProjectileOrigin();
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
            SetFirePointActive(pattern7.FirePoint, false);
        }

        protected override bool ShouldIgnoreBodyStateRenderer(SpriteRenderer renderer)
        {
            return renderer != null
                && (pattern3.FirePoint.Contains(renderer.transform, BodyRoot)
                    || pattern5.FirePoint.Contains(renderer.transform, BodyRoot)
                    || pattern7.FirePoint.Contains(renderer.transform, BodyRoot));
        }

        private sealed class PatternPreviewPresenter
        {
            private readonly List<int> groups = new();

            private Component owner;
            private Transform target;
            private BossPatternBulletLineView view;
            private bool isEnabled;
            private float fullHoldSeconds;
            private float singleGroupFullHoldRatio;
            private int groupIndex;
            private float fillDuration;

            public void Configure(
                Component nextOwner,
                Transform nextTarget,
                bool nextEnabled,
                float nextFullHoldSeconds,
                float nextSingleGroupFullHoldRatio)
            {
                owner = nextOwner;
                target = nextTarget != null ? nextTarget : owner != null ? owner.transform : null;
                isEnabled = nextEnabled;
                fullHoldSeconds = Mathf.Max(0f, nextFullHoldSeconds);
                singleGroupFullHoldRatio = Mathf.Clamp01(nextSingleGroupFullHoldRatio);
            }

            public void BeginLoading(IReadOnlyList<int> nextGroups, float duration)
            {
                if (!isEnabled || duration <= 0f || !CopyGroups(nextGroups))
                {
                    Hide();
                    return;
                }

                BossPatternBulletLineView lineView = EnsureView();
                if (lineView == null)
                {
                    return;
                }

                float holdSeconds = GetFullHoldSeconds(duration);
                fillDuration = duration - holdSeconds;
                lineView.ShowLoading(groups, 0);
            }

            public void UpdateLoading(float duration, float remaining)
            {
                if (!isEnabled || view == null)
                {
                    return;
                }

                float elapsed = Mathf.Max(0f, duration - remaining);
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fillDuration));
                int fullGroupCount = progress >= 1f
                    ? groups.Count
                    : Mathf.Clamp(Mathf.FloorToInt(progress * groups.Count + 0.0001f), 0, groups.Count);
                view.ShowLoading(groups, fullGroupCount);
            }

            public void BeginPlayback(IReadOnlyList<int> nextGroups)
            {
                if (!isEnabled || !CopyGroups(nextGroups))
                {
                    Hide();
                    return;
                }

                BossPatternBulletLineView lineView = EnsureView();
                if (lineView == null)
                {
                    return;
                }

                groupIndex = 0;
                lineView.ShowNextGroup(groups, groupIndex);
            }

            public void AdvanceGroup()
            {
                if (!isEnabled || view == null)
                {
                    return;
                }

                groupIndex++;
                view.ShowNextGroup(groups, groupIndex);
            }

            public void Hide()
            {
                groupIndex = 0;
                fillDuration = 0f;
                view?.Hide();
            }

            private bool CopyGroups(IReadOnlyList<int> nextGroups)
            {
                groups.Clear();
                if (nextGroups == null)
                {
                    return false;
                }

                for (int i = 0; i < nextGroups.Count; i++)
                {
                    groups.Add(nextGroups[i]);
                }

                return groups.Count > 0;
            }

            private float GetFullHoldSeconds(float duration)
            {
                float maxHoldSeconds = Mathf.Max(0f, duration - 0.01f);
                float holdSeconds = Mathf.Clamp(fullHoldSeconds, 0f, maxHoldSeconds);
                if (groups.Count == 1)
                {
                    float singleGroupHoldSeconds = duration * singleGroupFullHoldRatio;
                    holdSeconds = Mathf.Max(holdSeconds, singleGroupHoldSeconds);
                }

                return Mathf.Clamp(holdSeconds, 0f, maxHoldSeconds);
            }

            private BossPatternBulletLineView EnsureView()
            {
                if (owner == null)
                {
                    return null;
                }

                Transform targetRoot = target != null ? target : owner.transform;
                if (view != null)
                {
                    view.SetTarget(targetRoot);
                    return view;
                }

                view = owner.GetComponent<BossPatternBulletLineView>();
                if (view == null)
                {
                    view = owner.GetComponentInChildren<BossPatternBulletLineView>(true);
                }

                if (view == null)
                {
                    view = owner.gameObject.AddComponent<BossPatternBulletLineView>();
                }

                view.SetTarget(targetRoot);
                return view;
            }
        }

        private sealed class Pattern7GuideView
        {
            private const string WallLayerName = "Wall";
            private const int MaxDashCountPerLine = 160;
            private const float DashLength = 0.2f;
            private const float DashGap = 0.14f;

            private static Material material;

            private readonly List<LineRenderer> guideLines = new();
            private Component owner;

            public void Show(
                Component nextOwner,
                Vector3 origin,
                Vector2 baseDirection,
                float fanAngleDegrees,
                float projectileSpeed,
                float projectileLifetime,
                float projectileRadius,
                Color chargingColor)
            {
                owner = nextOwner;
                if (owner == null || baseDirection.sqrMagnitude <= 0.0001f)
                {
                    Hide();
                    return;
                }

                float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
                float halfFanAngle = fanAngleDegrees * 0.5f;
                float maxLength = Mathf.Max(0f, projectileSpeed * projectileLifetime);
                float width = Mathf.Max(0.013f, projectileRadius * 0.14f);
                Color color = chargingColor;
                color.a = 0.58f;
                int visibleDashCount = 0;

                for (int i = 0; i < 3; i++)
                {
                    float t = i - 1f;
                    Vector2 direction = AngleToDirection(baseAngle + halfFanAngle * t);
                    float length = GetGuideLength(origin, direction, maxLength, projectileRadius);
                    visibleDashCount = DrawDashes(visibleDashCount, origin, direction, length, color, width);
                }

                HideFrom(visibleDashCount);
            }

            public void Hide()
            {
                HideFrom(0);
            }

            private int DrawDashes(int startDashIndex, Vector3 start, Vector2 direction, float length, Color color, float width)
            {
                if (length <= 0.01f)
                {
                    return startDashIndex;
                }

                Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
                int dashCount = Mathf.Min(MaxDashCountPerLine, Mathf.CeilToInt(length / (DashLength + DashGap)));
                int nextDashIndex = startDashIndex;

                for (int i = 0; i < dashCount; i++)
                {
                    float segmentStart = i * (DashLength + DashGap);
                    float segmentEnd = Mathf.Min(segmentStart + DashLength, length);
                    if (segmentEnd <= 0f)
                    {
                        continue;
                    }

                    LineRenderer dash = EnsureGuideLine(nextDashIndex);
                    if (dash == null)
                    {
                        continue;
                    }

                    dash.enabled = true;
                    dash.startColor = color;
                    dash.endColor = color;
                    dash.startWidth = width;
                    dash.endWidth = width;
                    dash.SetPosition(0, start + (Vector3)(normalized * segmentStart));
                    dash.SetPosition(1, start + (Vector3)(normalized * segmentEnd));
                    nextDashIndex++;
                }

                return nextDashIndex;
            }

            private static float GetGuideLength(Vector2 start, Vector2 direction, float maxLength, float projectileRadius)
            {
                if (maxLength <= 0.01f)
                {
                    return 0f;
                }

                int wallMask = GetWallMask();
                if (wallMask == 0 || direction.sqrMagnitude <= 0.0001f)
                {
                    return maxLength;
                }

                float castRadius = Mathf.Max(0.001f, projectileRadius);
                RaycastHit2D hit = Physics2D.CircleCast(start, castRadius, direction.normalized, maxLength, wallMask);
                return hit.collider != null ? Mathf.Max(0f, hit.distance) : maxLength;
            }

            private static int GetWallMask()
            {
                int wallLayer = LayerMask.NameToLayer(WallLayerName);
                return wallLayer >= 0 ? 1 << wallLayer : 0;
            }

            private LineRenderer EnsureGuideLine(int index)
            {
                if (owner == null)
                {
                    return null;
                }

                while (guideLines.Count <= index)
                {
                    GameObject lineObject = new($"Pattern7GuideLine_{guideLines.Count:00}");
                    lineObject.transform.SetParent(owner.transform, false);
                    LineRenderer line = lineObject.AddComponent<LineRenderer>();
                    line.material = GetMaterial();
                    line.useWorldSpace = true;
                    line.loop = false;
                    line.positionCount = 2;
                    line.numCornerVertices = 0;
                    line.numCapVertices = 1;
                    line.sortingOrder = 17;
                    guideLines.Add(line);
                }

                LineRenderer guideLine = guideLines[index];
                guideLine.material = GetMaterial();
                return guideLine;
            }

            private void HideFrom(int startIndex)
            {
                for (int i = Mathf.Max(0, startIndex); i < guideLines.Count; i++)
                {
                    if (guideLines[i] != null)
                    {
                        guideLines[i].enabled = false;
                    }
                }
            }

            private static Material GetMaterial()
            {
                if (material != null)
                {
                    return material;
                }

                Shader shader = Shader.Find("Sprites/Default");
                material = shader != null ? new Material(shader) : null;
                return material;
            }

            private static Vector2 AngleToDirection(float angleDegrees)
            {
                float radians = angleDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        private sealed class BodyRootSlamController
        {
            private bool isMoved;
            private Vector3 baseLocalPosition;

            public IEnumerator Slam(
                Transform target,
                Transform ownerTransform,
                Pattern4Settings settings,
                System.Func<bool> isExecutionPaused,
                Action stop)
            {
                if (target == null || target == ownerTransform || settings == null)
                {
                    yield break;
                }

                if (!isMoved)
                {
                    baseLocalPosition = target.localPosition;
                    isMoved = true;
                }

                Vector3 upPosition = baseLocalPosition + Vector3.up * settings.SlamUpOffset;
                Vector3 downPosition = baseLocalPosition + Vector3.down * settings.SlamDownOffset;
                yield return Move(target, target.localPosition, upPosition, settings.SlamRiseSeconds, isExecutionPaused, stop);
                yield return Move(target, target.localPosition, downPosition, settings.SlamDropSeconds, isExecutionPaused, stop);
                PlayExplosionIfEnabled(settings.Effects, target.position);
                PlayCameraShakeIfEnabled(settings.Effects, Vector2.down);
            }

            public IEnumerator Recover(
                Transform target,
                Transform ownerTransform,
                Pattern4Settings settings,
                System.Func<bool> isExecutionPaused,
                Action stop)
            {
                if (!isMoved || target == null || target == ownerTransform || settings == null)
                {
                    yield break;
                }

                yield return Move(target, target.localPosition, baseLocalPosition, settings.SlamRecoverSeconds, isExecutionPaused, stop);
                Reset(target);
            }

            public void Reset(Transform target)
            {
                if (!isMoved)
                {
                    return;
                }

                if (target != null)
                {
                    target.localPosition = baseLocalPosition;
                }

                isMoved = false;
            }

            private static IEnumerator Move(Transform target, Vector3 from, Vector3 to, float seconds, System.Func<bool> isExecutionPaused, Action stop)
            {
                float duration = Mathf.Max(0.01f, seconds);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    if (target == null)
                    {
                        yield break;
                    }

                    if (isExecutionPaused?.Invoke() == true)
                    {
                        stop?.Invoke();
                        yield return null;
                        continue;
                    }

                    stop?.Invoke();
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    target.localPosition = Vector3.Lerp(from, to, t);
                    yield return null;
                }

                if (target != null)
                {
                    target.localPosition = to;
                }
            }
        }

    }
}
