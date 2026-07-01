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

        public override void BeginAttack(PlayerShooter shooter)
        {
        }

        public override void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
            int ticksDue = Mathf.FloorToInt(chargeTime / bulletConsumeInterval);
            while (shooter.ChargeConsumedBulletCount < ticksDue)
            {
                if (!shooter.TryConsumeChargeBullet())
                {
                    if (shooter.ChargeConsumedBulletCount > 0)
                    {
                        FireChargedShot(shooter);
                    }
                    shooter.EndChargeAfterAutoFire();
                    return;
                }
            }
        }

        public override void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
            if (shooter.ChargeConsumedBulletCount <= 0 && !shooter.TryConsumeChargeBullet()) return;

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
