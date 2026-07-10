using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Input;
using Week14.Save;
using Week14.UI;

namespace Week14.Skills
{
    public sealed class SkillLoadoutManager : MonoBehaviour
    {
        private const SkillSlot ActiveSlot = SkillSlot.Skill1;

        [Tooltip("Skill ID와 실제 스킬 에셋을 연결하는 데이터베이스입니다.")]
        [SerializeField] private SkillDatabase database;
        [Tooltip("게임 시작 시 자동으로 해금+무료 구매되고, 장착된 스킬이 하나도 없으면 자동 장착되는 기본 스킬입니다. 환불(반환)이 불가능합니다. 비워두면 아무 스킬도 자동 지급하지 않습니다.")]
        [SerializeField] private BaseSkillSO defaultSkill;
        [Tooltip("테스트용: 활성 슬롯에 아무 스킬도 장착되어 있지 않을 때 시작 시 자동으로 해금하고 장착할 스킬입니다. 비워두면 자동 장착하지 않습니다.")]
        [SerializeField] private BaseSkillSO defaultTestSkill;
        [Tooltip("테스트용: 체크하면 세이브 파일의 장착 스킬을 불러오지 않고, 시작 시 항상 defaultTestSkill을 장착합니다. (세이브 파일은 읽지도 쓰지도 않습니다.)")]
        [SerializeField] private bool forceDefaultTestSkill;

        private static SkillLoadoutManager instance;

        private readonly Dictionary<SkillSlot, BaseSkillSO> equippedSkills = new();
        private float cooldownRemaining;

        // 지속시간이 있는 스킬(HasDelayedCooldownStart)을 사용한 뒤, 효과가 끝나는 콜백이 올 때까지
        // true로 유지됩니다. 이 동안은 cooldownRemaining을 감소시키지 않아 게이지가 0%로 멈춰 있습니다.
        private bool cooldownLocked;
        private BaseSkillSO effectEndSubscribedSkill;

        public static SkillLoadoutManager Instance => instance;

        public event Action<float, float> CooldownChanged;
        public event Action<SkillSlot, BaseSkillSO> SkillEquipped;
        public event Action<SkillSlot, BaseSkillSO> SkillUsed;

        public float CooldownRemaining => cooldownRemaining;

