using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Pistol", fileName = "PistolWeapon")]
    public sealed class PistolWeaponSO : BaseWeaponSO
    {
        public override void BeginAttack(PlayerShooter shooter)
        {
            shooter.TryShootEnemy();
            shooter.EndCharge();
        }
    }
}
