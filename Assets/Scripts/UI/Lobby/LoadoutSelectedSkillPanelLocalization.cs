using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Week14.Skills;
using Week14.Weapons;

namespace Week14.UI
{
    // LoadoutSelectedSkillPanel이 쓰는 로컬라이징 문구를 전담하는 컴포넌트입니다.
    // 카테고리명/좌우클릭 힌트 같은 고정 문구는 한 번만 구독해서 캐시해두고,
    // 쿨타임/포인트/가격/환불액처럼 숫자가 들어가는 문구는 표시할 때마다 새로 계산합니다.
    // 또한 무기/스킬/패시브 스킬 에셋들의 LocalizedString(이름/설명 등)을 미리 로드해둬서,
    // 처음 그 항목을 호버하거나 언어를 바꾼 직후에도 텍스트가 딜레이 없이 뜨게 합니다.
    public sealed class LoadoutSelectedSkillPanelLocalization : MonoBehaviour
    {
        [Tooltip("여기 등록된 무기들의 이름/설명/패링범위 등 로컬라이징 문구를 미리 로드해둡니다. 비워두면 무기 쪽 예열은 건너뜁니다.")]
        [SerializeField] private WeaponDatabase weaponDatabase;
        [Tooltip("여기 등록된 액티브 스킬들의 이름/설명 로컬라이징 문구를 미리 로드해둡니다. 비워두면 스킬 쪽 예열은 건너뜁니다.")]
        [SerializeField] private SkillDatabase skillDatabase;
        [Tooltip("여기 등록된 패시브 스킬들의 이름/설명 로컬라이징 문구를 미리 로드해둡니다. 비워두면 패시브 스킬 쪽 예열은 건너뜁니다.")]
        [SerializeField] private PassiveSkillDatabase passiveSkillDatabase;

        [Header("고정 문구 (인자 없음, 한 번만 바인드되어 언어 변경 시 실시간 갱신)")]
        [SerializeField] private LocalizedString localizedActiveSkillCategoryText;
        [SerializeField] private LocalizedString localizedPassiveSkillCategoryText;
        [SerializeField] private LocalizedString localizedWeaponCategoryText;
        [SerializeField] private LocalizedString localizedPurchaseHintText;
        [SerializeField] private LocalizedString localizedRefundHintText;
        [SerializeField] private LocalizedString localizedUnequipHintText;
        [SerializeField] private LocalizedString localizedEquippedLabelText;
        [SerializeField] private LocalizedString localizedNonRefundableText;

        [Header("숫자 포맷 문구 (표시할 때마다 해당 값으로 새로 계산, {0}에 숫자가 들어감)")]
        [SerializeField] private LocalizedString localizedCooldownFormatText;
        [SerializeField] private LocalizedString localizedPointsFormatText;
        [SerializeField] private LocalizedString localizedPriceFormatText;
        [SerializeField] private LocalizedString localizedRefundFormatText;
        [SerializeField] private LocalizedString localizedBestClearTimeFormatText;

        // 로컬라이징 필드가 비어있으면 여기 적힌 기존 한국어 문구가 기본값으로 쓰인다.
        private string activeSkillCategoryCache = "액티브 스킬";
        private string passiveSkillCategoryCache = "패시브 스킬";
        private string weaponCategoryCache = "무기";
        private string purchaseHintCache = "좌클릭";
        private string refundHintCache = "우클릭";
        private string unequipHintCache = "장착해제";
        private string equippedLabelCache = "장착중";
        private string nonRefundableCache = "환불 불가";

        public string ActiveSkillCategory => activeSkillCategoryCache;
        public string PassiveSkillCategory => passiveSkillCategoryCache;
        public string WeaponCategory => weaponCategoryCache;
        public string PurchaseHint => purchaseHintCache;
        public string RefundHint => refundHintCache;
        public string UnequipHint => unequipHintCache;
        public string EquippedLabel => equippedLabelCache;
        public string NonRefundable => nonRefundableCache;

