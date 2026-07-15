using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    // 순수하게 "패링당하기 위한" 탄이다. 플레이어에게는 데미지를 주지 않고(ConfigurePlayerCollisionIgnored),
    // 자신의 수명(지속시간) 동안 패링당하지 않으면 아무 효과 없이 그냥 사라진다.
    // 패링(Intercepted)당하면 그 자리에 보상용 탄을 원형으로 균등하게 뿌린다.
    // 소환(스폰)은 FireParrySuppressionBaitAction 같은 그래프 액션이 담당하고, "패링당하면 무슨 일이
    // 일어나는지"(보상 탄 몇 발을 어떤 반경/지속시간으로 뿌릴지)는 전부 이 탄 자신이 들고 있다.
    [AddComponentMenu("Week14/Combat/Parry Bait Reward Projectile")]
    public sealed class ParryBaitRewardProjectile : EnemyProjectile
    {
        [Header("보상 폭발")]
        [Tooltip("패링 성공 시 사방으로 뿌릴 보상 탄의 프리팹/설정입니다.")]
        [SerializeField] private BossProjectileSettings rewardProjectile = new();
        [Tooltip("패링 성공 시 뿌릴 보상 탄 개수입니다(기본값 — 스폰한 액션이 다른 값으로 덮어쓸 수 있습니다).")]
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [Tooltip("보상 탄이 배치될 원의 반지름입니다(기본값 — 스폰한 액션이 다른 값으로 덮어쓸 수 있습니다).")]
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [Tooltip("보상 탄의 지속시간입니다(기본값 — 스폰한 액션이 다른 값으로 덮어쓸 수 있습니다).")]
        [SerializeField, Min(0.01f)] private float rewardLifetime = 2f;

        private int? rewardCountOverride;
        private float? rewardRadiusOverride;
        private float? rewardLifetimeOverride;

        protected override void OnProjectileInitialized()
        {
            // 이 탄은 순수 패링 타겟이라 플레이어와 부딪혀도 데미지를 주지 않는다.
            ConfigurePlayerCollisionIgnored(true);
        }

        // 이 탄 자신이 패링 가능한 시간(=사라지기까지 남은 수명)을 스폰 직후에 지정한다.
        internal void ConfigureBaitDuration(float lifetimeSeconds)
        {
            OverrideProjectileLifetime(lifetimeSeconds);
        }

        internal void ConfigureRewardOverrides(int? countOverride, float? radiusOverride, float? lifetimeOverride)
        {
            rewardCountOverride = countOverride;
            rewardRadiusOverride = radiusOverride;
            rewardLifetimeOverride = lifetimeOverride;
        }

        protected override void OnProjectileDestroying(EnemyProjectileDestroyReason reason, Vector3 position)
        {
            if (reason != EnemyProjectileDestroyReason.Intercepted)
            {
                return;
            }

            FireRewardCircle(position);
        }

        private void FireRewardCircle(Vector3 center)
        {
            EnemyProjectile prefab = rewardProjectile?.Prefab;
            if (prefab == null)
            {
                return;
            }

            int count = Mathf.Max(1, rewardCountOverride ?? rewardBulletCount);
            float radius = Mathf.Max(0.01f, rewardRadiusOverride ?? rewardCircleRadius);
            float lifetime = Mathf.Max(0.01f, rewardLifetimeOverride ?? rewardLifetime);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(step * i);
                Vector3 spawnPosition = center + (Vector3)(direction * radius);

                EnemyProjectile reward = Spawn(
                    prefab,
                    OwnerBullets,
                    spawnPosition,
                    direction,
                    rewardProjectile.BulletDamage,
                    0f,
                    rewardProjectile.Speed,
                    lifetime,
                    rewardProjectile.Radius,
                    ProjectileColor,
                    rewardProjectile.TrailSeconds,
                    rewardProjectile.TrailWidthMultiplier,
                    false,
                    0f,
                    0f);

                // 보상 탄은 패링은 가능해야 하지만, 플레이어와 닿아도 접촉 데미지는 주지 않는다.
                // (ConfigurePlayerCollisionIgnored는 패링 가능 여부와는 무관하다.)
                reward?.ConfigurePlayerCollisionIgnored(true);
            }
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
