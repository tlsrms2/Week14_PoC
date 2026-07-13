using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Weapons
{
    [CreateAssetMenu(menuName = "Week14/Weapons/Baseball Bat", fileName = "BaseballBatWeapon")]
    public sealed class BaseballBatWeaponSO : BaseWeaponSO
    {
        [Tooltip("공격속도: 한 번 휘두른 뒤 다음 공격이 가능해지기까지의 대기시간(초)입니다.")]
        [SerializeField, Min(0f)] private float attackCooldownSeconds = 0.5f;
        [Tooltip("차징하지 않았을 때의 최소 반사 범위입니다.")]
        [SerializeField, Min(0f)] private float minAttackRange = 1.2f;
        [Tooltip("최대 차징했을 때의 최대 반사 범위입니다.")]
        [SerializeField, Min(0f)] private float maxAttackRange = 3f;
        [Tooltip("최소 범위에서 최대 범위까지 커지는 데 걸리는 차징 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float maxChargeSeconds = 2f;
        [Tooltip("반사된 적탄이 적에게 줄 피해량입니다.")]
        [SerializeField, Min(0)] private int reflectedDamage = 3;
        [Tooltip("반사된 적탄의 고정 이동 속도입니다. 반사 전 탄막 속도와 무관하게 이 값으로 덮어씁니다.")]
        [SerializeField, Min(0.01f)] private float reflectedProjectileSpeed = 8f;
        [Tooltip("휘두르는 순간 반원 공격범위를 짧게 보여주는 플래시 색상입니다.")]
        [SerializeField] private Color rangeFlashColor = new Color(1f, 0.55f, 0.1f, 0.6f);
        [Tooltip("공격 버튼을 누르고 있는 동안 보여줄 차징 범위 미리보기 색상입니다.")]
        [SerializeField] private Color previewRangeColor = new Color(1f, 0.75f, 0.2f, 0.35f);
        [Tooltip("범위 플래시가 사라지는 데 걸리는 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float rangeFlashSeconds = 0.1f;
        [Tooltip("휘두를 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string swingSfxId = string.Empty;
        [Tooltip("야구 배트를 장착했을 때 적용할 이동 속도 배율입니다. 1.5 = 50% 증가.")]
        [SerializeField, Min(0f)] private float moveSpeedMultiplier = 1.5f;

        public float MaxAttackRange => maxAttackRange;
        public Color RangeFlashColor => rangeFlashColor;
        public float RangeFlashSeconds => rangeFlashSeconds;
        public float ReflectedProjectileSpeed => reflectedProjectileSpeed;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        public override void BeginAttack(PlayerShooter shooter)
        {
            if (!shooter.IsBayonetCooldownReady())
            {
                shooter.HideBaseballBatRangePreview();
                shooter.ResetChargeTime();
                return;
            }

            shooter.PreviewBaseballBatRange(GetAttackRange(0f), previewRangeColor);
        }

        public override void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
            if (!shooter.IsBayonetCooldownReady())
            {
                shooter.HideBaseballBatRangePreview();
                shooter.ResetChargeTime();
                return;
            }

            shooter.PreviewBaseballBatRange(GetAttackRange(chargeTime), previewRangeColor);
        }

        public override void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
            shooter.HideBaseballBatRangePreview();
            if (shooter.TryConsumeBayonetCooldown(attackCooldownSeconds))
            {
                float attackRange = GetAttackRange(chargeTime);
                shooter.SwingBaseballBat(
                    reflectedDamage,
                    attackRange,
                    reflectedProjectileSpeed,
                    rangeFlashColor,
                    rangeFlashSeconds,
                    swingSfxId);
            }
        }

        public override void ApplyWeaponTrait(GameObject player)
        {
            player?.GetComponent<PlayerCombatController>()?.SetWeaponMoveSpeedMultiplier(moveSpeedMultiplier);
        }

        public override void RemoveWeaponTrait(GameObject player)
        {
            player?.GetComponent<PlayerCombatController>()?.SetWeaponMoveSpeedMultiplier(1f);
        }

        private float GetAttackRange(float chargeTime)
        {
            float charge01 = maxChargeSeconds > 0f ? Mathf.Clamp01(chargeTime / maxChargeSeconds) : 1f;
            return Mathf.Lerp(minAttackRange, maxAttackRange, charge01);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            maxAttackRange = Mathf.Max(minAttackRange, maxAttackRange);
            maxChargeSeconds = Mathf.Max(0.01f, maxChargeSeconds);
            moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
        }
    }
}
