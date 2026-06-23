using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        private const int MinimumProjectileCount = 2;

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

        private void EnsureProjectiles()
        {
            projectiles ??= new List<ProjectileSettings>();
            while (projectiles.Count < MinimumProjectileCount)
            {
                projectiles.Add(new ProjectileSettings());
            }

            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i] ??= new ProjectileSettings();
            }
        }

        private ProjectileSettings GetProjectile(int projectileIndex)
        {
            EnsureProjectiles();
            if (projectiles.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(projectileIndex, 0, projectiles.Count - 1);
            return projectiles[index];
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
            [SerializeField, Min(0), Tooltip("패턴1에서 사용할 공용 투사체 인덱스입니다.")] private int projectileIndex;
            [SerializeField, Min(4), Tooltip("한 번의 사방 발사에서 생성할 탄환 수입니다.")] private int radialBulletCount = 4;
            [SerializeField, Min(0.01f), Tooltip("사방 탄환을 반복 발사하는 간격입니다.")] private float burstInterval = 0.65f;
            [SerializeField, Min(0f), Tooltip("보스 중심에서 패턴1 탄환을 원형으로 배치할 거리입니다.")] private float spawnRadius = 0.85f;
            [SerializeField, Tooltip("탄환을 발사할 때마다 회전시킬 각도입니다.")] private float angleStepDegrees = 25f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴1 원점 폭발/연기 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.OriginBurst(1f, 18, 1f, 12);

            public int ProjectileIndex => projectileIndex;
            public AlternatingProjectileOrigins ProjectileOrigins => projectileOrigins;
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

            [SerializeField, Min(0), Tooltip("패턴2에서 사용할 공용 투사체 인덱스입니다.")] private int projectileIndex = 1;
            [SerializeField, Min(0f), Tooltip("패턴2 중 느려진 이동 속도 배율입니다.")] private float moveSpeedMultiplier = 0.25f;
            [SerializeField, Min(1), Tooltip("머신건처럼 연속 발사할 탄환 수입니다.")] private int bulletCount = 14;
            [SerializeField, Min(0.01f), Tooltip("머신건 탄환 발사 간격입니다.")] private float fireInterval = 0.12f;
            [SerializeField, Min(0f), Tooltip("연속 탄환들이 겹치지 않도록 옆으로 벌리는 거리입니다.")] private float spawnSpacing = 0.18f;

            [SerializeField, Tooltip("발사 묶음 목록입니다. 각 묶음은 n발을 m초 간격으로 쏜 뒤 l초 쉽니다.")]
            private List<VolleySettings> volleys = new() { new VolleySettings() };
            [Header("Effects")]
            [SerializeField, Tooltip("패턴2 원점 폭발/연기 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.OriginBurst(0.9f, 14, 0.9f, 10);

            public int ProjectileIndex => projectileIndex;
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

            [SerializeField, Min(0), Tooltip("패턴3에서 사용할 공용 투사체 인덱스입니다.")] private int projectileIndex = 1;
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
            
            public int ProjectileIndex => projectileIndex;
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

            [SerializeField, Min(0), Tooltip("이 패턴에서 사용할 공용 투사체 인덱스입니다.")] private int projectileIndex = 1;
            [SerializeField, Min(1), Tooltip("한 웨이브에서 360도로 발사할 탄환 수입니다.")] private int bulletCount = 32;
            [SerializeField, Min(1), Tooltip("전방위 원형 파동 발사를 몇 번 반복할지 정합니다.")] private int waveCount = 3;
            [SerializeField, Min(0f), Tooltip("전방위 웨이브 사이의 대기 시간입니다.")] private float waveInterval = 0.2f;
            [SerializeField, Tooltip("첫 탄환의 시작 각도 오프셋입니다.")] private float startAngleOffset;
            [SerializeField, Min(0f), Tooltip("보스 중심에서 전방위 탄환을 원형으로 배치할 거리입니다.")] private float spawnRadius = 0.85f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴4 내려찍기 폭발/진동 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.Slam();

            public int ProjectileIndex => projectileIndex;
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

            [SerializeField, Min(0), Tooltip("패턴5에서 사용할 공용 투사체 인덱스입니다.")] private int projectileIndex;
            [SerializeField, Min(0f), Tooltip("제자리에서 기를 모으는 시간입니다.")] private float windupSeconds = 1.4f;
            [SerializeField, Min(1), Tooltip("기 모으기가 끝난 뒤 발사할 탄환 수입니다.")] private int bulletCount = 36;
            [SerializeField, Min(0.01f), Tooltip("미니건 탄환 발사 간격입니다.")] private float fireInterval = 0.045f;
            [SerializeField, Min(0f), Tooltip("연속 탄환들이 겹치지 않도록 옆으로 벌리는 거리입니다.")] private float spawnSpacing = 0.12f;
            [SerializeField, Min(0f), Tooltip("매 탄환마다 회전할 각도입니다.")] private float sweepStepDegrees = 5f;
            [SerializeField, Min(0f), Tooltip("중심 방향(플레이어)을 기준으로 좌우로 꺾이는 최대 부채꼴 각도입니다.")] private float maxSweepAngle = 35f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴5 준비 연기/발사 총구화염 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.WindupMuzzle();
            [SerializeField, HideInInspector] private bool pattern5BulletShakeDefaultApplied;

            public int ProjectileIndex => projectileIndex;
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
            [SerializeField, Min(0), Tooltip("패턴7 세 갈래 발사에 사용할 공용 투사체 인덱스입니다.")] private int normalProjectileIndex;
            [SerializeField, Min(0), Tooltip("패턴7 보조 발사에 사용할 공용 투사체 인덱스입니다.")] private int secondaryProjectileIndex = 1;
            [SerializeField, Tooltip("패턴7 보조 투사체별 소환 위치 목록입니다. 발사 개수보다 부족한 요소는 FirePoint의 Projectile Origin을 사용합니다.")]
            private List<Transform> secondaryProjectileOrigins = new();
            [SerializeField, Min(0f), Tooltip("발사 직전 플레이어를 조준하며 대기하는 시간입니다.")] private float windupSeconds = 1.0f;
            [SerializeField, Min(1), Tooltip("일반 탄환 3갈래 묶음을 몇 번 발사할지 정합니다.")] private int normalVolleyCount = 3;
            [SerializeField, Min(0f), Tooltip("일반 탄환 3갈래 묶음 사이의 발사 간격입니다. 0이면 한 번에 모두 발사합니다.")] private float normalVolleyInterval = 0.18f;
            [SerializeField, Min(0), Tooltip("첫 발사 묶음과 함께 소환할 보조 투사체 수입니다.")] private int secondaryBulletCount = 1;
            [SerializeField, Range(0f, 180f), Tooltip("전방 부채꼴 전체 각도입니다. 3갈래 탄환은 -절반, 중앙, +절반 방향으로 발사됩니다.")] private float fanAngleDegrees = 42f;
            [SerializeField, Min(0f), Tooltip("세 갈래 일반 탄환이 시작 위치에서 옆으로 벌어지는 거리입니다.")] private float normalSpawnSpacing = 0.16f;
            [SerializeField, Min(0f), Tooltip("보조 투사체를 발사 방향 앞쪽으로 소환할 거리입니다.")] private float secondarySpawnForwardOffset = 0f;
            [Header("Effects")]
            [SerializeField, Tooltip("패턴7 준비 연기/발사 총구화염 설정입니다.")] private PatternEffectSettings effects = PatternEffectSettings.WindupMuzzle();

            public int NormalProjectileIndex => normalProjectileIndex;
            public int SecondaryProjectileIndex => secondaryProjectileIndex;
            public FirePoint FirePoint => firePoint;
            public IReadOnlyList<Transform> SecondaryProjectileOrigins => secondaryProjectileOrigins;
            public float WindupSeconds => windupSeconds;
            public int NormalVolleyCount => normalVolleyCount;
            public float NormalVolleyInterval => normalVolleyInterval;
            public int SecondaryBulletCount => secondaryBulletCount;
            public float FanAngleDegrees => fanAngleDegrees;
            public float NormalSpawnSpacing => normalSpawnSpacing;
            public float SecondarySpawnForwardOffset => secondarySpawnForwardOffset;
            public PatternEffectSettings Effects => effects;
        }
    }
}
