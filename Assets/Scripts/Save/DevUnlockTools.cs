using System.Collections.Generic;
using UnityEngine;
using Week14.Challenge;
using Week14.Combat;
using Week14.Enemy;
using Week14.Skills;
using Week14.Story;
using Week14.UI;
using Week14.Weapons;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Save
{
    [Tooltip("테스트용 개발자 기능입니다. GameSaveData가 저장하는 모든 항목을 즉시 조작/초기화합니다.")]
    public sealed class DevUnlockTools : MonoBehaviour
    {
        [Header("데이터베이스 참조")]
        [Tooltip("전체 해금 대상 스킬을 가져올 데이터베이스입니다.")]
        [SerializeField] private SkillDatabase skillDatabase;
        [Tooltip("전체 해금 대상 패시브 스킬을 가져올 데이터베이스입니다.")]
        [SerializeField] private PassiveSkillDatabase passiveSkillDatabase;
        [Tooltip("전체 해금/클리어 처리 대상 보스 목록입니다. 테스트하려는 보스 데이터를 등록하세요.")]
        [SerializeField] private List<BossData> bosses = new();
        [Tooltip("전체 해금 대상 총기를 가져올 데이터베이스입니다.")]
        [SerializeField] private WeaponDatabase weaponDatabase;
        [Tooltip("챌린지 전체 완료 처리/초기화 대상 챌린지를 가져올 데이터베이스입니다.")]
        [SerializeField] private ChallengeDatabaseSO challengeDatabase;

        [Header("핫키")]
        [Tooltip("플레이 중 이 키를 누르면 전체 세이브를 초기화합니다.")]
        [SerializeField] private bool enableResetEverythingHotkey = true;
        [Tooltip("플레이 중 이 키를 누르면 모든 스킬/보스/패시브/총기를 즉시 해금합니다.")]
        [SerializeField] private bool enableUnlockAllHotkey = true;
        [Tooltip("플레이 중 이 키를 누르면 모든 챌린지를 완료 처리합니다.")]
        [SerializeField] private bool enableCompleteAllChallengesHotkey = true;
        [Tooltip("플레이 중 이 키를 누르면 모든 챌린지 진행 상태(완료 + 카운터)를 초기화합니다.")]
        [SerializeField] private bool enableResetAllChallengesHotkey = true;
        [Tooltip("플레이 중 이 키를 누르면 챌린지 포인트를 debugPointsToGrant만큼 지급합니다.")]
        [SerializeField] private bool enableGrantChallengePointsHotkey = true;
        [Tooltip("플레이 중 이 키를 누르면 현재 씬의 보스 체력을 1로 만듭니다.")]
        [SerializeField] private bool enableSetBossHpToOneHotkey = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key resetEverythingHotkey = Key.F1;
        [SerializeField] private Key unlockAllHotkey = Key.F2;
        [SerializeField] private Key completeAllChallengesHotkey = Key.F3;
        [SerializeField] private Key resetAllChallengesHotkey = Key.F4;
        [SerializeField] private Key grantChallengePointsHotkey = Key.F5;
        [SerializeField] private Key setBossHpToOneHotkey = Key.F6;
#else
        [SerializeField] private KeyCode resetEverythingHotkey = KeyCode.F1;
        [SerializeField] private KeyCode unlockAllHotkey = KeyCode.F2;
        [SerializeField] private KeyCode completeAllChallengesHotkey = KeyCode.F3;
        [SerializeField] private KeyCode resetAllChallengesHotkey = KeyCode.F4;
        [SerializeField] private KeyCode grantChallengePointsHotkey = KeyCode.F5;
        [SerializeField] private KeyCode setBossHpToOneHotkey = KeyCode.F6;
#endif

        [Header("개별 대상 (선택 스킬/보스/총기/챌린지 기능이 사용)")]
        [Tooltip("아래 '선택 스킬' 관련 기능이 대상으로 삼을 스킬입니다.")]
        [SerializeField] private BaseSkillSO targetSkill;
        [Tooltip("아래 '선택 패시브 스킬' 관련 기능이 대상으로 삼을 패시브 스킬입니다.")]
        [SerializeField] private BasePassiveSkillSO targetPassiveSkill;
        [Tooltip("아래 '선택 보스' 관련 기능이 대상으로 삼을 보스입니다. '선택 챌린지' 기능도 이 보스 소속으로 취급합니다.")]
        [SerializeField] private BossData targetBoss;
        [Tooltip("아래 '선택 총기' 관련 기능이 대상으로 삼을 총기입니다.")]
        [SerializeField] private BaseWeaponSO targetWeapon;
        [Tooltip("아래 '선택 챌린지' 관련 기능이 대상으로 삼을 챌린지입니다. targetBoss에 속한 챌린지를 넣으세요.")]
        [SerializeField] private ChallengeDefinitionSO targetChallenge;

        [Header("챌린지 포인트")]
        [Tooltip("'테스트 포인트 지급'이 현재 포인트에 더할 양입니다.")]
        [SerializeField] private int debugPointsToGrant = 100;
        [Tooltip("'포인트 값으로 설정'이 정확히 맞출 포인트 값입니다.")]
        [SerializeField] private int debugPointsToSet;

        [Header("스토리 토글 (읽기 전용 표시값 - 버튼을 눌러야 갱신됨)")]
        [SerializeField] private bool prologueSeen;
        [SerializeField] private bool tutorialCompleted;
        [SerializeField] private bool pastSeen;
        [SerializeField] private bool act1Seen;
        [SerializeField] private bool lobbyTutorialBossSeen;
        [SerializeField] private bool lobbyTutorialSkillSeen;
        [SerializeField] private bool act2Seen;
        [SerializeField] private bool act3Seen;
        [SerializeField] private bool finalBossAftermathSeen;
        [SerializeField] private bool epilogueSeen;

        private static DevUnlockTools instance;

        // 씬을 넘어가도 살아남는 싱글턴입니다. 이미 살아있는 인스턴스가 있으면 자기 자신을 파괴해서,
        // 다음 씬에 원래부터 배치돼 있던 DevUnlockTools와 중복되어 핫키가 두 번씩 실행되는 것을 막습니다.
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
        }

        private void Update()
        {
            if (enableResetEverythingHotkey && WasHotkeyPressed(resetEverythingHotkey))
            {
                ResetEverything();
            }

            if (enableUnlockAllHotkey && WasHotkeyPressed(unlockAllHotkey))
            {
                UnlockAll();
            }

            if (enableCompleteAllChallengesHotkey && WasHotkeyPressed(completeAllChallengesHotkey))
            {
                CompleteAllChallenges();
            }

            if (enableResetAllChallengesHotkey && WasHotkeyPressed(resetAllChallengesHotkey))
            {
                ResetAllChallengeProgress();
            }

            if (enableGrantChallengePointsHotkey && WasHotkeyPressed(grantChallengePointsHotkey))
            {
                GrantDebugChallengePoints();
            }

            if (enableSetBossHpToOneHotkey && WasHotkeyPressed(setBossHpToOneHotkey))
            {
                SetCurrentBossHpToOne();
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

        // ---------------------------------------------------------------
        // 0. 전체 세이브 초기화
        // ---------------------------------------------------------------

        [ContextMenu("0. 전체 세이브 초기화 (완전 리셋)")]
        public void ResetEverything()
        {
            GameSaveManager.ResetEverything();
            WeaponLoadoutManager.Instance?.ReloadFromSave();
            SkillLoadoutManager.Instance?.ReloadFromSave();
            PassiveSkillLoadoutManager.Instance?.ReloadFromSave();
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 세이브 데이터를 전부 초기화했습니다(기본 해금 상태 + 기본 장착 상태로 복귀).");
        }

        // ---------------------------------------------------------------
        // 해금 전체 (스킬 + 패시브 + 보스 + 총기)
        // ---------------------------------------------------------------

        [ContextMenu("전체 해금 (스킬 + 패시브 + 보스 + 총기)")]
        public void UnlockAll()
        {
            UnlockAllSkills();
            UnlockAllPassiveSkills();
            UnlockAllBosses();
            UnlockAllWeapons();
            GameSaveManager.SetTutorialCompleted(true);
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 모든 스킬/패시브/보스/총기를 해금하고 튜토리얼을 스킵 처리했습니다.");
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

        // ---------------------------------------------------------------
        // 스토리 토글
        // ---------------------------------------------------------------

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
            SetStorySeen(StoryEpisodeId.LobbyTutorialSkill, true);
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 로비 보스/스킬 튜토리얼 완료 상태로 변경했습니다.");
        }

        [ContextMenu("스토리 토글/6-1. 로비 스킬 튜토리얼 시청 초기화")]
        public void ResetLobbyTutorialSkillSeen()
        {
            SetStorySeen(StoryEpisodeId.LobbyTutorialSkill, false);
            PullStoryTogglesFromSave();
            Debug.Log("[DevUnlockTools] 로비 스킬 튜토리얼 시청 상태를 초기화했습니다.");
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

        // ---------------------------------------------------------------
        // 스킬 / 패시브 스킬 / 보스 / 총기 해금
        // ---------------------------------------------------------------

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

            RefreshBossUi();
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
            RefreshBossUi();
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

            GameSaveManager.UnlockDefaultWeapons();
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

        [ContextMenu("선택 보스 해금")]
        public void UnlockTargetBoss()
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss가 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockBoss(targetBoss.Id);
            RefreshBossUi();
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
            RefreshBossUi();
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

        // ---------------------------------------------------------------
        // 보스 클리어 여부 / 최고 클리어 기록
        // ---------------------------------------------------------------

        [ContextMenu("보스 클리어/전체 클리어 처리")]
        public void ClearAllBosses()
        {
            foreach (BossData boss in bosses)
            {
                if (boss != null)
                {
                    GameSaveManager.ClearBoss(boss.Id);
                }
            }

            RefreshBossUi();
        }

        [ContextMenu("보스 클리어/전체 클리어 되돌리기")]
        public void UnclearAllBosses()
        {
            foreach (BossData boss in bosses)
            {
                if (boss != null)
                {
                    GameSaveManager.UnclearBoss(boss.Id);
                }
            }

            RefreshBossUi();
        }

        [ContextMenu("보스 클리어/전체 최고기록 초기화")]
        public void ResetAllBossClearTimes()
        {
            GameSaveManager.ResetAllBossClearTimes();
        }

        [ContextMenu("보스 클리어/선택 보스 클리어 처리")]
        public void ClearTargetBoss()
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss가 비어있습니다.");
                return;
            }

            GameSaveManager.ClearBoss(targetBoss.Id);
            RefreshBossUi();
        }

        [ContextMenu("보스 클리어/선택 보스 클리어 되돌리기")]
        public void UnclearTargetBoss()
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss가 비어있습니다.");
                return;
            }

            GameSaveManager.UnclearBoss(targetBoss.Id);
            RefreshBossUi();
        }

        [ContextMenu("보스 클리어/선택 보스 최고기록 초기화")]
        public void ResetTargetBossClearTime()
        {
            if (targetBoss == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss가 비어있습니다.");
                return;
            }

            GameSaveManager.ResetBossClearTime(targetBoss.Id);
        }

        // ---------------------------------------------------------------
        // 보스 체력 (디버그, 세이브와 무관 - 현재 플레이 중인 씬의 보스 인스턴스에만 적용)
        // ---------------------------------------------------------------

        [ContextMenu("현재 씬 보스 체력 1로 설정")]
        public void SetCurrentBossHpToOne()
        {
            BossAI boss = FindRealBossInScene();
            if (boss == null)
            {
                Debug.LogWarning("[DevUnlockTools] 씬에서 BossAI를 찾지 못했습니다.");
                return;
            }

            BulletGauge hpGauge = boss.HpGauge;
            if (hpGauge == null)
            {
                Debug.LogWarning("[DevUnlockTools] 보스의 HpGauge를 찾지 못했습니다.");
                return;
            }

            int amountToSpend = hpGauge.CurrentBullets - 1;
            if (amountToSpend > 0)
            {
                hpGauge.TrySpend(amountToSpend, BulletChangeSource.Generic);
            }

            Debug.Log($"[DevUnlockTools] 보스 체력을 1로 설정했습니다. (현재 {hpGauge.CurrentBullets}/{hpGauge.MaxBullets})");
        }

        // 해커 보스 3페이즈에서 소환되는 분신(HackerHologramBoss)도 BossAI를 상속하기 때문에, 그 시점부터는
        // 씬에 BossAI가 두 개 동시에 존재한다. FindFirstObjectByType<BossAI>()는 이럴 때 어느 쪽을 돌려줄지
        // 보장이 없어서 분신을 잡으면 진짜 보스가 아니라 UI에 안 묶인 분신의 체력 게이지를 조작하게 된다.
        // 그래서 후보들을 모아 분신 타입을 명시적으로 걸러내고 진짜 보스만 돌려준다.
        private static BossAI FindRealBossInScene()
        {
            BossAI[] candidates = FindObjectsByType<BossAI>(FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is not HackerHologramBoss)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        // ---------------------------------------------------------------
        // 구매 상태 (챌린지 포인트로 구매한 스킬/패시브/총기)
        // ---------------------------------------------------------------

        [ContextMenu("구매 상태/스킬 구매 기록 초기화")]
        public void ResetPurchasedSkills()
        {
            GameSaveManager.ResetPurchasedSkills();
        }

        [ContextMenu("구매 상태/패시브 스킬 구매 기록 초기화")]
        public void ResetPurchasedPassiveSkills()
        {
            GameSaveManager.ResetPurchasedPassiveSkills();
        }

        [ContextMenu("구매 상태/총기 구매 기록 초기화")]
        public void ResetPurchasedWeapons()
        {
            GameSaveManager.ResetPurchasedWeapons();
        }

        [ContextMenu("구매 상태/선택 스킬 무료 구매 처리")]
        public void PurchaseTargetSkillFree()
        {
            if (targetSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockSkill(targetSkill.SkillId);
            GameSaveManager.PurchaseSkill(targetSkill.SkillId, 0);
        }

        [ContextMenu("구매 상태/선택 패시브 스킬 무료 구매 처리")]
        public void PurchaseTargetPassiveSkillFree()
        {
            if (targetPassiveSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetPassiveSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockPassiveSkill(targetPassiveSkill.SkillId);
            GameSaveManager.PurchasePassiveSkill(targetPassiveSkill.SkillId, 0);
        }

        [ContextMenu("구매 상태/선택 총기 무료 구매 처리")]
        public void PurchaseTargetWeaponFree()
        {
            if (targetWeapon == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetWeapon이 비어있습니다.");
                return;
            }

            GameSaveManager.UnlockWeapon(targetWeapon.WeaponId);
            GameSaveManager.PurchaseWeapon(targetWeapon.WeaponId, 0);
        }

        // ---------------------------------------------------------------
        // 장착 상태 (로비 장착 슬롯)
        // ---------------------------------------------------------------

        [ContextMenu("장착 상태/장착 스킬 초기화")]
        public void ResetEquippedSkills()
        {
            GameSaveManager.ResetEquippedSkills();
        }

        [ContextMenu("장착 상태/장착 패시브 스킬 초기화")]
        public void ResetEquippedPassiveSkills()
        {
            GameSaveManager.ResetEquippedPassiveSkills();
        }

        [ContextMenu("장착 상태/장착 총기 초기화")]
        public void ResetEquippedWeapon()
        {
            GameSaveManager.ResetEquippedWeapon();
        }

        [ContextMenu("장착 상태/선택 스킬 장착")]
        public void EquipTargetSkill()
        {
            if (targetSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.SetEquippedSkillId((int)SkillSlot.Skill1, targetSkill.SkillId);
        }

        [ContextMenu("장착 상태/선택 패시브 스킬 장착")]
        public void EquipTargetPassiveSkill()
        {
            if (targetPassiveSkill == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetPassiveSkill이 비어있습니다.");
                return;
            }

            GameSaveManager.SetEquippedPassiveSkillId((int)PassiveSkillSlot.Passive1, targetPassiveSkill.SkillId);
        }

        [ContextMenu("장착 상태/선택 총기 장착")]
        public void EquipTargetWeapon()
        {
            if (targetWeapon == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetWeapon이 비어있습니다.");
                return;
            }

            GameSaveManager.SetEquippedWeaponId(targetWeapon.WeaponId);
        }

        // ---------------------------------------------------------------
        // 챌린지 완료 / 진행 상태
        // ---------------------------------------------------------------

        [ContextMenu("챌린지/전체 완료 처리")]
        public void CompleteAllChallenges()
        {
            if (challengeDatabase == null)
            {
                Debug.LogWarning("[DevUnlockTools] challengeDatabase가 비어있어 챌린지를 완료 처리할 수 없습니다.");
                return;
            }

            IReadOnlyList<BossChallengeGroup> groups = challengeDatabase.BossGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                BossChallengeGroup group = groups[i];
                if (group.Boss == null)
                {
                    continue;
                }

                IReadOnlyList<ChallengeDefinitionSO> challenges = group.Challenges;
                for (int j = 0; j < challenges.Count; j++)
                {
                    ChallengeDefinitionSO challenge = challenges[j];
                    if (challenge == null)
                    {
                        continue;
                    }

                    string saveKey = GameSaveManager.BuildChallengeSaveKey(group.Boss.Id, challenge.ChallengeId);
                    GameSaveManager.CompleteChallenge(saveKey, challenge.RewardPoint);
                }
            }

            Debug.Log("[DevUnlockTools] 모든 챌린지를 완료 처리했습니다.");
        }

        [ContextMenu("챌린지/전체 진행 초기화 (완료 + 카운터)")]
        public void ResetAllChallengeProgress()
        {
            GameSaveManager.ResetCompletedChallenges();
            GameSaveManager.ResetChallengeCounters();
            Debug.Log("[DevUnlockTools] 모든 챌린지 진행 상태를 초기화했습니다.");
        }

        [ContextMenu("챌린지/선택 챌린지 완료 처리")]
        public void CompleteTargetChallenge()
        {
            if (!TryGetTargetChallengeSaveKey(out string saveKey))
            {
                return;
            }

            GameSaveManager.CompleteChallenge(saveKey, targetChallenge.RewardPoint);
        }

        [ContextMenu("챌린지/선택 챌린지 초기화")]
        public void ResetTargetChallenge()
        {
            if (!TryGetTargetChallengeSaveKey(out string saveKey))
            {
                return;
            }

            GameSaveManager.ResetChallenge(saveKey);
        }

        private bool TryGetTargetChallengeSaveKey(out string saveKey)
        {
            saveKey = null;
            if (targetBoss == null || targetChallenge == null)
            {
                Debug.LogWarning("[DevUnlockTools] targetBoss/targetChallenge가 비어있습니다.");
                return false;
            }

            saveKey = GameSaveManager.BuildChallengeSaveKey(targetBoss.Id, targetChallenge.ChallengeId);
            return true;
        }

        // ---------------------------------------------------------------
        // 챌린지 포인트
        // ---------------------------------------------------------------

        [ContextMenu("챌린지 포인트/포인트 지급")]
        public void GrantDebugChallengePoints()
        {
            GameSaveManager.AddDebugChallengePoints(debugPointsToGrant);
            Debug.Log($"[DevUnlockTools] 챌린지 포인트 {debugPointsToGrant} 지급. 현재 보유: {GameSaveManager.ChallengePoints}");
        }

        [ContextMenu("챌린지 포인트/포인트 값으로 설정")]
        public void SetDebugChallengePoints()
        {
            GameSaveManager.SetDebugChallengePoints(debugPointsToSet);
            Debug.Log($"[DevUnlockTools] 챌린지 포인트를 {debugPointsToSet}(으)로 설정했습니다.");
        }

        [ContextMenu("챌린지 포인트/포인트 초기화 (0)")]
        public void ResetDebugChallengePoints()
        {
            GameSaveManager.SetDebugChallengePoints(0);
            Debug.Log("[DevUnlockTools] 챌린지 포인트를 0으로 초기화했습니다.");
        }

        // ---------------------------------------------------------------
        // 내부 헬퍼
        // ---------------------------------------------------------------

        // 잠긴 BossSlot/BossImage는 자기 자신을 SetActive(false)로 꺼버려서 OnEnable이 다시 돌 일이 없고,
        // GameSaveManager의 보스 해금/클리어 세터는 UI에 알림을 주지 않는다. 그래서 씬 리로드 없이 즉시
        // 반영하려면 디버그 툴이 씬에 있는(비활성 포함) 모든 슬롯을 직접 찾아 Refresh()를 호출해야 한다.
        private static void RefreshBossUi()
        {
            BossSlot[] slots = FindObjectsByType<BossSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Refresh();
            }

            BossImage[] images = FindObjectsByType<BossImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < images.Length; i++)
            {
                images[i].Refresh();
            }
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
            lobbyTutorialSkillSeen = IsStorySeen(StoryEpisodeId.LobbyTutorialSkill);
            act2Seen = IsStorySeen(StoryEpisodeId.Act2);
            act3Seen = IsStorySeen(StoryEpisodeId.Act3);
            finalBossAftermathSeen = IsStorySeen(StoryEpisodeId.FinalBossAftermath);
        }

        // act2/act3/finalBossAftermath/epilogue 중 하나라도 true면, 그 이전 단계인 로비 보스/스킬
        // 튜토리얼은 이미 봤다고 간주합니다(정상적인 플레이라면 그 시점 전에 로비를 거쳐야 하므로).
        private void SetStoryProgress(
            bool synopsis,
            bool tutorial,
            bool act1,
            bool act2,
            bool act3,
            bool finalBossAftermath,
            bool epilogue)
        {
            bool lobbyTutorialsSeen = act2 || act3 || finalBossAftermath || epilogue;

            GameSaveManager.SetPrologueSeen(synopsis);
            GameSaveManager.SetTutorialCompleted(tutorial);
            GameSaveManager.SetEpilogueSeen(epilogue);
            GameSaveManager.SetPastSeen(act1 || act2 || act3 || finalBossAftermath || epilogue);
            SetStorySeen(StoryEpisodeId.Act1, act1);
            SetStorySeen(StoryEpisodeId.LobbyTutorialBoss, lobbyTutorialsSeen);
            SetStorySeen(StoryEpisodeId.LobbyTutorialSkill, lobbyTutorialsSeen);
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
