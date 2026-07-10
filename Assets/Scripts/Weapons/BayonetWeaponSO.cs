using UnityEngine;
using Week14.Combat;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Bayonet", fileName = "BayonetWeapon")]
    public sealed class BayonetWeaponSO : BaseWeaponSO
    {
        [Tooltip("공격속도: 한 번 휘두른 뒤 다음 공격이 가능해지기까지의 대기시간(초)입니다.")]
        [SerializeField, Min(0f)] private float attackCooldownSeconds = 0.5f;
        [Tooltip("공격범위: 반원 판정의 반지름입니다. 조준 방향(락온 중이면 보스 방향) 기준 앞쪽 반원 안의 적탄을 제거하고 적에게 피해를 줍니다.")]
        [SerializeField, Min(0f)] private float attackRange = 2f;
        [Tooltip("적에게 줄 피해량입니다.")]
        [SerializeField, Min(0)] private int slashDamage = 2;
        [Tooltip("휘두르는 순간 반원 공격범위를 짧게 보여주는 플래시 색상입니다.")]
        [SerializeField] private Color rangeFlashColor = new Color(1f, 1f, 1f, 0.6f);
        [Tooltip("범위 플래시가 사라지는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float rangeFlashSeconds = 0.15f;

        public override void BeginAttack(PlayerShooter shooter)
        {
            if (shooter.TryConsumeBayonetCooldown(attackCooldownSeconds))
            {
                shooter.SwingBayonet(slashDamage, attackRange, rangeFlashColor, rangeFlashSeconds);
            }

            shooter.EndCharge();
        }
    }
}
