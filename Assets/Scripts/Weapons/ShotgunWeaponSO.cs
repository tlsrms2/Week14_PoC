using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Shotgun", fileName = "ShotgunWeapon")]
    public sealed class ShotgunWeaponSO : BaseWeaponSO
    {
        [Tooltip("인접한 탄 사이의 고정 각도 간격(도)입니다. 예: 10이면 탄 3발 시 -10°, 0°, +10° 배치.")]
        [FormerlySerializedAs("spreadAngle")]
        [SerializeField, Min(0f)] private float pelletStep = 10f;
        [Tooltip("탄환이 사라지는 최대 사거리입니다.")]
        [SerializeField, Min(0.1f)] private float maxRange = 5f;

        public float PelletStep => pelletStep;
        public float MaxRange => maxRange;

        public override void BeginAttack(PlayerShooter shooter)
        {
            int pelletCount = shooter.CurrentBullets;
            if (pelletCount > 0)
            {
                int[] damages = new int[pelletCount];
                for (int i = 0; i < pelletCount; i++)
                {
                    damages[i] = GetDamageForAmmo(pelletCount - i);
                }

                if (shooter.TrySpendAllBullets())
                {
                    shooter.FireSpread(damages, pelletStep, maxRange);
                }
            }

            shooter.EndCharge();
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
