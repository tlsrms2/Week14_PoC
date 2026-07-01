// Legacy: PistolWeaponSO로 대체됨. 기존 에셋 호환성을 위해 보관.
using UnityEngine;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Standard Weapon", fileName = "StandardWeapon")]
    public sealed class StandardWeaponSO : BaseWeaponSO
    {
        public override void BeginAttack(Week14.Combat.PlayerShooter shooter)
        {
            shooter.TryShootEnemy();
        }
    }
}
