using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Sniper", fileName = "SniperWeapon")]
    public sealed class SniperWeaponSO : BaseWeaponSO
    {
        [Tooltip("차징 중 탄환을 한 발씩 소모하는 주기(초)입니다.")]
        [SerializeField, Min(0.01f)] private float bulletConsumeInterval = 0.35f;
        [Tooltip("소모탄 1개당 붙는 데미지 배율입니다. 최종 데미지 = (소모한 탄들의 데미지 합) * (1 + 이 배율 * (소모탄 개수 - 1)). " +
            "즉 1발만 소모했을 때는 배율 보너스가 붙지 않고, 2발째부터 배율이 적용됩니다.")]
        [SerializeField, Min(0f)] private float damageMultiplierPerExtraBullet = 0.5f;

        [Header("Charge Tick Visual")]
        [Tooltip("탄환이 소모될 때마다 Firepoint에 스폰할 스프라이트입니다. 비워두면 이펙트가 생성되지 않습니다.")]
        [SerializeField] private Sprite chargeTickSprite;
        [Tooltip("스폰 시점의 시작 스케일입니다.")]
        [SerializeField, Min(0.01f)] private float chargeTickStartScale = 1f;
        [SerializeField] private int chargeTickSortingOrder = 20;
        [Tooltip("차지를 시작한 뒤 이 시간(초) 이상 눌러야 첫 스프라이트가 스폰됩니다.")]
        [SerializeField, Min(0f)] private float chargeTickFirstSpawnDelaySeconds = 0.15f;
        [Tooltip("스프라이트가 스케일 0에 도달하는 시간 = 소모 주기(bulletConsumeInterval) * 이 비율. 1보다 작으면 실제 탄 소모 시점보다 먼저 사라지고, 1보다 크면 더 늦게(다음 탄 소모 이후까지) 남아있습니다. " +
            "다음 틱의 스프라이트가 스폰될 때 아직 안 사라졌다면 그 즉시 교체됩니다.")]
        [SerializeField, Range(0.05f, 3f)] private float chargeTickShrinkRatio = 0.7f;

        private float ChargeTickLifetimeSeconds => bulletConsumeInterval * chargeTickShrinkRatio;

        public override void BeginAttack(PlayerShooter shooter)
        {
        }

        public override void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
            if (!shooter.HasSpawnedInitialChargeTickEffect && chargeTime >= chargeTickFirstSpawnDelaySeconds)
            {
                shooter.SpawnSniperChargeTickEffect(
                    chargeTickSprite,
                    chargeTickStartScale,
                    ChargeTickLifetimeSeconds,
                    chargeTickSortingOrder);
                shooter.MarkInitialChargeTickEffectSpawned();
            }

            int ticksDue = Mathf.FloorToInt(chargeTime / bulletConsumeInterval);
            while (shooter.ChargeConsumedBulletCount < ticksDue && shooter.CurrentBullets > 0)
            {
                if (shooter.TryConsumeChargeBullet())
                {
                    shooter.PlaySniperChargeSfx();
                    shooter.SpawnSniperChargeTickEffect(
                        chargeTickSprite,
                        chargeTickStartScale,
                        ChargeTickLifetimeSeconds,
                        chargeTickSortingOrder);
                }
            }
        }

        public override void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
            if (shooter.ChargeConsumedBulletCount <= 0) return;

            FireChargedShot(shooter);
        }

        private void FireChargedShot(PlayerShooter shooter)
        {
            int consumedCount = shooter.ChargeConsumedBulletCount;
            int damageSum = shooter.ChargeAccumulatedDamage;
            float multiplier = 1f + damageMultiplierPerExtraBullet * Mathf.Max(0, consumedCount - 1);
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damageSum * multiplier));

            shooter.FireSingle(finalDamage);
        }

        public override void ApplyWeaponTrait(GameObject player)
        {
            player?.GetComponent<PlayerCombatController>()?.SetLockOnSuppressed(true);
        }

        public override void RemoveWeaponTrait(GameObject player)
        {
            player?.GetComponent<PlayerCombatController>()?.SetLockOnSuppressed(false);
        }
    }
}
