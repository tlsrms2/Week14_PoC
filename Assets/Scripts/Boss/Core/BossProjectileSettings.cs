using UnityEngine;
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

    public sealed class BossGraphBgmIdAttribute : PropertyAttribute
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
        [SerializeField, Tooltip("발사할 EnemyProjectile 프리팹입니다.")] protected EnemyProjectile prefab;
        [SerializeField, Min(0), Tooltip("플레이어에게 적중했을 때 감소시킬 탄환 수입니다.")] protected int bulletDamage = 1;
        [SerializeField, Min(0f), Tooltip("발사 전 충전 시간입니다.")] protected float chargeSeconds = 0.15f;
        [SerializeField, Min(0f), Tooltip("충전 중 투사체가 천천히 전진하는 속도입니다.")] protected float chargeDriftSpeed = 0.25f;
        [SerializeField, Tooltip("충전 중 플레이어 방향을 계속 갱신합니다.")] protected bool aimAtPlayerWhileCharging = true;
        [SerializeField, Tooltip("발사 순간 플레이어 방향으로 다시 조준합니다.")] protected bool aimAtPlayerOnLaunch;
        [SerializeField, Min(0f), Tooltip("투사체 이동 속도입니다.")] protected float speed = 7f;
        [SerializeField, Min(0f), Tooltip("투사체 수명입니다.")] protected float lifetime = 3f;
        [SerializeField, Min(0.01f), Tooltip("투사체 충돌 반지름입니다.")] protected float radius = 0.12f;
        [SerializeField, Min(0.01f), Tooltip("투사체 궤적 유지 시간입니다.")] protected float trailSeconds = 0.1f;
        [SerializeField, Min(0.1f), Tooltip("투사체 궤적 두께 배율입니다.")] protected float trailWidthMultiplier = 3f;

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
            float trailSeconds,
            float trailWidthMultiplier)
        {
            this.bulletDamage = bulletDamage;
            this.chargeSeconds = chargeSeconds;
            this.chargeDriftSpeed = chargeDriftSpeed;
            this.aimAtPlayerWhileCharging = aimAtPlayerWhileCharging;
            this.aimAtPlayerOnLaunch = aimAtPlayerOnLaunch;
            this.speed = speed;
            this.lifetime = lifetime;
            this.radius = radius;
            this.trailSeconds = trailSeconds;
            this.trailWidthMultiplier = trailWidthMultiplier;
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
        public float TrailSeconds => trailSeconds;
        public float TrailWidthMultiplier => trailWidthMultiplier;
    }
}
