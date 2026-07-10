using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Shotgun", fileName = "ShotgunWeapon")]
    public sealed class ShotgunWeaponSO : BaseWeaponSO
    {
        [Tooltip("한 번 발사할 때 나가는 펠릿(탄) 개수입니다.")]
        [SerializeField, Min(1)] private int pelletCount = 5;
        [Tooltip("펠릿들이 퍼지는 전체 각도(도)입니다. 예: 40이면 조준 방향 기준 -20~+20도 사이에 고르게 분포합니다.")]
        [SerializeField, Min(0f)] private float spreadAngle = 30f;

        public int PelletCount => pelletCount;
        public float SpreadAngle => spreadAngle;

        public override void BeginAttack(PlayerShooter shooter)
        {
            if (shooter.CurrentBullets > 0)
            {
                int damage = shooter.CalculateAttackBulletDamage();
                if (shooter.TrySpendOneBullet())
                {
                    shooter.FireSpread(damage, pelletCount, spreadAngle);
                }
            }

            shooter.EndCharge();
        }
    }
}
