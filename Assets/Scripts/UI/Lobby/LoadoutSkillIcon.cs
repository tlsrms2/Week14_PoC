using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Save;
using Week14.Skills;

namespace Week14.UI
{
    [RequireComponent(typeof(Image))]
    public sealed class LoadoutSkillIcon : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private LoadoutPanelController panelController;
        [SerializeField] private BaseSkillSO skill;
        [Tooltip("클릭으로 장착됐을 때 적용할 색상입니다. 기본 이미지 색과 구분되는 색으로 설정하세요.")]
        [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.3f);

        private Image iconImage;
        private RectTransform rectTransform;
        private Color baseColor;

        public BaseSkillSO Skill => skill;

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

        private bool IsUnlocked()
        {
            return skill != null && GameSaveManager.IsSkillUnlocked(skill.SkillId);
        }
    }
}