        private void Awake()
        {
            BindLocalizedString(localizedActiveSkillCategoryText, HasLocalizedString(localizedActiveSkillCategoryText), SetActiveSkillCategoryCache);
            BindLocalizedString(localizedPassiveSkillCategoryText, HasLocalizedString(localizedPassiveSkillCategoryText), SetPassiveSkillCategoryCache);
            BindLocalizedString(localizedWeaponCategoryText, HasLocalizedString(localizedWeaponCategoryText), SetWeaponCategoryCache);
            BindLocalizedString(localizedPurchaseHintText, HasLocalizedString(localizedPurchaseHintText), SetPurchaseHintCache);
            BindLocalizedString(localizedRefundHintText, HasLocalizedString(localizedRefundHintText), SetRefundHintCache);
            BindLocalizedString(localizedUnequipHintText, HasLocalizedString(localizedUnequipHintText), SetUnequipHintCache);
            BindLocalizedString(localizedEquippedLabelText, HasLocalizedString(localizedEquippedLabelText), SetEquippedLabelCache);
            BindLocalizedString(localizedNonRefundableText, HasLocalizedString(localizedNonRefundableText), SetNonRefundableCache);

            WarmUpWeaponAssets();
            WarmUpSkillAssets();
            WarmUpPassiveSkillAssets();
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void OnDestroy()
        {
            UnbindLocalizedString(localizedActiveSkillCategoryText, HasLocalizedString(localizedActiveSkillCategoryText), SetActiveSkillCategoryCache);
            UnbindLocalizedString(localizedPassiveSkillCategoryText, HasLocalizedString(localizedPassiveSkillCategoryText), SetPassiveSkillCategoryCache);
            UnbindLocalizedString(localizedWeaponCategoryText, HasLocalizedString(localizedWeaponCategoryText), SetWeaponCategoryCache);
            UnbindLocalizedString(localizedPurchaseHintText, HasLocalizedString(localizedPurchaseHintText), SetPurchaseHintCache);
            UnbindLocalizedString(localizedRefundHintText, HasLocalizedString(localizedRefundHintText), SetRefundHintCache);
            UnbindLocalizedString(localizedUnequipHintText, HasLocalizedString(localizedUnequipHintText), SetUnequipHintCache);
            UnbindLocalizedString(localizedEquippedLabelText, HasLocalizedString(localizedEquippedLabelText), SetEquippedLabelCache);
            UnbindLocalizedString(localizedNonRefundableText, HasLocalizedString(localizedNonRefundableText), SetNonRefundableCache);
        }

        // 언어가 바뀌면 고정 문구 8개는 이미 구독 중이라 자동으로 갱신되지만,
        // 무기/스킬/패시브 스킬 에셋들의 로컬라이징 문구는 실제로 그 항목을 호버하기 전까진
        // 아무도 구독하지 않으므로 여기서 미리 한 번 로드해둬야 새 언어로 처음 호버할 때도 딜레이가 없다.
        private void HandleLocaleChanged(Locale locale)
        {
            WarmUpWeaponAssets();
            WarmUpSkillAssets();
            WarmUpPassiveSkillAssets();
        }

        private void WarmUpWeaponAssets()
        {
            if (weaponDatabase == null)
            {
                return;
            }

            IReadOnlyList<BaseWeaponSO> weapons = weaponDatabase.AllWeapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                BaseWeaponSO weapon = weapons[i];
                if (weapon == null)
                {
                    continue;
                }

                RefreshIfLocalized(weapon.LocalizedDisplayName, weapon.HasLocalizedDisplayName);
                RefreshIfLocalized(weapon.LocalizedDescription, weapon.HasLocalizedDescription);
                RefreshIfLocalized(weapon.LocalizedParryingRangeTooltipText, weapon.HasLocalizedParryingRangeTooltipText);
                RefreshIfLocalized(weapon.LocalizedMaxAmmoTooltipText, weapon.HasLocalizedMaxAmmoTooltipText);
                RefreshIfLocalized(weapon.LocalizedBulletDamageTooltipText, weapon.HasLocalizedBulletDamageTooltipText);
            }
        }

        private void WarmUpSkillAssets()
        {
            if (skillDatabase == null)
            {
                return;
            }

            IReadOnlyList<BaseSkillSO> skills = skillDatabase.AllSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                BaseSkillSO skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                RefreshIfLocalized(skill.LocalizedDisplayName, skill.HasLocalizedDisplayName);
                RefreshIfLocalized(skill.LocalizedDescription, skill.HasLocalizedDescription);
            }
        }

