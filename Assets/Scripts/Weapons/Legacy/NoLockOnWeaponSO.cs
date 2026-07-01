// Legacy: ShotgunWeaponSO / SniperWeaponSO로 대체됨. 기존 에셋 호환성을 위해 보관.
using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/No LockOn Weapon", fileName = "NoLockOnWeapon")]
    public sealed class NoLockOnWeaponSO : BaseWeaponSO
    {
        public override void BeginAttack(PlayerShooter shooter)
        {
            shooter.TryShootEnemy();
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
