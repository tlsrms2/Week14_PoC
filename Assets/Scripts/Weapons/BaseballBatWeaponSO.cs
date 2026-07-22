using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Weapons
{
    [System.Serializable]
    public sealed class BaseballBatVfxSettings
    {
        [Tooltip("차징이 50% 미만일 때 재생할 기본 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject swingVfxPrefab;
        [Tooltip("차징이 50% 이상 100% 미만일 때 재생할 이펙트 프리팹입니다. 비워두면 기본 프리팹을 사용합니다.")]
        [SerializeField] private GameObject halfChargeSwingVfxPrefab;
        [Tooltip("차징이 100%일 때 재생할 이펙트 프리팹입니다. 비워두면 50% 이상 프리팹을 사용합니다.")]
        [SerializeField] private GameObject fullChargeSwingVfxPrefab;
        [Tooltip("오른쪽(+X)으로 휘두를 때의 앵커 기준 로컬 오프셋입니다. 실제 휘두르는 방향에 맞춰 자동 회전됩니다.")]
        [SerializeField] private Vector2 rightFacingLocalOffset;
        [Tooltip("최대 차징일 때 적용할 오른쪽 기준 X 오프셋입니다. 기본 X 오프셋에서 이 값까지 차징 비율로 증가합니다.")]
        [SerializeField] private float maxRightFacingXOffset;
        [Tooltip("조준 방향에 더할 이펙트 로컬 회전 보정값(도)입니다.")]
        [SerializeField] private float rotationOffsetDegrees;
        [Tooltip("기준 공격 범위에서 사용할 이펙트 로컬 스케일입니다.")]
        [SerializeField] private Vector3 localScale = Vector3.one;
        [Tooltip("실제 공격 범위에 비례해 이펙트 스케일을 변경합니다.")]
        [SerializeField] private bool scaleWithAttackRange = true;
        [Tooltip("위 Local Scale이 그대로 적용되는 기준 공격 범위입니다.")]
        [SerializeField, Min(0.01f)] private float referenceAttackRange = 3f;
        [Tooltip("이펙트 프리팹에 포함된 모든 Renderer의 Sorting Order입니다.")]
        [SerializeField] private int sortingOrder = 69;
        [Tooltip("이펙트 애니메이션 재생 배속입니다. 1이면 원본 속도입니다.")]
        [SerializeField, Min(0.01f)] private float playbackSpeed = 1f;
        [Tooltip("이펙트가 시작된 뒤 실제 반사 판정이 발생할 때까지의 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float attackHitDelaySeconds = 0.333f;
        [Tooltip("공격 순간 실제 반원 판정 범위를 표시할 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float rangeIndicatorSeconds = 0.75f;
        [Tooltip("공격 순간 표시되는 실제 반원 판정 범위의 색상입니다.")]
        [SerializeField] private Color rangeIndicatorColor = new Color(1f, 0.55f, 0.1f, 0.6f);
        [Tooltip("공격 버튼을 누르고 있는 동안 표시되는 차징 범위 색상입니다.")]
        [SerializeField] private Color previewRangeColor = new Color(1f, 0.75f, 0.2f, 0.35f);
        [Tooltip("차징하지 않을 때 배트 표시 스프라이트가 플레이어(앵커)로부터 떨어져 있을 거리입니다.")]
        [SerializeField, Min(0f)] private float displayOffsetDistance = 0.8f;
        [Tooltip("차징 시 조준 방향 기준으로 와인드업(Z축 반대 방향) 회전할 최대 각도(도)입니다. " +
            "차징 진행도(0~1)에 비례해 0에서 이 값까지 서서히 회전하고, 공격 시엔 반대 부호(+)까지 스윙합니다.")]
        [SerializeField] private float displayWindUpDegrees = 30f;
        [Tooltip("배트 표시 스프라이트의 아트 방향과 실제 조준 방향을 맞추기 위한 회전 보정값(도)입니다.")]
        [SerializeField] private float displaySpriteRotationOffsetDegrees;
        [Tooltip("배트 표시 스프라이트의 Local Scale입니다. 스프라이트 원본 크기가 게임 월드 크기와 안 맞을 때 조정하세요.")]
        [SerializeField] private Vector3 displayScale = Vector3.one;
        [Tooltip("배트 표시 스프라이트의 Sorting Order입니다.")]
        [SerializeField] private int displaySortingOrder = 69;

        public float DisplayOffsetDistance => displayOffsetDistance;
        public float DisplayWindUpDegrees => displayWindUpDegrees;
        public float DisplaySpriteRotationOffsetDegrees => displaySpriteRotationOffsetDegrees;
        public Vector3 DisplayScale => displayScale;
        public int DisplaySortingOrder => displaySortingOrder;
        public float RotationOffsetDegrees => rotationOffsetDegrees;
        public float PlaybackSpeed => playbackSpeed;
        public float AttackHitDelaySeconds => attackHitDelaySeconds;
        public float RangeIndicatorSeconds => rangeIndicatorSeconds;
        public Color RangeIndicatorColor => rangeIndicatorColor;
        public Color PreviewRangeColor => previewRangeColor;
        public int SortingOrder => sortingOrder;

        public Vector2 GetRightFacingLocalOffset(float charge01)
        {
            float xOffset = Mathf.Lerp(
                rightFacingLocalOffset.x,
                maxRightFacingXOffset,
                Mathf.Clamp01(charge01));
            return new Vector2(xOffset, rightFacingLocalOffset.y);
        }

        public GameObject ResolveSwingVfxPrefab(float charge01)
        {
            float clampedCharge01 = Mathf.Clamp01(charge01);
            if (clampedCharge01 >= 1f && fullChargeSwingVfxPrefab != null)
            {
                return fullChargeSwingVfxPrefab;
            }

            if (clampedCharge01 >= 0.5f && halfChargeSwingVfxPrefab != null)
            {
                return halfChargeSwingVfxPrefab;
            }

            return swingVfxPrefab;
        }

        public Vector3 GetLocalScale(float attackRange)
        {
            return localScale * GetAttackRangeScale(attackRange);
        }

        private float GetAttackRangeScale(float attackRange)
        {
            return scaleWithAttackRange
                ? Mathf.Max(0f, attackRange) / Mathf.Max(0.01f, referenceAttackRange)
                : 1f;
        }

        internal void Validate()
        {
            referenceAttackRange = Mathf.Max(0.01f, referenceAttackRange);
            playbackSpeed = Mathf.Max(0.01f, playbackSpeed);
            attackHitDelaySeconds = Mathf.Max(0f, attackHitDelaySeconds);
            rangeIndicatorSeconds = Mathf.Max(0.01f, rangeIndicatorSeconds);
            displayOffsetDistance = Mathf.Max(0f, displayOffsetDistance);
        }
    }

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
        [SerializeField] private BaseballBatVfxSettings vfxSettings = new BaseballBatVfxSettings();
        [Tooltip("휘두를 때 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string swingSfxId = string.Empty;
        [Tooltip("야구 배트를 장착했을 때 적용할 이동 속도 배율입니다. 1.5 = 50% 증가.")]
        [SerializeField, Min(0f)] private float moveSpeedMultiplier = 1.5f;

        public float MaxAttackRange => maxAttackRange;
        public float ReflectedProjectileSpeed => reflectedProjectileSpeed;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public BaseballBatVfxSettings VfxSettings => vfxSettings ??= new BaseballBatVfxSettings();

        public override void BeginAttack(PlayerShooter shooter)
        {
            if (!shooter.IsBayonetCooldownReady())
            {
                shooter.HideBaseballBatRangePreview();
                shooter.ResetChargeTime();
                return;
            }

            shooter.PreviewBaseballBatRange(GetAttackRange(GetCharge01(0f)), VfxSettings.PreviewRangeColor);
            shooter.UpdateBaseballBatWindUp(GetCharge01(0f), VfxSettings, InGameSprite);
        }

        public override void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
            if (!shooter.IsBayonetCooldownReady())
            {
                shooter.HideBaseballBatRangePreview();
                shooter.ResetChargeTime();
                return;
            }

            shooter.PreviewBaseballBatRange(
                GetAttackRange(GetCharge01(chargeTime)),
                VfxSettings.PreviewRangeColor);
            shooter.UpdateBaseballBatWindUp(GetCharge01(chargeTime), VfxSettings, InGameSprite);
        }

        public override void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
            shooter.HideBaseballBatRangePreview();
            if (shooter.TryConsumeBayonetCooldown(attackCooldownSeconds))
            {
                float charge01 = GetCharge01(chargeTime);
                float attackRange = GetAttackRange(charge01);
                shooter.SwingBaseballBat(
                    reflectedDamage,
                    attackRange,
                    reflectedProjectileSpeed,
                    VfxSettings,
                    charge01,
                    swingSfxId);
                shooter.StartBaseballBatSwingThrough(charge01, VfxSettings, InGameSprite, VfxSettings.AttackHitDelaySeconds);
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

        public float GetCharge01FromRange(float attackRange)
        {
            return Mathf.Approximately(minAttackRange, maxAttackRange)
                ? 1f
                : Mathf.InverseLerp(minAttackRange, maxAttackRange, attackRange);
        }

        private float GetCharge01(float chargeTime)
        {
            return maxChargeSeconds > 0f ? Mathf.Clamp01(chargeTime / maxChargeSeconds) : 1f;
        }

        private float GetAttackRange(float charge01)
        {
            return Mathf.Lerp(minAttackRange, maxAttackRange, charge01);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            maxAttackRange = Mathf.Max(minAttackRange, maxAttackRange);
            maxChargeSeconds = Mathf.Max(0.01f, maxChargeSeconds);
            moveSpeedMultiplier = Mathf.Max(0f, moveSpeedMultiplier);
            vfxSettings ??= new BaseballBatVfxSettings();
            vfxSettings.Validate();
        }
    }
}
