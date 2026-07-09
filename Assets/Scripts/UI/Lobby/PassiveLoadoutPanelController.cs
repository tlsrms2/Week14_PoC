using UnityEngine;
using Week14.Skills;

namespace Week14.UI
{
    // 패시브 슬롯은 2개이므로, 슬롯마다 이 컨트롤러를 하나씩 배치하고 Inspector에서 slot 값을 다르게 지정합니다.
    public sealed class PassiveLoadoutPanelController : MonoBehaviour
    {
        [SerializeField] private PassiveSkillSlot slot;

        private PassiveLoadoutSkillIcon selectedIcon;

        public PassiveSkillSlot Slot => slot;

        private void OnEnable()
        {
            RefreshSelectionFromEquippedSkill();
        }

        public void SelectSkill(PassiveLoadoutSkillIcon icon, BasePassiveSkillSO skill)
        {
            if (skill == null || PassiveSkillLoadoutManager.Instance == null)
            {
                return;
            }

            if (selectedIcon == icon)
            {
                if (!PassiveSkillLoadoutManager.Instance.UnequipSkill(slot))
                {
                    return;
                }

                icon.SetSelected(false);
                selectedIcon = null;
                return;
            }

            if (!PassiveSkillLoadoutManager.Instance.EquipSkill(slot, skill.SkillId))
            {
                return;
            }

            if (selectedIcon != null)
            {
                selectedIcon.SetSelected(false);
            }

            selectedIcon = icon;
            selectedIcon.SetSelected(true);
        }

        private void RefreshSelectionFromEquippedSkill()
        {
            selectedIcon = null;

            BasePassiveSkillSO equippedSkill = PassiveSkillLoadoutManager.Instance != null
                ? PassiveSkillLoadoutManager.Instance.GetEquippedSkill(slot)
                : null;

            foreach (PassiveLoadoutSkillIcon icon in GetComponentsInChildren<PassiveLoadoutSkillIcon>(includeInactive: true))
            {
                bool isEquipped = equippedSkill != null && icon.Skill == equippedSkill;
                icon.SetSelected(isEquipped);

                if (isEquipped)
                {
                    selectedIcon = icon;
                }
            }
        }
    }
}
