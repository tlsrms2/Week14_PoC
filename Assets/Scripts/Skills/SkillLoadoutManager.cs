using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Input;
using Week14.Save;

namespace Week14.Skills
{
    public sealed class SkillLoadoutManager : MonoBehaviour
    {
        private const SkillSlot ActiveSlot = SkillSlot.Skill1;

        [Tooltip("Skill ID와 실제 스킬 에셋을 연결하는 데이터베이스입니다.")]
        [SerializeField] private SkillDatabase database;
        [Tooltip("테스트용: 활성 슬롯에 아무 스킬도 장착되어 있지 않을 때 시작 시 자동으로 해금하고 장착할 스킬입니다. 비워두면 자동 장착하지 않습니다.")]
        [SerializeField] private BaseSkillSO defaultTestSkill;

        private static SkillLoadoutManager instance;

        private readonly Dictionary<SkillSlot, BaseSkillSO> equippedSkills = new();
        private int currentStack;

        public static SkillLoadoutManager Instance => instance;

        public event Action<int, int> StackChanged;
        public event Action<SkillSlot, BaseSkillSO> SkillEquipped;
        public event Action<SkillSlot, BaseSkillSO> SkillUsed;

        public int CurrentStack => currentStack;
        public int RequiredStack
        {
            get
            {
                BaseSkillSO skill = GetEquippedSkill(ActiveSlot);
                return skill != null ? Mathf.Max(1, skill.RequiredStack) : 1;
            }
        }

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
            EquipDefaultTestSkillIfNeeded();
        }

        private void OnEnable()
        {
            PlayerParryController.ProjectileParried += HandleProjectileParried;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            PlayerParryController.ProjectileParried -= HandleProjectileParried;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void Update()
        {
            if (GameInput.UseSkillDown)
            {
                TryUseSkill(ActiveSlot);
            }
        }

        public bool IsSkillUnlocked(string skillId)
        {
            return GameSaveManager.IsSkillUnlocked(skillId);
        }

        public void UnlockSkill(string skillId)
        {
            GameSaveManager.UnlockSkill(skillId);
        }

        public BaseSkillSO GetEquippedSkill(SkillSlot slot)
        {
            return equippedSkills.TryGetValue(slot, out BaseSkillSO skill) ? skill : null;
        }

        public bool EquipSkill(SkillSlot slot, string skillId)
        {
            BaseSkillSO skill = database != null ? database.FindById(skillId) : null;
            if (skill == null || !GameSaveManager.IsSkillUnlocked(skillId))
            {
                return false;
            }

            equippedSkills[slot] = skill;
            if (slot == ActiveSlot)
            {
                currentStack = 0;
                StackChanged?.Invoke(currentStack, skill.RequiredStack);
            }

            GameSaveManager.SetEquippedSkillId((int)slot, skillId);
            SkillEquipped?.Invoke(slot, skill);
            return true;
        }

        public bool TryUseSkill(SkillSlot slot)
        {
            if (!equippedSkills.TryGetValue(slot, out BaseSkillSO skill) || skill == null)
            {
                return false;
            }

            if (currentStack < skill.RequiredStack)
            {
                return false;
            }

            currentStack -= skill.RequiredStack;
            skill.Execute(gameObject);
            SkillUsed?.Invoke(slot, skill);
            StackChanged?.Invoke(currentStack, skill.RequiredStack);
            return true;
        }

        private void HandleProjectileParried()
        {
            AddStack(ActiveSlot);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetStack();
        }

        private void ResetStack()
        {
            if (currentStack == 0)
            {
                return;
            }

            currentStack = 0;
            BaseSkillSO skill = GetEquippedSkill(ActiveSlot);
            StackChanged?.Invoke(currentStack, skill != null ? skill.RequiredStack : 0);
        }

        private void AddStack(SkillSlot slot)
        {
            if (!equippedSkills.TryGetValue(slot, out BaseSkillSO skill) || skill == null)
            {
                return;
            }

            int requiredStack = Mathf.Max(1, skill.RequiredStack);
            int nextStack = Mathf.Min(requiredStack, currentStack + 1);
            if (nextStack == currentStack)
            {
                return;
            }

            currentStack = nextStack;
            StackChanged?.Invoke(currentStack, requiredStack);
        }

        private void EquipDefaultTestSkillIfNeeded()
        {
            if (defaultTestSkill == null || GetEquippedSkill(ActiveSlot) != null)
            {
                return;
            }

            GameSaveManager.UnlockSkill(defaultTestSkill.SkillId);
            EquipSkill(ActiveSlot, defaultTestSkill.SkillId);
        }

        private void LoadEquippedSkills()
        {
            equippedSkills.Clear();
            currentStack = 0;

            foreach (SkillSlot slot in Enum.GetValues(typeof(SkillSlot)))
            {
                string skillId = GameSaveManager.GetEquippedSkillId((int)slot);
                BaseSkillSO skill = database != null ? database.FindById(skillId) : null;
                if (skill != null)
                {
                    equippedSkills[slot] = skill;
                }
            }
        }
    }
}