        // 액티브 스킬이 장착되어 있지 않을 때는 -1을 반환합니다.
        // (0을 쓰면 "쿨타임 0초짜리 스킬이 준비됨"과 "장착된 스킬이 없음"을 구분할 수 없기 때문)
        public float CooldownDuration
        {
            get
            {
                BaseSkillSO skill = GetEquippedSkill(ActiveSlot);
                return skill != null ? skill.CooldownSeconds : -1f;
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

            UnlockDefaultSkill();
            LoadEquippedSkills();
            EquipDefaultSkillIfNeeded();
            EquipDefaultTestSkillIfNeeded();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeEffectEnd();
        }

        private void Update()
        {
            TickCooldown(Time.deltaTime);

            if (!GameModalState.BlocksGameplayInput && GameInput.UseSkillDown)
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
            if (skill == null
                || !GameSaveManager.IsSkillUnlocked(skillId)
                || !GameSaveManager.IsSkillPurchased(skillId))
            {
                return false;
            }

            equippedSkills[slot] = skill;
            if (slot == ActiveSlot)
            {
                ResetCooldown();
            }

            GameSaveManager.SetEquippedSkillId((int)slot, skillId);
            SkillEquipped?.Invoke(slot, skill);
            return true;
        }

        public bool IsDefaultSkill(string skillId)
        {
            return defaultSkill != null && defaultSkill.SkillId == skillId;
        }

        public bool RefundSkill(string skillId)
        {
            BaseSkillSO skill = database != null ? database.FindById(skillId) : null;
            if (skill == null || skill == defaultSkill)
            {
                return false;
            }

            if (equippedSkills.TryGetValue(ActiveSlot, out BaseSkillSO equipped) && equipped == skill)
            {
                UnequipSkill(ActiveSlot);
            }

            return GameSaveManager.RefundSkill(skillId, skill.Price);
        }

        public bool UnequipSkill(SkillSlot slot)
        {
            if (!equippedSkills.Remove(slot))
            {
                return false;
            }

            if (slot == ActiveSlot)
            {
                ResetCooldown();
            }

            GameSaveManager.SetEquippedSkillId((int)slot, null);
            SkillEquipped?.Invoke(slot, null);
            return true;
        }

        public bool TryUseSkill(SkillSlot slot)
        {
            if (!equippedSkills.TryGetValue(slot, out BaseSkillSO skill) || skill == null)
            {
                return false;
            }

            if (cooldownLocked || cooldownRemaining > 0f)
            {
                return false;
            }

            // 지속시간이 있는 스킬도 게이지는 즉시 "0% 채워짐(=CooldownSeconds 그대로 남음)" 상태로
            // 표시하고, 효과가 끝날 때까지는 TickCooldown에서 감소시키지 않습니다.
            cooldownRemaining = skill.CooldownSeconds;
            GameObject user = PlayerCombatController.Active != null ? PlayerCombatController.Active.gameObject : gameObject;
            skill.Execute(user);
            SkillUsed?.Invoke(slot, skill);
            CooldownChanged?.Invoke(cooldownRemaining, skill.CooldownSeconds);

            if (skill.HasDelayedCooldownStart)
            {
                cooldownLocked = true;
                effectEndSubscribedSkill = skill;
                skill.SubscribeEffectEnd(HandleEffectEnded);
            }

            return true;
        }

        public void ResetActiveCooldown()
        {
            ResetCooldown();
        }

        private void HandleEffectEnded()
        {
            UnsubscribeEffectEnd();
            cooldownLocked = false;
        }

        private void UnsubscribeEffectEnd()
        {
            if (effectEndSubscribedSkill == null)
            {
                return;
            }

            effectEndSubscribedSkill.UnsubscribeEffectEnd(HandleEffectEnded);
            effectEndSubscribedSkill = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetCooldown();
        }

        private void TickCooldown(float deltaTime)
        {
            if (cooldownLocked || cooldownRemaining <= 0f)
            {
                return;
            }

            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - deltaTime);
            BaseSkillSO skill = GetEquippedSkill(ActiveSlot);
            CooldownChanged?.Invoke(cooldownRemaining, skill != null ? skill.CooldownSeconds : -1f);
        }

        private void ResetCooldown()
        {
            UnsubscribeEffectEnd();
            cooldownLocked = false;
            cooldownRemaining = 0f;
            BaseSkillSO skill = GetEquippedSkill(ActiveSlot);
            CooldownChanged?.Invoke(cooldownRemaining, skill != null ? skill.CooldownSeconds : -1f);
        }

        public void UnlockDefaultSkill()
        {
            if (defaultSkill != null)
            {
                GameSaveManager.UnlockSkill(defaultSkill.SkillId);
                GameSaveManager.PurchaseSkill(defaultSkill.SkillId, 0);
            }
        }

        private void EquipDefaultSkillIfNeeded()
        {
            if (defaultSkill == null || GetEquippedSkill(ActiveSlot) != null)
            {
                return;
            }

            EquipSkill(ActiveSlot, defaultSkill.SkillId);
        }

        private void EquipDefaultTestSkillIfNeeded()
        {
            if (defaultTestSkill == null)
            {
                return;
            }

            if (!forceDefaultTestSkill && GetEquippedSkill(ActiveSlot) != null)
            {
                return;
            }

            equippedSkills[ActiveSlot] = defaultTestSkill;
            ResetCooldown();
            SkillEquipped?.Invoke(ActiveSlot, defaultTestSkill);
        }

        private void LoadEquippedSkills()
        {
            equippedSkills.Clear();
            cooldownRemaining = 0f;

            if (forceDefaultTestSkill)
            {
                return;
            }

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
