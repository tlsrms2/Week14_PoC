using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
        [Tooltip("현재 표시 중인 무기/스킬이 장착 중일 때 IconImage 위에 표시할 오브젝트입니다.")]
        [SerializeField] private GameObject iconEquippedIndicator;
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

        [Header("기본으로 보여줄 항목 (아직 아무것도 호버하지 않은 최초 상태일 때 표시. 셋 중 하나만 채우세요)")]
        [SerializeField] private BaseSkillSO defaultDisplaySkill;
        [SerializeField] private BasePassiveSkillSO defaultDisplayPassiveSkill;
        [SerializeField] private BaseWeaponSO defaultDisplayWeapon;

        private Sprite defaultActionSprite;
        private BaseWeaponSO localizedWeapon;
        private BaseSkillSO localizedSkill;
        private BasePassiveSkillSO localizedPassiveSkill;

        public static LoadoutSelectedSkillPanel Instance { get; private set; }

        private string ActiveSkillCategoryText => localization != null ? localization.ActiveSkillCategory : "액티브 스킬";
        private string PassiveSkillCategoryText => localization != null ? localization.PassiveSkillCategory : "패시브 스킬";
        private string WeaponCategoryText => localization != null ? localization.WeaponCategory : "무기";
        private string PurchaseHintText => localization != null ? localization.PurchaseHint : "좌클릭";
        private string RefundHintText => localization != null ? localization.RefundHint : "우클릭";
        private string UnequipHintText => localization != null ? localization.UnequipHint : "장착해제";
        private string EquippedLabelText => localization != null ? localization.EquippedLabel : "장착중";
        private string NonRefundableText => localization != null ? localization.NonRefundable : "환불 불가";
        private string DefaultGrantedNonRefundableText => localization != null ? localization.DefaultGrantedNonRefundable : "기본 지급 / 환불 불가";

        private void Awake()
        {
            Instance = this;

            if (actionHintImage != null)
            {
                defaultActionSprite = actionHintImage.sprite;
            }

            RefreshPoints(GameSaveManager.ChallengePoints);
        }

        private void Start()
        {
            // PassiveSkillLoadoutManager가 장착된 패시브를 재적용(ApplyAllEquippedPassives)하는 시점은
            // 씬의 모든 Awake가 끝난 뒤(SceneManager.sceneLoaded)라서, ShowDefault를 Awake에서 바로 부르면
            // 아직 갱신되지 않은 보너스 값(예: ParryRangeBonus)으로 초기 설명 텍스트가 만들어질 수 있다.
            // 모든 Awake가 끝난 뒤 호출되는 Start로 미뤄서 최신 값을 반영한다.
            ShowDefault();
        }

        // 아무것도 호버하지 않은 최초 상태(패널을 처음 열었을 때)에 보여줄 항목입니다.
        // 셋 중 먼저 채워진 걸 우선순위대로 하나만 보여주고, 아무것도 안 채웠으면 비워둡니다.
        private void ShowDefault()
        {
            if (defaultDisplayWeapon != null)
            {
                Show(defaultDisplayWeapon);
                return;
            }

            if (defaultDisplaySkill != null)
            {
                Show(defaultDisplaySkill);
                return;
            }

            if (defaultDisplayPassiveSkill != null)
            {
                Show(defaultDisplayPassiveSkill);
                return;
            }

            Hide();
        }

        private void OnEnable()
        {
            GameSaveManager.ChallengePointsChanged += RefreshPoints;
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;

            // 패널이 꺼져있던 동안(구독 해제 상태) 언어가 바뀌었을 수도 있으므로,
            // 다시 켜질 때(패널을 열 때) 한 번 최신 언어로 강제 갱신해준다.
            HandleLocaleChanged(LocalizationSettings.SelectedLocale);
        }

        private void OnDisable()
        {
            GameSaveManager.ChallengePointsChanged -= RefreshPoints;
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        // pointsText는 ChallengePointsChanged 이벤트가 와야, categoryText/costText/actionHintText/
        // requiredStackText는 다시 Show()가 불려야 새 문구로 갱신되는 "스냅샷" 값들이라 언어 변경
        // 자체로는 안 바뀐다. 언어가 바뀌는 순간 직접 다시 계산해준다.
        private void HandleLocaleChanged(Locale locale)
        {
            RefreshPoints(GameSaveManager.ChallengePoints);

            if (localizedWeapon != null)
            {
                Show(localizedWeapon);
            }
            else if (localizedSkill != null)
            {
                Show(localizedSkill);
            }
            else if (localizedPassiveSkill != null)
            {
                Show(localizedPassiveSkill);
            }
        }

        private void OnDestroy()
        {
            UnbindAllLocalizedText();

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

            UnbindAllLocalizedText();
            SetCategoryText(ActiveSkillCategoryText);
            LoadoutHoverHighlight.SetHovered(skill.SkillId);

            bool refundable = SkillLoadoutManager.Instance == null || !SkillLoadoutManager.Instance.IsDefaultSkill(skill.SkillId);
            bool equipped = SkillLoadoutManager.Instance != null && SkillLoadoutManager.Instance.GetEquippedSkill(SkillSlot.Skill1) == skill;
            string cooldownText = localization != null ? localization.FormatCooldown(skill.CooldownSeconds) : $"쿨타임: {skill.CooldownSeconds}초";

            // 로컬라이징이 연결된 필드는 일반 텍스트를 먼저 넣지 않는다(무기와 동일한 이유 — 깜빡임 방지).
            string displayName = skill.HasLocalizedDisplayName ? string.Empty : skill.DisplayName;
            string description = skill.HasLocalizedDescription ? string.Empty : skill.Description;
            ShowInternal(displayName, description, cooldownText, skill.Icon, skill.Price, GameSaveManager.IsSkillPurchased(skill.SkillId), refundable, equipped);
            BindLocalizedSkillText(skill);
        }

        public void Show(BasePassiveSkillSO skill)
        {
            if (skill == null)
            {
                return;
            }

            UnbindAllLocalizedText();
            SetCategoryText(PassiveSkillCategoryText);
            LoadoutHoverHighlight.SetHovered(skill.SkillId);

            bool refundable = PassiveSkillLoadoutManager.Instance == null || !PassiveSkillLoadoutManager.Instance.IsDefaultPassiveSkill(skill.SkillId);
            bool equipped = PassiveSkillLoadoutManager.Instance != null && PassiveSkillLoadoutManager.Instance.GetEquippedSkill(PassiveSkillSlot.Passive1) == skill;

            string displayName = skill.HasLocalizedDisplayName ? string.Empty : skill.DisplayName;
            string description = skill.HasLocalizedDescription ? string.Empty : skill.Description;
            ShowInternal(displayName, description, string.Empty, skill.Icon, skill.Price, GameSaveManager.IsPassiveSkillPurchased(skill.SkillId), refundable, equipped);
            BindLocalizedPassiveSkillText(skill);
        }

        public void Show(BaseWeaponSO weapon)
        {
            if (weapon == null)
            {
                return;
            }

            UnbindAllLocalizedText();
            SetCategoryText(WeaponCategoryText);
            LoadoutHoverHighlight.SetHovered(weapon.WeaponId);

            bool refundable = WeaponLoadoutManager.Instance == null || !WeaponLoadoutManager.Instance.IsDefaultWeapon(weapon.WeaponId);
            bool equipped = WeaponLoadoutManager.Instance != null && WeaponLoadoutManager.Instance.CurrentWeapon == weapon;

            // 로컬라이징이 연결된 필드는 일반 텍스트를 먼저 넣지 않는다. 잠깐이라도 원래(비로컬라이징)
            // 텍스트가 보였다가 로컬라이징 값으로 바뀌는 깜빡임을 막기 위해서다 — 대신 비워뒀다가
            // BindLocalizedWeaponText가 곧바로 채운다. 로컬라이징이 없는 필드는 그냥 원래 텍스트를 쓴다.
            string displayName = weapon.HasLocalizedDisplayName ? string.Empty : weapon.DisplayName;
            string description = weapon.HasLocalizedDescription ? string.Empty : weapon.Description;
            ShowInternal(displayName, description, string.Empty, weapon.Icon, weapon.Price, GameSaveManager.IsWeaponPurchased(weapon.WeaponId), refundable, equipped);

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
            UnbindAllLocalizedText();
            SetCategoryText(string.Empty);
            LoadoutHoverHighlight.ClearHovered();
            ShowInternal(string.Empty, string.Empty, string.Empty, null, null, false, true, false);
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

        private void BindLocalizedSkillText(BaseSkillSO skill)
        {
            if (skill == null)
            {
                return;
            }

            localizedSkill = skill;
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(skill.LocalizedDisplayName, skill.HasLocalizedDisplayName, SetNameText);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(skill.LocalizedDescription, skill.HasLocalizedDescription, SetDescriptionText);
        }

        private void UnbindLocalizedSkillText()
        {
            if (localizedSkill == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedSkill.LocalizedDisplayName, localizedSkill.HasLocalizedDisplayName, SetNameText);
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedSkill.LocalizedDescription, localizedSkill.HasLocalizedDescription, SetDescriptionText);
            localizedSkill = null;
        }

        private void BindLocalizedPassiveSkillText(BasePassiveSkillSO skill)
        {
            if (skill == null)
            {
                return;
            }

            localizedPassiveSkill = skill;
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(skill.LocalizedDisplayName, skill.HasLocalizedDisplayName, SetNameText);
            LoadoutSelectedSkillPanelLocalization.BindLocalizedString(skill.LocalizedDescription, skill.HasLocalizedDescription, SetDescriptionText);
        }

        private void UnbindLocalizedPassiveSkillText()
        {
            if (localizedPassiveSkill == null)
            {
                return;
            }

            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedPassiveSkill.LocalizedDisplayName, localizedPassiveSkill.HasLocalizedDisplayName, SetNameText);
            LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(localizedPassiveSkill.LocalizedDescription, localizedPassiveSkill.HasLocalizedDescription, SetDescriptionText);
            localizedPassiveSkill = null;
        }

        private void UnbindAllLocalizedText()
        {
            UnbindLocalizedWeaponText();
            UnbindLocalizedSkillText();
            UnbindLocalizedPassiveSkillText();
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

        private void ShowInternal(string displayName, string description, string requiredStack, Sprite icon, int? cost, bool isPurchased, bool isRefundable, bool isEquipped)
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

            SetActiveSafe(iconEquippedIndicator, icon != null && isEquipped);

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

            // 장착 중인데 해제가 불가능한 경우에는 액션 힌트 이미지는 숨기고,
            // 대신 "장착중"이라는 상태 표시 텍스트만 보여준다(클릭해도 아무 일도 안 일어나므로).
            bool showActionHint = cost.HasValue && !isEquipped && (!isPurchased || isRefundable);
            bool showEquippedOnlyLabel = cost.HasValue && isEquipped;

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
                else if (cost.HasValue && isPurchased && !isRefundable)
                {
                    actionHintText.text = DefaultGrantedNonRefundableText;
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
