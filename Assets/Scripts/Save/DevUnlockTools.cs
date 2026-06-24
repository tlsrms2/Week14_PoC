using System.Collections.Generic;
using UnityEngine;
using Week14.Skills;
using Week14.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Save
{
    [Tooltip("테스트용 개발자 기능입니다. 스킬/보스를 즉시 해금합니다.")]
    public sealed class DevUnlockTools : MonoBehaviour
    {
        [Tooltip("전체 해금 대상 스킬을 가져올 데이터베이스입니다.")]
        [SerializeField] private SkillDatabase skillDatabase;
        [Tooltip("전체 해금 대상 보스 목록입니다. 테스트하려는 보스 데이터를 등록하세요.")]
        [SerializeField] private List<BossData> bosses = new();
        [Tooltip("플레이 중 이 키를 누르면 모든 스킬/보스를 즉시 해금합니다.")]
        [SerializeField] private bool enableUnlockAllHotkey = true;
        [Tooltip("플레이 중 이 키를 누르면 모든 스킬/보스 해금을 되돌립니다.")]
        [SerializeField] private bool enableLockAllHotkey = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key unlockAllHotkey = Key.F9;
        [SerializeField] private Key lockAllHotkey = Key.F10;
#else
        [SerializeField] private KeyCode unlockAllHotkey = KeyCode.F9;
        [SerializeField] private KeyCode lockAllHotkey = KeyCode.F10;
#endif

        [Header("개별 해금/되돌리기 대상")]
        [Tooltip("아래 '선택 스킬 해금/되돌리기'가 대상으로 삼을 스킬입니다.")]
        [SerializeField] private BaseSkillSO targetSkill;
        [Tooltip("아래 '선택 보스 해금/되돌리기'가 대상으로 삼을 보스입니다.")]
        [SerializeField] private BossData targetBoss;

        private void Update()
        {
            if (enableUnlockAllHotkey && WasHotkeyPressed(unlockAllHotkey))
            {
                UnlockAll();
            }

            if (enableLockAllHotkey && WasHotkeyPressed(lockAllHotkey))
            {
                LockAll();
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool WasHotkeyPressed(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }
#else
        private static bool WasHotkeyPressed(KeyCode key)
        {
            return Input.GetKeyDown(key);
        }
#endif

        [ContextMenu("전체 해금 (스킬 + 보스)")]
        public void UnlockAll()
        {
            UnlockAllSkills();
            UnlockAllBosses();
            Debug.Log("[DevUnlockTools] 모든 스킬/보스를 해금했습니다.");
        }

        [ContextMenu("전체 해금 되돌리기 (스킬 + 보스)")]
        public void LockAll()
        {
            LockAllSkills();
            LockAllBosses();
            Debug.Log("[DevUnlockTools] 모든 스킬/보스 해금을 되돌렸습니다.");
        }

        [ContextMenu("스킬 전체 해금")]
        public void UnlockAllSkills()
        {
            if (skillDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] skillDatabase가 비어있어 스킬을 해금할 수 없습니다.");
                return;
            }

            foreach (BaseSkillSO skill in skillDatabase.AllSkills)
            {
                if (skill != null)
                {
                    GameSaveManager.UnlockSkill(skill.SkillId);
                }
            }
        }

        [ContextMenu("스킬 전체 해금 되돌리기")]
        public void LockAllSkills()
        {
            if (skillDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] skillDatabase가 비어있어 스킬을 잠글 수 없습니다.");
                return;
            }

            foreach (BaseSkillSO skill in skillDatabase.AllSkills)
            {
                if (skill != null)
                {
                    GameSaveManager.LockSkill(skill.SkillId);
                }
            }
        }

        [ContextMenu("보스 전체 해금")]
        public void UnlockAllBosses()
        {
            foreach (BossData boss in bosses)
            {
                if (boss != null)
                {
                    GameSaveManager.UnlockBoss(boss.Id);
                }
            }
        }

        [ContextMenu("보스 전체 해금 되돌리기")]
        public void LockAllBosses()
        {
            foreach (BossData boss in bosses)
            {
                if (boss != null)
                {
                    GameSaveManager.LockBoss(boss.Id);
                }
            }
        }

        [ContextMenu("선택 스킬 해금")]
        public void UnlockTargetSkill()
        {
            if (targetSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockSkill(targetSkill.SkillId);
        }

        [ContextMenu("선택 스킬 해금 되돌리기")]
        public void LockTargetSkill()
        {
            if (targetSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.LockSkill(targetSkill.SkillId);
        }

        [ContextMenu("선택 보스 해금")]
        public void UnlockTargetBoss()
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss가 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockBoss(targetBoss.Id);
        }

        [ContextMenu("선택 보스 해금 되돌리기")]
        public void LockTargetBoss()
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss가 비어있습니다.");
                return;
            }

            GameSaveManager.LockBoss(targetBoss.Id);
        }
    }
}
