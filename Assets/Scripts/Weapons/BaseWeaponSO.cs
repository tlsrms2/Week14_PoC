using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Week14.Combat;
using Week14.Skills;

namespace Week14.Weapons
{
    public abstract class BaseWeaponSO : ScriptableObject
    {
        [Tooltip("총기 고유 식별자입니다. 저장 데이터와 WeaponDatabase에서 이 값으로 총기를 찾습니다.")]
        [SerializeField] private string weaponId;
        [Tooltip("UI에 표시할 총기 이름입니다.")]
        [SerializeField] private string displayName;
        [SerializeField] private LocalizedString localizedDisplayName;
        [Tooltip("UI에 표시할 총기 아이콘입니다.")]
        [SerializeField] private Sprite icon;
        [Tooltip("UI에 표시할 총기 아이콘의 아웃라인(테두리) 스프라이트입니다.")]
        [SerializeField] private Sprite outlineIcon;
        [Tooltip("UI에 표시할 총기 설명입니다.")]
        [SerializeField, TextArea] private string description;
        [SerializeField] private LocalizedString localizedDescription;
        [Tooltip("인게임에서 보여줄 총기 비주얼입니다. (선택, 이번 패스에는 아무 코드도 읽지 않는 자리만 잡아둔 필드)")]
        [SerializeField] private Sprite inGameSprite;
        [Tooltip("이 총기로 발사할 투사체 프리팹입니다. 비워두면 PlayerCombatConfig의 기본 투사체를 사용합니다.")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [Tooltip("이 총기를 장착했을 때 플레이어 왼팔 애니메이터에 적용할 컨트롤러입니다. 비워두면 기본 컨트롤러를 유지합니다.")]
        [SerializeField] private RuntimeAnimatorController leftArmController;
        [Tooltip("이 총기가 보유할 수 있는 최대 탄환 수입니다. 장착 시 BulletGauge가 이 값으로 재설정됩니다.")]
        [SerializeField, Min(1)] private int maxAmmo = 5;
        [Tooltip("패링 판정 범위 배수입니다. 1 = 기본값과 동일. 마우스 패링 리티클 스케일에 곱해지는 멀티플라이어로 적용됩니다(절대 거리 값이 아님).")]
        [SerializeField, Min(0.01f)] private float parryingRange = 1f;
        [Tooltip("남은 탄환 수에 따른 공격 데미지입니다. 인덱스 0은 탄환 1개 남았을 때, 마지막 인덱스는 탄환이 가장 많을 때입니다. " +
            "배열 길이는 항상 Max Ammo와 같게 자동으로 맞춰집니다. int[]를 쓰는 이유: BossAI.ReceivePlayerHit/PlayerProjectile이 전부 int 데미지를 쓰기 때문입니다.")]
        [SerializeField] private int[] damagePerAmmoStep = { 5, 2, 2, 1, 1 };
        [SerializeField] private string maxAmmoTooltipTextFormat;
        [SerializeField] private LocalizedString localizedMaxAmmoTooltipText;
        [SerializeField] private string parryingRangeTooltipTextFormat;
        [SerializeField] private LocalizedString localizedParryingRangeTooltipText;
        [SerializeField] private string bulletDamageTooltipTextFormat;
        [SerializeField] private LocalizedString localizedBulletDamageTooltipText;
        [Tooltip("이 총기를 장착했을 때 자동으로 장착되는 스킬입니다. 슬롯 순서대로 SkillSlot에 할당됩니다.")]
        [SerializeField] private BaseSkillSO[] skills;

        public string WeaponId => weaponId;
        public string DisplayName => displayName;
        public LocalizedString LocalizedDisplayName => localizedDisplayName;
        public Sprite Icon => icon;
        public Sprite OutlineIcon => outlineIcon;
        public string Description => description;
        public LocalizedString LocalizedDescription => localizedDescription;
        public bool HasLocalizedDisplayName => HasLocalizedString(localizedDisplayName);
        public bool HasLocalizedDescription => HasLocalizedString(localizedDescription);
        public Sprite InGameSprite => inGameSprite;
        public PlayerProjectile ProjectilePrefab => projectilePrefab;
        public RuntimeAnimatorController LeftArmController => leftArmController;
        public int MaxAmmo => maxAmmo;
        public float ParryingRange => parryingRange;
        public int[] DamagePerAmmoStep => damagePerAmmoStep;
        public string MaxAmmoTooltipText => FormatTooltipText(maxAmmoTooltipTextFormat, maxAmmo);
        public LocalizedString LocalizedMaxAmmoTooltipText => localizedMaxAmmoTooltipText;
        public bool HasLocalizedMaxAmmoTooltipText => HasLocalizedString(localizedMaxAmmoTooltipText);
        public object[] MaxAmmoTooltipArguments => new object[] { maxAmmo };
        public string ParryingRangeTooltipText => FormatTooltipText(parryingRangeTooltipTextFormat, parryingRange);
        public LocalizedString LocalizedParryingRangeTooltipText => localizedParryingRangeTooltipText;
        public bool HasLocalizedParryingRangeTooltipText => HasLocalizedString(localizedParryingRangeTooltipText);
        public object[] ParryingRangeTooltipArguments => new object[] { parryingRange };
        public string BulletDamageSequenceText => BuildDamageSequenceText();
        public string BulletDamageTooltipText => FormatTooltipText(bulletDamageTooltipTextFormat, BulletDamageSequenceText);
        public LocalizedString LocalizedBulletDamageTooltipText => localizedBulletDamageTooltipText;
        public bool HasLocalizedBulletDamageTooltipText => HasLocalizedString(localizedBulletDamageTooltipText);
        public object[] BulletDamageTooltipArguments => new object[] { BulletDamageSequenceText };
        public BaseSkillSO[] Skills => skills;

        private static string FormatTooltipText(string format, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            try
            {
                return string.Format(format, arguments);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }

        public int GetDamageForAmmo(int remainingAmmo)
        {
            if (damagePerAmmoStep == null || damagePerAmmoStep.Length == 0)
            {
                return 1;
            }

            int index = Mathf.Clamp(remainingAmmo - 1, 0, damagePerAmmoStep.Length - 1);
            return Mathf.Max(0, damagePerAmmoStep[index]);
        }

        private string BuildDamageSequenceText()
        {
            if (damagePerAmmoStep == null || damagePerAmmoStep.Length == 0)
            {
                return string.Empty;
            }

            string[] values = new string[damagePerAmmoStep.Length];
            for (int i = 0; i < damagePerAmmoStep.Length; i++)
            {
                values[i] = damagePerAmmoStep[damagePerAmmoStep.Length - 1 - i].ToString();
            }

            return string.Join("-", values);
        }

        public abstract void BeginAttack(PlayerShooter shooter);

        public virtual void HoldAttack(PlayerShooter shooter, float chargeTime)
        {
        }

        public virtual void ReleaseAttack(PlayerShooter shooter, float chargeTime)
        {
        }

        public virtual void ApplyWeaponTrait(GameObject player)
        {
        }

        public virtual void RemoveWeaponTrait(GameObject player)
        {
        }

        protected virtual void OnValidate()
        {
            maxAmmo = Mathf.Max(1, maxAmmo);
            ResizeDamagePerAmmoStep();
        }

        private void ResizeDamagePerAmmoStep()
        {
            if (damagePerAmmoStep != null && damagePerAmmoStep.Length == maxAmmo)
            {
                return;
            }

            int oldLength = damagePerAmmoStep?.Length ?? 0;
            int fillValue = oldLength > 0 ? damagePerAmmoStep[oldLength - 1] : 1;
            int[] resized = new int[maxAmmo];
            for (int i = 0; i < maxAmmo; i++)
            {
                resized[i] = i < oldLength ? damagePerAmmoStep[i] : fillValue;
            }

            damagePerAmmoStep = resized;
        }
    }
}
