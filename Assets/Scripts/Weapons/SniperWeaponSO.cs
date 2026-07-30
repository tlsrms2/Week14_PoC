using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Sniper", fileName = "SniperWeapon")]
    public sealed class SniperWeaponSO : BaseWeaponSO
    {
        [Tooltip("이 시간(초) 이상 홀드하면 차지 공격이 발동합니다. 이보다 일찍 놓으면 일반 사격이 나갑니다.")]
        [SerializeField, Min(0.01f)] private float chargeThresholdSeconds = 1f;
        [Tooltip("차지 공격 시 데미지 배율입니다. 예: 3이면 3배.")]
        [SerializeField, Min(1f)] private float chargeDamageMultiplier = 3f;
        [Header("Charge Laser Visual")]
        [Tooltip("차지를 시작한 뒤 이 시간(초) 이상 눌러야 레이저 연출이 나타납니다(짧게 클릭만 하는 일반 사격에는 안 뜨게 하기 위함).")]
        [SerializeField, Min(0f)] private float laserFirstAppearDelaySeconds = 0.15f;
        [Tooltip("레이저 길이(미터)입니다.")]
        [SerializeField, Min(0.1f)] private float laserLength = 6f;
        [Tooltip("차지가 시작될 때 양옆 레이저가 중앙(조준 방향) 기준으로 벌어져 있는 전체 각도(도)입니다. 차지가 진행될수록 이 각도가 0으로 좁아지며 중앙으로 모입니다.")]
        [SerializeField, Min(0f)] private float laserSpreadAngle = 40f;

        public override void BeginAttack(PlayerShooter shooter)
        {
            if (shooter.CurrentBullets <= 0)
            {
                return;
            }

            shooter.PlaySniperChargeSfx();
        }

        public override void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
            shooter.UpdateSniperChargeProgress(chargeTime, chargeThresholdSeconds);

            if (!shooter.HasShownChargeLaser && chargeTime >= laserFirstAppearDelaySeconds && shooter.CurrentBullets > 0)
            {
                shooter.ShowSniperChargeLaser(laserLength, laserSpreadAngle);
            }
        }

        public override void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
            if (shooter.CurrentBullets <= 0)
            {
                return;
            }

            int baseDamage = shooter.CalculateAttackBulletDamage();
            bool isCharged = chargeTime >= chargeThresholdSeconds;
            int finalDamage = isCharged
                ? Mathf.Max(1, Mathf.RoundToInt(baseDamage * chargeDamageMultiplier))
                : baseDamage;

            if (shooter.TrySpendOneBullet())
            {
                shooter.FireSingle(finalDamage);
            }
        }
    }
}
