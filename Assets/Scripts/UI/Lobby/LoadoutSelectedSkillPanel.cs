using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Week14.Save;
using Week14.Skills;
using Week14.Weapons;

namespace Week14.UI
{
    // 로드아웃 패널 오른쪽에 고정 배치되는 "선택스킬 설명" 구역입니다.
    // 팝업처럼 뜨고 사라지는 게 아니라 패널 안에 항상 붙어있고, 호버할 때마다 내용만 갱신됩니다.
    // 문구 로컬라이징은 LoadoutSelectedSkillPanelLocalization이 전담합니다.
    public sealed class LoadoutSelectedSkillPanel : MonoBehaviour
    {
        [Tooltip("문구 로컬라이징을 담당하는 컴포넌트입니다. 비워두면 한국어 기본 문구로 동작합니다.")]
        [SerializeField] private LoadoutSelectedSkillPanelLocalization localization;

        [Tooltip("호버 중인 항목이 무기/액티브 스킬/패시브 스킬 중 무엇인지 표시할 텍스트입니다.")]
        [SerializeField] private TMP_Text categoryText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text requiredStackText;
        [SerializeField] private Image iconImage;
        [Tooltip("무기를 호버했을 때만 켜지는 무기 전용 수치 패널입니다. 스킬/패시브 호버 시, 그리고 아무것도 호버하지 않을 때는 꺼집니다.")]
        [SerializeField] private GameObject weaponStatsPanel;
        [SerializeField] private TMP_Text parryingRangeText;
        [SerializeField] private BulletDamageDisplay bulletDamageDisplay;
        [Tooltip("현재 보유 중인 챌린지 포인트를 표시할 텍스트입니다.")]
        [SerializeField] private TMP_Text pointsText;
        [Tooltip("지금 호버 중인 스킬/무기의 가격을 표시할 텍스트입니다. 호버 중이 아닐 때는 비워집니다.")]
        [SerializeField] private TMP_Text costText;
        [Tooltip("costText와 같이 켜지고 꺼지는 이미지입니다(예: 포인트/코인 아이콘). costText가 비워질 때 같이 꺼집니다.")]
        [SerializeField] private Image costIcon;
        [Tooltip("좌클릭/우클릭 행동 힌트를 보여줄 이미지입니다. 미구매 상태에서는 원래(에디터에 지정된) 이미지를 그대로 쓰고, 구매 상태에서는 purchasedActionSprite로 바뀝니다.")]
        [SerializeField] private Image actionHintImage;
        [Tooltip("구매된 항목(장착 중이든 아니든)을 호버했을 때 actionHintImage에 적용할 이미지입니다(우클릭 힌트용).")]
        [SerializeField] private Sprite purchasedActionSprite;
        [SerializeField] private TMP_Text actionHintText;

        private Sprite defaultActionSprite;
        private BaseWeaponSO localizedWeapon;

        public static LoadoutSelectedSkillPanel Instance { get; private set; }

        private string ActiveSkillCategoryText => localization != null ? localization.ActiveSkillCategory : "액티브 스킬";
        private string PassiveSkillCategoryText => localization != null ? localization.PassiveSkillCategory : "패시브 스킬";
        private string WeaponCategoryText => localization != null ? localization.WeaponCategory : "무기";
        private string PurchaseHintText => localization != null ? localization.PurchaseHint : "좌클릭";
        private string RefundHintText => localization != null ? localization.RefundHint : "우클릭";
        private string UnequipHintText => localization != null ? localization.UnequipHint : "장착해제";
        private string EquippedLabelText => localization != null ? localization.EquippedLabel : "장착중";
        private string NonRefundableText => localization != null ? localization.NonRefundable : "환불 불가";

        private void Awake()
        {
            Instance = this;

            if (actionHintImage != null)
            {
                defaultActionSprite = actionHintImage.sprite;
            }

            Hide();
            RefreshPoints(GameSaveManager.ChallengePoints);
        }

