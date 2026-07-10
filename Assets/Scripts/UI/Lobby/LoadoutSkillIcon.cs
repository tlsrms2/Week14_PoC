using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;
using Week14.Skills;

namespace Week14.UI
{
    // 액티브 스킬 로스터의 스킬칸 하나입니다. 슬롯과 무관하게 하나만 존재하는 공유 목록의 아이템입니다.
    [RequireComponent(typeof(Image))]
    public sealed class LoadoutSkillIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const SkillSlot ActiveSlot = SkillSlot.Skill1;

        [SerializeField] private BaseSkillSO skill;
        [Tooltip("잠김(해금O 구매X) 상태일 때 스킬 이미지 위에 표시할 잠금 마크입니다.")]
        [SerializeField] private GameObject lockMarkOverlay;
        [Tooltip("미해금 상태일 때 스킬 이미지 대신 표시할 스프라이트입니다. 비워두면 그냥 아이콘을 숨깁니다.")]
        [SerializeField] private Sprite lockedPlaceholderSprite;
        [Tooltip("지금 장착되어 있는 스킬일 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color equippedTintColor = new(1f, 0.85f, 0.3f);
        [Tooltip("이 스킬이 (스킬칸/스킬슬롯 어디서든) 마지막으로 호버되었을 때 켤 테두리 오브젝트입니다. 장착 강조와는 별개로 동작합니다.")]
        [SerializeField] private GameObject hoverOutline;
        [Tooltip("잠김 또는 구매 상태일 때만(=미해금이 아닐 때만) 표시할 이미지 패널입니다.")]
        [SerializeField] private GameObject unlockedInfoPanel;

        private Image iconImage;
        private Color baseColor;
        private bool subscribedToSkillEquipped;

        public BaseSkillSO Skill => skill;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RefreshVisualState();
            RefreshEquippedTint();
            SetActiveSafe(hoverOutline, false);
            TrySubscribe();
            LoadoutHoverHighlight.HoveredSkillIdChanged += HandleHoveredSkillChanged;
        }

        private void Start()
        {
            // SkillLoadoutManager는 씬에 늦게 생기는 DontDestroyOnLoad 싱글턴이라
            // OnEnable 시점엔 Instance가 아직 null일 수 있다. Start에서 한 번 더 시도한다.
            RefreshEquippedTint();
            TrySubscribe();
        }

        private void OnDisable()
        {
            if (subscribedToSkillEquipped && SkillLoadoutManager.Instance != null)
            {
                SkillLoadoutManager.Instance.SkillEquipped -= HandleSkillEquipped;
            }

            subscribedToSkillEquipped = false;
            LoadoutHoverHighlight.HoveredSkillIdChanged -= HandleHoveredSkillChanged;
        }

        private void OnValidate()
        {
            if (skill == null)
            {
                return;
            }

            Image image = GetComponent<Image>();
            if (image != null)
            {
                image.sprite = skill.Icon;
            }
        }

        private void TrySubscribe()
        {
            if (subscribedToSkillEquipped || SkillLoadoutManager.Instance == null)
            {
                return;
            }

            SkillLoadoutManager.Instance.SkillEquipped += HandleSkillEquipped;
            subscribedToSkillEquipped = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (GetState() == LoadoutSkillLockState.NotUnlocked)
            {
                return;
            }

            LoadoutSelectedSkillPanel.Instance?.Show(skill);
            LoadoutHoverHighlight.SetHovered(skill.SkillId);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            LoadoutSelectedSkillPanel.Instance?.Hide();
            LoadoutHoverHighlight.ClearHovered();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (skill == null || SkillLoadoutManager.Instance == null)
            {
                return;
            }

            LoadoutSkillLockState state = GetState();
            bool isEquipped = SkillLoadoutManager.Instance.GetEquippedSkill(ActiveSlot) == skill;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (state == LoadoutSkillLockState.Locked)
                {
                    if (GameSaveManager.PurchaseSkill(skill.SkillId, skill.Price))
                    {
                        RefreshVisualState();
                        LoadoutSelectedSkillPanel.Instance?.Show(skill);
                    }
                }
                else if (state == LoadoutSkillLockState.Purchased && !isEquipped)
                {
                    SkillLoadoutManager.Instance.EquipSkill(ActiveSlot, skill.SkillId);
                    LoadoutSelectedSkillPanel.Instance?.Show(skill);
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (state == LoadoutSkillLockState.Purchased)
                {
                    if (isEquipped)
                    {
                        SkillLoadoutManager.Instance.UnequipSkill(ActiveSlot);
                        LoadoutSelectedSkillPanel.Instance?.Show(skill);
                    }
                    else if (SkillLoadoutManager.Instance.RefundSkill(skill.SkillId))
                    {
                        RefreshVisualState();
                        LoadoutSelectedSkillPanel.Instance?.Show(skill);
                    }
                }
            }
        }

        private void HandleSkillEquipped(SkillSlot slot, BaseSkillSO _)
        {
            if (slot == ActiveSlot)
            {
                RefreshEquippedTint();
            }
        }

        private void HandleHoveredSkillChanged(string hoveredSkillId)
        {
            if (hoverOutline != null)
            {
                hoverOutline.SetActive(skill != null && hoveredSkillId == skill.SkillId);
            }
        }

        private void RefreshEquippedTint()
        {
            EnsureInitialized();

            if (iconImage == null)
            {
                return;
            }

            bool isEquipped = skill != null && SkillLoadoutManager.Instance != null && SkillLoadoutManager.Instance.GetEquippedSkill(ActiveSlot) == skill;
            iconImage.color = isEquipped ? equippedTintColor : baseColor;
        }

        private void EnsureInitialized()
        {
            if (iconImage != null)
            {
                return;
            }

            iconImage = GetComponent<Image>();

            if (iconImage != null)
            {
                baseColor = iconImage.color;
            }
        }

        private LoadoutSkillLockState GetState()
        {
            if (skill == null || !GameSaveManager.IsSkillUnlocked(skill.SkillId))
            {
                return LoadoutSkillLockState.NotUnlocked;
            }

            return GameSaveManager.IsSkillPurchased(skill.SkillId)
                ? LoadoutSkillLockState.Purchased
                : LoadoutSkillLockState.Locked;
        }

        private void RefreshVisualState()
        {
            EnsureInitialized();

            if (iconImage == null)
            {
                return;
            }

            if (skill == null)
            {
                iconImage.enabled = false;
                iconImage.raycastTarget = false;
                SetActiveSafe(lockMarkOverlay, false);
                SetActiveSafe(unlockedInfoPanel, false);
                return;
            }

            LoadoutSkillLockState state = GetState();

            switch (state)
            {
                case LoadoutSkillLockState.NotUnlocked:
                    iconImage.sprite = lockedPlaceholderSprite;
                    iconImage.enabled = lockedPlaceholderSprite != null;
                    iconImage.raycastTarget = false;
                    SetActiveSafe(lockMarkOverlay, false);
                    SetActiveSafe(unlockedInfoPanel, false);
                    break;
                case LoadoutSkillLockState.Locked:
                    iconImage.sprite = skill.Icon;
                    iconImage.enabled = true;
                    iconImage.raycastTarget = true;
                    SetActiveSafe(lockMarkOverlay, true);
                    SetActiveSafe(unlockedInfoPanel, true);
                    break;
                case LoadoutSkillLockState.Purchased:
                    iconImage.sprite = skill.Icon;
                    iconImage.enabled = true;
                    iconImage.raycastTarget = true;
                    SetActiveSafe(lockMarkOverlay, false);
                    SetActiveSafe(unlockedInfoPanel, true);
                    break;
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
