using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Save;

namespace Week14.Skills
{
    public sealed class PassiveSkillLoadoutManager : MonoBehaviour
    {
        [Tooltip("Skill ID와 실제 패시브 스킬 에셋을 연결하는 데이터베이스입니다.")]
        [SerializeField] private PassiveSkillDatabase database;
        [Tooltip("Passive1 슬롯에 장착된 패시브 스킬이 하나도 없으면 자동 장착되는 기본 패시브 스킬입니다. 환불(반환)이 불가능합니다. " +
            "해금+무료 구매는 GameSaveConfig(Resources/GameSaveConfig.asset)의 기본 해금 패시브 스킬 목록에서 처리되므로, " +
            "실제로 장착되게 하려면 그 목록에도 같은 패시브 스킬을 등록해야 합니다. 비워두면 아무 패시브 스킬도 자동 장착하지 않습니다.")]
        [SerializeField] private BasePassiveSkillSO defaultPassiveSkill;

        private static PassiveSkillLoadoutManager instance;

        private readonly Dictionary<PassiveSkillSlot, BasePassiveSkillSO> equippedSkills = new();

        public static PassiveSkillLoadoutManager Instance => instance;

        public event Action<PassiveSkillSlot, BasePassiveSkillSO> SkillEquipped;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            LoadEquippedSkills();
            EquipDefaultPassiveSkillIfNeeded();
            ApplyAllEquippedPassives();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        public BasePassiveSkillSO GetEquippedSkill(PassiveSkillSlot slot)
        {
            return equippedSkills.TryGetValue(slot, out BasePassiveSkillSO skill) ? skill : null;
        }

        public bool IsDefaultPassiveSkill(string skillId)
        {
            return defaultPassiveSkill != null && defaultPassiveSkill.SkillId == skillId;
        }

        public bool IsSkillEquippedInAnySlot(string skillId)
        {
            foreach (BasePassiveSkillSO skill in equippedSkills.Values)
            {
                if (skill != null && skill.SkillId == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool EquipSkill(PassiveSkillSlot slot, string skillId)
        {
            BasePassiveSkillSO skill = database != null ? database.FindById(skillId) : null;
            if (skill == null
                || !GameSaveManager.IsPassiveSkillUnlocked(skillId)
                || !GameSaveManager.IsPassiveSkillPurchased(skillId)
                || IsSkillEquippedInAnySlot(skillId))
            {
                return false;
            }

            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;

            if (equippedSkills.TryGetValue(slot, out BasePassiveSkillSO previousSkill))
            {
                previousSkill?.RemovePassive(playerObject);
            }

            equippedSkills[slot] = skill;
            skill.ApplyPassive(playerObject);

            GameSaveManager.SetEquippedPassiveSkillId((int)slot, skillId);
            SkillEquipped?.Invoke(slot, skill);
            return true;
        }

        public bool UnequipSkill(PassiveSkillSlot slot)
        {
            if (!equippedSkills.TryGetValue(slot, out BasePassiveSkillSO skill))
            {
                return false;
            }

            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;
            skill?.RemovePassive(playerObject);

            if (slot == PassiveSkillSlot.Passive1 && defaultPassiveSkill != null)
            {
                equippedSkills[slot] = defaultPassiveSkill;
                defaultPassiveSkill.ApplyPassive(playerObject);
                GameSaveManager.SetEquippedPassiveSkillId((int)slot, defaultPassiveSkill.SkillId);
                SkillEquipped?.Invoke(slot, defaultPassiveSkill);
                return true;
            }

            equippedSkills.Remove(slot);

            GameSaveManager.SetEquippedPassiveSkillId((int)slot, null);
            SkillEquipped?.Invoke(slot, null);
            return true;
        }

        public bool RefundSkill(string skillId)
        {
            BasePassiveSkillSO skill = database != null ? database.FindById(skillId) : null;
            if (skill == null || skill == defaultPassiveSkill)
            {
                return false;
            }

            foreach (PassiveSkillSlot slot in Enum.GetValues(typeof(PassiveSkillSlot)))
            {
                if (equippedSkills.TryGetValue(slot, out BasePassiveSkillSO equipped) && equipped == skill)
                {
                    return false;
                }
            }

            return GameSaveManager.RefundPassiveSkill(skillId, skill.Price);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyAllEquippedPassives();
        }

        private void ApplyAllEquippedPassives()
        {
            GameObject playerObject = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : null;
            foreach (BasePassiveSkillSO skill in equippedSkills.Values)
            {
                skill?.ApplyPassive(playerObject);
            }
        }

        // 기본 패시브가 있으면 Passive1 슬롯은 항상 하나를 장착합니다. 예전 세이브에 명시적 해제(null)가
        // 남아 있어도 여기서 기본 패시브로 보정합니다.
        private void EquipDefaultPassiveSkillIfNeeded()
        {
            if (defaultPassiveSkill == null || GetEquippedSkill(PassiveSkillSlot.Passive1) != null)
            {
                return;
            }

            EquipSkill(PassiveSkillSlot.Passive1, defaultPassiveSkill.SkillId);
        }

        private void LoadEquippedSkills()
        {
            equippedSkills.Clear();

            foreach (PassiveSkillSlot slot in Enum.GetValues(typeof(PassiveSkillSlot)))
            {
                string skillId = GameSaveManager.GetEquippedPassiveSkillId((int)slot);
                BasePassiveSkillSO skill = database != null ? database.FindById(skillId) : null;
                if (skill != null)
                {
                    equippedSkills[slot] = skill;
                }
            }
        }
    }
}
