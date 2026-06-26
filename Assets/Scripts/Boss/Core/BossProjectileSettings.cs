using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    [System.Serializable]
    public sealed class BossGraphProjectileEntry
    {
        [SerializeField] private string projectileName = "Default";
        [SerializeField] private BossProjectileSettings projectile = new();

        public string ProjectileName => projectileName?.Trim();
        public BossProjectileSettings Projectile => projectile;
    }

    public sealed class BossGraphProjectileNameAttribute : PropertyAttribute
    {
    }

    public sealed class BossGraphSfxIdAttribute : PropertyAttribute
    {
    }

    public sealed class BossGraphBossChildPathAttribute : PropertyAttribute
    {
    }

    public sealed class BossGraphMinionChildPathAttribute : PropertyAttribute
    {
    }

    public sealed class BossGraphNodeIdAttribute : PropertyAttribute
    {
    }

    [System.Serializable]
    public class BossProjectileSettings
    {
        private const float FixedHomingSeconds = 10f;
        private const float FixedHomingTurnDegreesPerSecond = 540f;

        [SerializeField, Tooltip("발사할 적 탄환 프리팹입니다.")] protected EnemyProjectile prefab;
        [SerializeField, Min(0), Tooltip("플레이어에게 적중했을 때 감소시킬 플레이어 탄환 수입니다.")] protected int bulletDamage = 1;
        [SerializeField, Min(0f), Tooltip("발사 전 충전 시간입니다.")] protected float chargeSeconds = 0.15f;
        [SerializeField, Min(0f), Tooltip("충전 중 탄환이 천천히 움직이는 속도입니다.")] protected float chargeDriftSpeed = 0.25f;
        [SerializeField, Tooltip("충전 중 플레이어 방향을 계속 갱신합니다.")] protected bool aimAtPlayerWhileCharging = true;
        [SerializeField, Tooltip("발사 순간 플레이어 방향으로 다시 조준합니다.")] protected bool aimAtPlayerOnLaunch;
        [SerializeField, Min(0f), Tooltip("탄환 이동 속도입니다.")] protected float speed = 7f;
        [SerializeField, Min(0f), Tooltip("탄환 수명입니다.")] protected float lifetime = 3f;
        [SerializeField, Min(0.01f), Tooltip("탄환 충돌 반지름입니다.")] protected float radius = 0.12f;
        [SerializeField, Tooltip("충전 중 색입니다.")] protected Color chargingColor = new(0.35f, 0.8f, 1f, 1f);
        [SerializeField, FormerlySerializedAs("color"), Tooltip("발사 후 색입니다.")] protected Color launchedColor = new(1f, 0.95f, 0.25f, 1f);
        [SerializeField, Tooltip("유도탄이 대기 중일 때만 사용할 충전 프리팹입니다. 비워두면 기본 프리팹을 사용합니다.")] protected EnemyProjectile homingChargePrefab;
        [SerializeField, Tooltip("유도탄이 대기 중 깜빡일 때 번갈아 표시할 색입니다. 투명색이면 발사 후 색을 사용합니다.")] protected Color homingBlinkColor = Color.clear;
        [SerializeField, Tooltip("탄환 궤적 색입니다. 투명색이면 발사 후 색을 사용합니다.")] protected Color trailColor = Color.clear;
        [SerializeField, Tooltip("탄환 경로 인디케이터와 유도 조준 레티클 색입니다. 투명색이면 발사 후 색을 사용합니다.")] protected Color indicatorColor = Color.clear;
        [SerializeField, Min(0.01f), Tooltip("탄환 궤적 유지 시간입니다.")] protected float trailSeconds = 0.1f;
        [SerializeField, Min(0.1f), Tooltip("탄환 궤적 두께 배율입니다.")] protected float trailWidthMultiplier = 3f;
        [SerializeField] protected bool homingEnabled;

        public BossProjectileSettings()
        {
        }

        protected BossProjectileSettings(
            int bulletDamage,
            float chargeSeconds,
            float chargeDriftSpeed,
            bool aimAtPlayerWhileCharging,
            bool aimAtPlayerOnLaunch,
            float speed,
            float lifetime,
            float radius,
            Color chargingColor,
            Color launchedColor,
            float trailSeconds,
            float trailWidthMultiplier,
            bool homingEnabled,
            EnemyProjectile homingChargePrefab,
            Color homingBlinkColor,
            Color trailColor,
            Color indicatorColor)
        {
            this.bulletDamage = bulletDamage;
            this.chargeSeconds = chargeSeconds;
            this.chargeDriftSpeed = chargeDriftSpeed;
            this.aimAtPlayerWhileCharging = aimAtPlayerWhileCharging;
            this.aimAtPlayerOnLaunch = aimAtPlayerOnLaunch;
            this.speed = speed;
            this.lifetime = lifetime;
            this.radius = radius;
            this.chargingColor = chargingColor;
            this.launchedColor = launchedColor;
            this.trailSeconds = trailSeconds;
            this.trailWidthMultiplier = trailWidthMultiplier;
            this.homingEnabled = homingEnabled;
            this.homingChargePrefab = homingChargePrefab;
            this.homingBlinkColor = homingBlinkColor;
            this.trailColor = trailColor;
            this.indicatorColor = indicatorColor;
        }

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
        public EnemyProjectile HomingChargePrefab => homingChargePrefab;
        public Color HomingBlinkColor => homingBlinkColor;
        public Color TrailColor => trailColor;
        public Color IndicatorColor => indicatorColor;
        public bool HasHomingBlinkColor => HasCustomColor(homingBlinkColor);
        public bool HasTrailColor => HasCustomColor(trailColor);
        public bool HasIndicatorColor => HasCustomColor(indicatorColor);
        public float TrailSeconds => trailSeconds;
        public float TrailWidthMultiplier => trailWidthMultiplier;
        public bool HomingEnabled => homingEnabled;
        public float HomingSeconds => FixedHomingSeconds;
        public float HomingTurnDegreesPerSecond => FixedHomingTurnDegreesPerSecond;

        private static bool HasCustomColor(Color color)
        {
            return color != Color.clear;
        }
    }
}
