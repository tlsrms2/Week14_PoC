using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
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
                    shooter.FireLaser(totalDamage, laserSpeed, laserLifetimeSeconds, beamVisualSeconds, beamWidth, beamColor);
                }
            }

            shooter.EndCharge();
        }
    }
}
