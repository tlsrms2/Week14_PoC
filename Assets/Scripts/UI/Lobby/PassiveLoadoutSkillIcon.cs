using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;
using Week14.Skills;

namespace Week14.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class PassiveLoadoutSkillIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private PassiveLoadoutPanelController panelController;
        [SerializeField] private BasePassiveSkillSO skill;
        [Tooltip("클릭으로 장착됐을 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);

        private Image iconImage;
        private RectTransform rectTransform;
        private Color baseColor;

        public BasePassiveSkillSO Skill => skill;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RefreshLockState();
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsUnlocked())
            {
                return;
            }

            LoadoutTooltipPanel.Instance?.Show(skill, rectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            LoadoutTooltipPanel.Instance?.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (panelController == null || skill == null || !IsUnlocked())
            {
                return;
            }

            panelController.SelectSkill(this, skill);
        }

        public void SetSelected(bool selected)
        {
            EnsureInitialized();

            if (iconImage != null)
            {
                iconImage.color = selected ? selectedColor : baseColor;
            }
        }

        private void EnsureInitialized()
        {
            if (iconImage != null)
            {
                return;
            }

            iconImage = GetComponent<Image>();
            rectTransform = transform as RectTransform;

            if (iconImage != null)
            {
                baseColor = iconImage.color;

                if (skill != null)
                {
                    iconImage.sprite = skill.Icon;
                }
            }
        }

        private void RefreshLockState()
        {
            bool unlocked = IsUnlocked();

            if (iconImage != null)
            {
                iconImage.enabled = unlocked;
                iconImage.raycastTarget = unlocked;
            }
        }

        // 해금 + 구매 + (이 아이콘이 속한 패널의) 슬롯 해금까지 모두 충족해야 장착 가능합니다.
        private bool IsUnlocked()
        {
            return skill != null
                && GameSaveManager.IsPassiveSkillUnlocked(skill.SkillId)
                && GameSaveManager.IsPassiveSkillPurchased(skill.SkillId)
                && PassiveSkillLoadoutManager.Instance != null
                && panelController != null
                && PassiveSkillLoadoutManager.Instance.IsSlotUnlocked(panelController.Slot);
        }
    }
}
