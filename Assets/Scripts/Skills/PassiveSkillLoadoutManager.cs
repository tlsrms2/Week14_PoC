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
        [Tooltip("패시브 슬롯1을 해금하는 데 필요한 고유 보스 처치 종류 수입니다.")]
        [SerializeField, Min(0)] private int passive1RequiredBossCount;
        [Tooltip("패시브 슬롯2를 해금하는 데 필요한 고유 보스 처치 종류 수입니다.")]
        [SerializeField, Min(0)] private int passive2RequiredBossCount;

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

        public int RequiredBossCount(PassiveSkillSlot slot)
        {
            return slot == PassiveSkillSlot.Passive1 ? passive1RequiredBossCount : passive2RequiredBossCount;
        }

        public bool IsSlotUnlocked(PassiveSkillSlot slot)
        {
            return GameSaveManager.ClearedBossCount >= RequiredBossCount(slot);
        }

        public BasePassiveSkillSO GetEquippedSkill(PassiveSkillSlot slot)
        {
            return equippedSkills.TryGetValue(slot, out BasePassiveSkillSO skill) ? skill : null;
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
                || !IsSlotUnlocked(slot)
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
            equippedSkills.Remove(slot);

            GameSaveManager.SetEquippedPassiveSkillId((int)slot, null);
            SkillEquipped?.Invoke(slot, null);
            return true;
        }

        public bool RefundSkill(string skillId)
        {
            BasePassiveSkillSO skill = database != null ? database.FindById(skillId) : null;
            if (skill == null)
            {
                return false;
            }

            foreach (PassiveSkillSlot slot in Enum.GetValues(typeof(PassiveSkillSlot)))
            {
                if (equippedSkills.TryGetValue(slot, out BasePassiveSkillSO equipped) && equipped == skill)
                {
                    UnequipSkill(slot);
                    break;
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
