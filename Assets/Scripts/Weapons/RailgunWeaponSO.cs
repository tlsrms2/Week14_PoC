using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Weapons
{
    public enum RailgunBeamLengthAxis
    {
        LocalX,
        LocalY
    }

    [System.Serializable]
    public sealed class RailgunVfxSettings
    {
        [Tooltip("한 번에 1~2발을 소비했을 때 생성할 레이저 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject oneToTwoAmmoBeamPrefab;
        [Tooltip("한 번에 3~4발을 소비했을 때 생성할 레이저 이펙트 프리팹입니다. 비워두면 1~2발 프리팹을 사용합니다.")]
        [SerializeField] private GameObject threeToFourAmmoBeamPrefab;
        [Tooltip("한 번에 5발 이상을 소비했을 때 생성할 레이저 이펙트 프리팹입니다. 비워두면 3~4발, 1~2발 순으로 대체합니다.")]
        [SerializeField] private GameObject fiveAmmoBeamPrefab;
        [Tooltip("1~4발 레일건 발사 순간 총구에 생성할 MuzzleFlash 이펙트 프리팹입니다. 비워두면 플레이어 공용 MuzzleFlash를 사용합니다.")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [Tooltip("5발 레일건 발사 순간 총구에 생성할 전용 MuzzleFlash 이펙트 프리팹입니다. 비워두면 1~4발 MuzzleFlash를 사용합니다.")]
        [SerializeField] private GameObject fiveAmmoMuzzleFlashPrefab;
        [Tooltip("프리팹 원본에서 레이저가 뻗어 있는 로컬 축입니다. 1,2bullet.png와 5bullets.png 기반 프리팹은 Local Y입니다.")]
        [SerializeField] private RailgunBeamLengthAxis beamLengthAxis = RailgunBeamLengthAxis.LocalY;
        [Tooltip("총구에서 레이저가 시작되는 위치를 조준 방향으로 미세 조정하는 거리입니다.")]
        [SerializeField] private float muzzleOffset;
        [Tooltip("조준 방향에 더할 이펙트 회전 보정값(도)입니다. 길이축 보정은 자동 적용되므로 보통 0으로 둡니다.")]
        [SerializeField] private float rotationOffsetDegrees;
        [Tooltip("프리팹 애니메이션 재생 배속입니다. 1이면 원본 속도입니다.")]
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [Tooltip("이펙트 프리팹에 포함된 모든 Renderer의 Sorting Order입니다.")]
        [SerializeField] private int sortingOrder = 73;

        public RailgunBeamLengthAxis BeamLengthAxis => beamLengthAxis;
        public float MuzzleOffset => muzzleOffset;
        public float RotationOffsetDegrees => rotationOffsetDegrees;
        public float PlaybackSpeed => playbackSpeed;
        public int SortingOrder => sortingOrder;
        public GameObject ResolveBeamPrefab(int spentAmmo)
        {
            if (spentAmmo >= 5)
            {
                return fiveAmmoBeamPrefab != null
                    ? fiveAmmoBeamPrefab
                    : threeToFourAmmoBeamPrefab != null
                        ? threeToFourAmmoBeamPrefab
                        : oneToTwoAmmoBeamPrefab;
            }

            if (spentAmmo >= 3)
            {
                return threeToFourAmmoBeamPrefab != null
                    ? threeToFourAmmoBeamPrefab
                    : oneToTwoAmmoBeamPrefab;
            }

            return oneToTwoAmmoBeamPrefab;
        }

        public GameObject ResolveMuzzleFlashPrefab(int spentAmmo)
        {
            return spentAmmo >= 5 && fiveAmmoMuzzleFlashPrefab != null
                ? fiveAmmoMuzzleFlashPrefab
                : muzzleFlashPrefab;
        }

        internal void Validate()
        {
            playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
        }
    }

    [CreateAssetMenu(menuName = "Week14/Weapons/Railgun", fileName = "RailgunWeapon")]
    public sealed class RailgunWeaponSO : BaseWeaponSO
    {
        [Tooltip("레이저(관통 투사체)의 이동 속도입니다. 실제 사거리 = 이 값 * Laser Lifetime Seconds.")]
        [SerializeField, Min(0.1f)] private float laserSpeed = 60f;
        [Tooltip("레이저 투사체가 실제로 날아가며 관통 판정을 유지하는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float laserLifetimeSeconds = 0.4f;
        [Tooltip("레이저 빔 시각 연출이 화면에 남아있는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float beamVisualSeconds = 0.12f;
        [Tooltip("레이저 빔의 두께입니다.")]
        [SerializeField, Min(0f)] private float beamWidth = 0.08f;
        [Tooltip("레이저 색상입니다.")]
        [SerializeField] private Color beamColor = new Color(0.5f, 0.9f, 1f, 1f);
        [SerializeField] private RailgunVfxSettings vfxSettings = new RailgunVfxSettings();

        [Header("Audio")]
        [Tooltip("탄환 1~4발을 소비해 발사할 때 재생할 SFX입니다.")]
        [FormerlySerializedAs("fireSfxId")]
        [SerializeField, BossGraphSfxId] private string oneToFourAmmoFireSfxId = string.Empty;
        [Tooltip("탄환 5발을 소비해 발사할 때 재생할 SFX입니다. 비워두면 1~4발용 SFX를 사용합니다.")]
        [SerializeField, BossGraphSfxId] private string fiveAmmoFireSfxId = string.Empty;

        public float LaserSpeed => laserSpeed;
        public float LaserLifetimeSeconds => laserLifetimeSeconds;
        public float BeamVisualSeconds => beamVisualSeconds;
        public float BeamWidth => beamWidth;
        public Color BeamColor => beamColor;
        public RailgunVfxSettings VfxSettings => vfxSettings ??= new RailgunVfxSettings();

        public override void BeginAttack(PlayerShooter shooter)
        {
            int bulletCount = shooter.CurrentBullets;
            if (bulletCount > 0)
            {
                int totalDamage = 0;
                for (int ammo = 1; ammo <= bulletCount; ammo++)
                {
                    totalDamage += GetDamageForAmmo(ammo);
                }

                if (shooter.TrySpendAllBullets())
                {
                    shooter.FireLaser(
                        totalDamage,
                        bulletCount,
                        laserSpeed,
                        laserLifetimeSeconds,
                        beamVisualSeconds,
                        beamWidth,
                        beamColor,
                        VfxSettings,
                        ResolveFireSfxId(bulletCount));
                }
            }

            shooter.EndCharge();
        }

        private string ResolveFireSfxId(int spentAmmo)
        {
            return spentAmmo >= 5 && !string.IsNullOrWhiteSpace(fiveAmmoFireSfxId)
                ? fiveAmmoFireSfxId
                : oneToFourAmmoFireSfxId;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            laserSpeed = Mathf.Max(0.1f, laserSpeed);
            laserLifetimeSeconds = Mathf.Max(0.01f, laserLifetimeSeconds);
            beamVisualSeconds = Mathf.Max(0f, beamVisualSeconds);
            beamWidth = Mathf.Max(0f, beamWidth);
            vfxSettings ??= new RailgunVfxSettings();
            vfxSettings.Validate();
        }
    }
}