        private void WarmUpPassiveSkillAssets()
        {
            if (passiveSkillDatabase == null)
            {
                return;
            }

            IReadOnlyList<BasePassiveSkillSO> skills = passiveSkillDatabase.AllSkills;
            for (int i = 0; i < skills.Count; i++)
            {
                BasePassiveSkillSO skill = skills[i];
                if (skill == null)
                {
                    continue;
                }

                RefreshIfLocalized(skill.LocalizedDisplayName, skill.HasLocalizedDisplayName);
                RefreshIfLocalized(skill.LocalizedDescription, skill.HasLocalizedDescription);
            }
        }

        // RefreshString()만 부르고 아무도 구독하지 않으면 로드된 결과를 아무도 붙잡고 있지 않아서
        // 그대로 해제돼버리는 것으로 보인다(실제 호버 시점에 다시 처음부터 로드됨). 그래서 결과를
        // 안 쓰더라도 반드시 핸들러를 구독했다가 완료되면 해제하는 방식으로 "붙잡아둔다".
        private static void RefreshIfLocalized(LocalizedString localizedString, bool enabled)
        {
            if (!enabled)
            {
                return;
            }

            LocalizedString.ChangeHandler handler = null;
            handler = _ => localizedString.StringChanged -= handler;

            localizedString.StringChanged += handler;
            localizedString.RefreshString();
        }

        private void SetActiveSkillCategoryCache(string value) => activeSkillCategoryCache = value;
        private void SetPassiveSkillCategoryCache(string value) => passiveSkillCategoryCache = value;
        private void SetWeaponCategoryCache(string value) => weaponCategoryCache = value;
        private void SetPurchaseHintCache(string value) => purchaseHintCache = value;
        private void SetRefundHintCache(string value) => refundHintCache = value;
        private void SetUnequipHintCache(string value) => unequipHintCache = value;
        private void SetEquippedLabelCache(string value) => equippedLabelCache = value;
        private void SetNonRefundableCache(string value) => nonRefundableCache = value;

        public string FormatCooldown(float seconds) => ResolveLocalizedFormat(localizedCooldownFormatText, "쿨타임: {0}초", seconds);
        public string FormatPoints(int points) => ResolveLocalizedFormat(localizedPointsFormatText, "포인트: {0}", points);
        public string FormatPrice(int price) => ResolveLocalizedFormat(localizedPriceFormatText, "가격: {0}", price);
        public string FormatRefund(int amount) => ResolveLocalizedFormat(localizedRefundFormatText, "환불 시 반환: {0}", amount);
        public string FormatBestClearTime(string time) => ResolveLocalizedFormat(localizedBestClearTimeFormatText, "최단 기록: {0}", time);

        // 숫자가 매번 바뀌는 문구는 미리 캐시해둘 수 없어서, 표시할 때마다 그 자리에서
        // 인자를 넣어 한 번만 새로 계산한다. GetLocalizedString은 테이블 로드가 아직 안
        // 끝났으면 내부적으로 WaitForCompletion으로 동기 대기하므로, StringChanged를
        // 구독했다가 즉시 해제하는 방식(비동기 로드 도중 해제하면 결과를 영영 못 받음)과
        // 달리 항상 실제 로컬라이징 값을 받는다.
        private static string ResolveLocalizedFormat(LocalizedString localizedString, string fallbackFormat, object arg)
        {
            if (!HasLocalizedString(localizedString))
            {
                return string.Format(fallbackFormat, arg);
            }

            return localizedString.GetLocalizedString(arg);
        }

        public static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }

        public static void BindLocalizedString(LocalizedString localizedString, bool enabled, LocalizedString.ChangeHandler handler, params object[] arguments)
        {
            if (!enabled)
            {
                return;
            }

            if (arguments.Length > 0)
            {
                localizedString.Arguments = arguments;
            }

            localizedString.StringChanged += handler;
            localizedString.RefreshString();
        }

        public static void UnbindLocalizedString(LocalizedString localizedString, bool enabled, LocalizedString.ChangeHandler handler)
        {
            if (!enabled)
            {
                return;
            }

            localizedString.StringChanged -= handler;
        }
    }
}
