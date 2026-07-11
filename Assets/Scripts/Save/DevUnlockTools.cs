using System.Collections.Generic;
using UnityEngine;
using Week14.Skills;
using Week14.Story;
using Week14.UI;
using Week14.Weapons;
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
        [Tooltip("전체 해금 대상 패시브 스킬을 가져올 데이터베이스입니다.")]
        [SerializeField] private PassiveSkillDatabase passiveSkillDatabase;
        [Tooltip("전체 해금 대상 보스 목록입니다. 테스트하려는 보스 데이터를 등록하세요.")]
        [SerializeField] private List<BossData> bosses = new();
        [Tooltip("전체 해금 대상 총기를 가져올 데이터베이스입니다.")]
        [SerializeField] private WeaponDatabase weaponDatabase;
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
        [Tooltip("아래 '선택 패시브 스킬 해금/되돌리기'가 대상으로 삼을 패시브 스킬입니다.")]
        [SerializeField] private BasePassiveSkillSO targetPassiveSkill;
        [Tooltip("아래 '선택 보스 해금/되돌리기'가 대상으로 삼을 보스입니다.")]
        [SerializeField] private BossData targetBoss;
        [Tooltip("아래 '선택 총기 해금/되돌리기'가 대상으로 삼을 총기입니다.")]
        [SerializeField] private BaseWeaponSO targetWeapon;
        [Tooltip("'테스트 포인트 지급'이 지급할 챌린지 포인트 양입니다.")]
        [SerializeField] private int debugPointsToGrant = 100;

        [Header("스토리 토글")]
        [SerializeField] private bool prologueSeen;
        [SerializeField] private bool tutorialCompleted;
        [SerializeField] private bool pastSeen;
        [SerializeField] private bool act1Seen;
        [SerializeField] private bool lobbyTutorialBossSeen;
        [SerializeField] private bool act2Seen;
        [SerializeField] private bool act3Seen;
        [SerializeField] private bool finalBossAftermathSeen;
        [SerializeField] private bool epilogueSeen;

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

        [ContextMenu("전체 해금 (스킬 + 패시브 + 보스 + 총기)")]
        public void UnlockAll()
        {
            UnlockAllSkills();
            UnlockAllPassiveSkills();
            UnlockAllBosses();
            UnlockAllWeapons();
            Debug.Log("[DevUnlockTools] 모든 스킬/패시브/보스/총기를 해금했습니다.");
        }

        [ContextMenu("전체 해금 되돌리기 (스킬 + 패시브 + 보스 + 총기)")]
        public void LockAll()
        {
            LockAllSkills();
            LockAllPassiveSkills();
            LockAllBosses();
            LockAllWeapons();
            Debug.Log("[DevUnlockTools] 모든 스킬/패시브/보스/총기 해금을 되돌렸습니다.");
        }

        [ContextMenu("스토리 토글/1. 전체 초기화")]
        public void ClearStoryToggles()
        {
            GameSaveManager.ResetStoryProgress();
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 스토리 진행 상태를 리셋했습니다.");
        }

        [ContextMenu("스토리 토글/2. 프롤로그 완료")]
        public void CompletePrologue()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: false,
                act1: false,
                act2: false,
                act3: false,
                finalBossAftermath: false,
                epilogue: false);
            Debug.Log("[DevUnlockTools] 시눕시스 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/3. 튜토리얼 완료")]
        public void CompleteTutorial()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: false,
                act2: false,
                act3: false,
                finalBossAftermath: false,
                epilogue: false);
            Debug.Log("[DevUnlockTools] 튜토리얼 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/4. 과거 완료")]
        public void CompletePast()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: false,
                act2: false,
                act3: false,
                finalBossAftermath: false,
                epilogue: false);
            GameSaveManager.SetPastSeen(true);
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 과거 컷신 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/5. Act1 완료")]
        public void CompleteAct1()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: true,
                act2: false,
                act3: false,
                finalBossAftermath: false,
                epilogue: false);
            Debug.Log("[DevUnlockTools] Act1 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/6. 로비 보스 튜토리얼 완료")]
        public void CompleteLobbyTutorialBoss()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: true,
                act2: false,
                act3: false,
                finalBossAftermath: false,
                epilogue: false);
            SetStorySeen(StoryEpisodeId.LobbyTutorialBoss, true);
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 로비 보스 튜토리얼 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/7. Act2 완료")]
        public void CompleteAct2()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: true,
                act2: true,
                act3: false,
                finalBossAftermath: false,
                epilogue: false);
            Debug.Log("[DevUnlockTools] Act2 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/8. Act3 완료")]
        public void CompleteAct3()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: true,
                act2: true,
                act3: true,
                finalBossAftermath: false,
                epilogue: false);
            Debug.Log("[DevUnlockTools] Act3 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/9. 최종 보스 완료")]
        public void CompleteFinalBoss()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: true,
                act2: true,
                act3: true,
                finalBossAftermath: true,
                epilogue: false);
            Debug.Log("[DevUnlockTools] 최종 보스 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/10. 에필로그 완료")]
        public void CompleteEpilogue()
        {
            SetStoryProgress(
                synopsis: true,
                tutorial: true,
                act1: true,
                act2: true,
                act3: true,
                finalBossAftermath: true,
                epilogue: true);
            Debug.Log("[DevUnlockTools] 에필로그 완료 상태로 변경했습니다.");
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

        [ContextMenu("패시브 스킬 전체 해금")]
        public void UnlockAllPassiveSkills()
        {
            if (passiveSkillDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] passiveSkillDatabase가 비어있어 패시브 스킬을 해금할 수 없습니다.");
                return;
            }

            foreach (BasePassiveSkillSO skill in passiveSkillDatabase.AllSkills)
            {
                if (skill != null)
                {
                    GameSaveManager.UnlockPassiveSkill(skill.SkillId);
                }
            }
        }

        [ContextMenu("패시브 스킬 전체 해금 되돌리기")]
        public void LockAllPassiveSkills()
        {
            if (passiveSkillDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] passiveSkillDatabase가 비어있어 패시브 스킬을 잠글 수 없습니다.");
                return;
            }

            foreach (BasePassiveSkillSO skill in passiveSkillDatabase.AllSkills)
            {
                if (skill != null)
                {
                    GameSaveManager.LockPassiveSkill(skill.SkillId);
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
                    GameSaveManager.UnclearBoss(boss.Id);
                }
            }

            GameSaveManager.UnlockDefaultBoss();
        }

        [ContextMenu("총기 전체 해금")]
        public void UnlockAllWeapons()
        {
            if (weaponDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] weaponDatabase가 비어있어 총기를 해금할 수 없습니다.");
                return;
            }

            foreach (BaseWeaponSO weapon in weaponDatabase.AllWeapons)
            {
                if (weapon != null)
                {
                    GameSaveManager.UnlockWeapon(weapon.WeaponId);
                }
            }
        }

        [ContextMenu("총기 전체 해금 되돌리기")]
        public void LockAllWeapons()
        {
            if (weaponDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] weaponDatabase가 비어있어 총기를 잠글 수 없습니다.");
                return;
            }

            foreach (BaseWeaponSO weapon in weaponDatabase.AllWeapons)
            {
                if (weapon != null)
                {
                    GameSaveManager.LockWeapon(weapon.WeaponId);
                }
            }

            if (WeaponLoadoutManager.Instance != null)
            {
                WeaponLoadoutManager.Instance.UnlockDefaultWeapon();
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

        [ContextMenu("선택 패시브 스킬 해금")]
        public void UnlockTargetPassiveSkill()
        {
            if (targetPassiveSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetPassiveSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockPassiveSkill(targetPassiveSkill.SkillId);
        }

        [ContextMenu("선택 패시브 스킬 해금 되돌리기")]
        public void LockTargetPassiveSkill()
        {
            if (targetPassiveSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetPassiveSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.LockPassiveSkill(targetPassiveSkill.SkillId);
        }

        [ContextMenu("테스트 포인트 지급")]
        public void GrantDebugChallengePoints()
        {
            GameSaveManager.AddDebugChallengePoints(debugPointsToGrant);
            Debug.Log($"[DevUnlockTools] 챌린지 포인트 {debugPointsToGrant} 지급. 현재 보유: {GameSaveManager.ChallengePoints}");
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
            GameSaveManager.UnclearBoss(targetBoss.Id);
        }

        [ContextMenu("선택 총기 해금")]
        public void UnlockTargetWeapon()
        {
            if (targetWeapon == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetWeapon이 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockWeapon(targetWeapon.WeaponId);
        }

        [ContextMenu("선택 총기 해금 되돌리기")]
        public void LockTargetWeapon()
        {
            if (targetWeapon == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetWeapon이 비어있습니다.");
                return;
            }

            GameSaveManager.LockWeapon(targetWeapon.WeaponId);
        }

        private static bool IsStorySeen(StoryEpisodeId episodeId)
        {
            return GameSaveManager.HasSeenStoryEpisode(GetStorySaveId(episodeId));
        }

        private static void SetStorySeen(StoryEpisodeId episodeId, bool seen)
        {
            GameSaveManager.SetStoryEpisodeSeen(GetStorySaveId(episodeId), seen);
        }

        private void PullStoryTogglesFromSave()
        {
            prologueSeen = GameSaveManager.HasSeenPrologue;
            tutorialCompleted = GameSaveManager.HasCompletedTutorial;
            pastSeen = GameSaveManager.HasSeenPast;
            epilogueSeen = GameSaveManager.HasSeenEpilogue;
            act1Seen = IsStorySeen(StoryEpisodeId.Act1);
            lobbyTutorialBossSeen = IsStorySeen(StoryEpisodeId.LobbyTutorialBoss);
            act2Seen = IsStorySeen(StoryEpisodeId.Act2);
            act3Seen = IsStorySeen(StoryEpisodeId.Act3);
            finalBossAftermathSeen = IsStorySeen(StoryEpisodeId.FinalBossAftermath);
        }

        private void SetStoryProgress(
            bool synopsis,
            bool tutorial,
            bool act1,
            bool act2,
            bool act3,
            bool finalBossAftermath,
            bool epilogue)
        {
            GameSaveManager.SetPrologueSeen(synopsis);
            GameSaveManager.SetTutorialCompleted(tutorial);
            GameSaveManager.SetEpilogueSeen(epilogue);
            GameSaveManager.SetPastSeen(act1 || act2 || act3 || finalBossAftermath || epilogue);
            SetStorySeen(StoryEpisodeId.Act1, act1);
            SetStorySeen(StoryEpisodeId.LobbyTutorialBoss, act2 || act3 || finalBossAftermath || epilogue);
            SetStorySeen(StoryEpisodeId.Act2, act2);
            SetStorySeen(StoryEpisodeId.Act3, act3);
            SetStorySeen(StoryEpisodeId.FinalBossAftermath, finalBossAftermath);
            PullStoryTogglesFromSave();
        }

        private static string GetStorySaveId(StoryEpisodeId episodeId)
        {
            return episodeId.ToString();
        }
    }
}
