using UnityEngine;
using Week14.Skills;

namespace Week14.UI
{
    public sealed class LoadoutPanelController : MonoBehaviour
    {
        private const SkillSlot ActiveSlot = SkillSlot.Skill1;

        private LoadoutSkillIcon selectedIcon;

        private void OnEnable()
        {
            RefreshSelectionFromEquippedSkill();
        }

        public void SelectSkill(LoadoutSkillIcon icon, BaseSkillSO skill)
        {
            if (skill == null || SkillLoadoutManager.Instance == null)
            {
                return;
            }

            if (selectedIcon == icon)
            {
                if (!SkillLoadoutManager.Instance.UnequipSkill(ActiveSlot))
                {
                    return;
                }

                icon.SetSelected(false);
                selectedIcon = null;
                return;
            }

            if (!SkillLoadoutManager.Instance.EquipSkill(ActiveSlot, skill.SkillId))
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

            BaseSkillSO equippedSkill = SkillLoadoutManager.Instance != null
                ? SkillLoadoutManager.Instance.GetEquippedSkill(ActiveSlot)
                : null;

            foreach (LoadoutSkillIcon icon in GetComponentsInChildren<LoadoutSkillIcon>(includeInactive: true))
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