        private void OnEnable()
        {
            GameSaveManager.ChallengePointsChanged += RefreshPoints;
        }

        private void OnDisable()
        {
            GameSaveManager.ChallengePointsChanged -= RefreshPoints;
        }

        private void OnDestroy()
        {
            UnbindLocalizedWeaponText();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void RefreshPoints(int points)
        {
            if (pointsText != null)
            {
                pointsText.text = localization != null ? localization.FormatPoints(points) : $"포인트: {points}";
            }
        }

        public void Show(BaseSkillSO skill)
        {
            if (skill == null)
            {
                return;
            }

            UnbindLocalizedWeaponText();
            SetCategoryText(ActiveSkillCategoryText);

            bool refundable = SkillLoadoutManager.Instance == null || !SkillLoadoutManager.Instance.IsDefaultSkill(skill.SkillId);
            bool equipped = SkillLoadoutManager.Instance != null && SkillLoadoutManager.Instance.GetEquippedSkill(SkillSlot.Skill1) == skill;
            string cooldownText = localization != null ? localization.FormatCooldown(skill.CooldownSeconds) : $"쿨타임: {skill.CooldownSeconds}초";
            ShowInternal(skill.DisplayName, skill.Description, cooldownText, skill.Icon, skill.Price, GameSaveManager.IsSkillPurchased(skill.SkillId), refundable, equipped, true);
        }

        public void Show(BasePassiveSkillSO skill)
        {
            if (skill == null)
            {
                return;
            }

            UnbindLocalizedWeaponText();
            SetCategoryText(PassiveSkillCategoryText);

            bool equipped = PassiveSkillLoadoutManager.Instance != null && PassiveSkillLoadoutManager.Instance.GetEquippedSkill(PassiveSkillSlot.Passive1) == skill;
            ShowInternal(skill.DisplayName, skill.Description, string.Empty, skill.Icon, skill.Price, GameSaveManager.IsPassiveSkillPurchased(skill.SkillId), true, equipped, true);
        }

        public void Show(BaseWeaponSO weapon)
        {
            if (weapon == null)
            {
                return;
            }

            UnbindLocalizedWeaponText();
            SetCategoryText(WeaponCategoryText);

            bool refundable = WeaponLoadoutManager.Instance == null || !WeaponLoadoutManager.Instance.IsDefaultWeapon(weapon.WeaponId);
            bool equipped = WeaponLoadoutManager.Instance != null && WeaponLoadoutManager.Instance.CurrentWeapon == weapon;

            // 로컬라이징이 연결된 필드는 일반 텍스트를 먼저 넣지 않는다. 잠깐이라도 원래(비로컬라이징)
            // 텍스트가 보였다가 로컬라이징 값으로 바뀌는 깜빡임을 막기 위해서다 — 대신 비워뒀다가
            // BindLocalizedWeaponText가 곧바로 채운다. 로컬라이징이 없는 필드는 그냥 원래 텍스트를 쓴다.
            string displayName = weapon.HasLocalizedDisplayName ? string.Empty : weapon.DisplayName;
            string description = weapon.HasLocalizedDescription ? string.Empty : weapon.Description;
            ShowInternal(displayName, description, string.Empty, weapon.Icon, weapon.Price, GameSaveManager.IsWeaponPurchased(weapon.WeaponId), refundable, equipped, false);

            SetActiveSafe(weaponStatsPanel, true);
            SetParryingRangeText(weapon.HasLocalizedParryingRangeTooltipText ? string.Empty : weapon.ParryingRangeTooltipText);
            BindLocalizedWeaponText(weapon);

            if (bulletDamageDisplay != null)
            {
                bulletDamageDisplay.Refresh(weapon);
            }
        }

        public void Hide()
        {
            UnbindLocalizedWeaponText();
            SetCategoryText(string.Empty);
            ShowInternal(string.Empty, string.Empty, string.Empty, null, null, false, true, false, true);
        }

        private void SetCategoryText(string value)
        {
            if (categoryText != null)
            {
                categoryText.text = value;
            }
        }

        private void BindLocalizedWeaponText(BaseWeaponSO weapon)
        {
            if (weapon == null)
            {
                return;
            }

            localizedWeapon = weapon;
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(weapon.LocalizedDisplayName, weapon.HasLocalizedDisplayName, SetNameText);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(weapon.LocalizedDescription, weapon.HasLocalizedDescription, SetDescriptionText);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(weapon.LocalizedParryingRangeTooltipText, weapon.HasLocalizedParryingRangeTooltipText, SetParryingRangeText, weapon.ParryingRangeTooltipArguments);
        }

        private void UnbindLocalizedWeaponText()
        {
            if (localizedWeapon == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedWeapon.LocalizedDisplayName, localizedWeapon.HasLocalizedDisplayName, SetNameText);
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedWeapon.LocalizedDescription, localizedWeapon.HasLocalizedDescription, SetDescriptionText);
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedWeapon.LocalizedParryingRangeTooltipText, localizedWeapon.HasLocalizedParryingRangeTooltipText, SetParryingRangeText);
            localizedWeapon = null;
        }

