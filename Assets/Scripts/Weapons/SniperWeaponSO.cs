using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Sniper", fileName = "SniperWeapon")]
    public sealed class SniperWeaponSO : BaseWeaponSO
    {
        [Tooltip("최대 차지 시간(초)입니다. 이 시간을 채우면 최대 대미지가 됩니다.")]
        [SerializeField, Min(0.01f)] private float maxChargeTime = 2f;
        [Tooltip("차지 0%(즉시 발사)일 때 기본 대미지에 곱해지는 배율입니다.")]
        [SerializeField, Min(0f)] private float minDamageMultiplier = 0.5f;
        [Tooltip("차지 100%일 때 기본 대미지에 곱해지는 배율입니다.")]
        [SerializeField, Min(0f)] private float maxDamageMultiplier = 3f;

        public override void BeginAttack(PlayerShooter shooter)
        {
        }

        public override void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
            shooter.UpdateChargeVisual(chargeTime / maxChargeTime);
        }

        public override void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
            if (shooter.CurrentBullets <= 0) return;

            float ratio = Mathf.Clamp01(chargeTime / maxChargeTime);
            int baseDamage = shooter.CalculateAttackBulletDamage();
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, ratio)));

            if (!shooter.TrySpendOneBullet()) return;

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