        private void SetNameText(string value)
        {
            if (nameText != null)
            {
                nameText.text = value;
            }
        }

        private void SetDescriptionText(string value)
        {
            if (descriptionText != null)
            {
                descriptionText.text = value;
            }
        }

        private void SetParryingRangeText(string value)
        {
            if (parryingRangeText != null)
            {
                parryingRangeText.text = value;
            }
        }

        private void ShowInternal(string displayName, string description, string requiredStack, Sprite icon, int? cost, bool isPurchased, bool isRefundable, bool isEquipped, bool canUnequip)
        {
            SetNameText(displayName);
            SetDescriptionText(description);

            if (requiredStackText != null)
            {
                requiredStackText.text = requiredStack;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            bool showCost = cost.HasValue && !isEquipped;

            if (costText != null)
            {
                costText.text = showCost
                    ? (isPurchased
                        ? (isRefundable
                            ? (localization != null ? localization.FormatRefund(cost.Value) : $"환불 시 반환: {cost.Value}")
                            : NonRefundableText)
                        : (localization != null ? localization.FormatPrice(cost.Value) : $"가격: {cost.Value}"))
                    : string.Empty;
                costText.enabled = showCost;
            }

            if (costIcon != null)
            {
                costIcon.enabled = showCost;
            }

            // 장착 중인데 해제가 불가능한 경우(무기)에는 액션 힌트 이미지는 숨기고,
            // 대신 "장착중"이라는 상태 표시 텍스트만 보여준다(클릭해도 아무 일도 안 일어나므로).
            bool showActionHint = cost.HasValue && (isEquipped ? canUnequip : (!isPurchased || isRefundable));
            bool showEquippedOnlyLabel = cost.HasValue && isEquipped && !canUnequip;

            if (actionHintImage != null)
            {
                actionHintImage.sprite = isPurchased ? purchasedActionSprite : defaultActionSprite;
                actionHintImage.enabled = showActionHint;
            }

            if (actionHintText != null)
            {
                if (showActionHint)
                {
                    actionHintText.text = isEquipped ? UnequipHintText : (isPurchased ? RefundHintText : PurchaseHintText);
                    actionHintText.enabled = true;
                }
                else if (showEquippedOnlyLabel)
                {
                    actionHintText.text = EquippedLabelText;
                    actionHintText.enabled = true;
                }
                else
                {
                    actionHintText.text = string.Empty;
                    actionHintText.enabled = false;
                }
            }

            SetActiveSafe(weaponStatsPanel, false);

            if (bulletDamageDisplay != null)
            {
                bulletDamageDisplay.Clear();
            }
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
