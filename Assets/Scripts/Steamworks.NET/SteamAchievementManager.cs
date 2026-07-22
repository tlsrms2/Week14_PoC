#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using UnityEngine;
using Week14.Challenge;
using Week14.Ending;
using Week14.Save;
using Week14.Skills;
using Week14.UI;
using Week14.Weapons;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

// 챌린지 완료를 Steam 업적으로 연결합니다. 보스별로 (1) 첫 챌린지 완료, (2) 중간점검 개수 도달,
// (3) 전체 챌린지 완료 시점에 ChallengeDatabaseSO(BossChallengeGroup)에 지정된 업적 API Name을 전송합니다.
public class SteamAchievementManager : MonoBehaviour
{
    [SerializeField] private ChallengeDatabaseSO database;
    [Tooltip("테스트/디버그용: 아래 '선택 보스 업적 초기화' 메뉴가 대상으로 삼을 보스입니다.")]
    [SerializeField] private BossData debugTargetBoss;

    [Header("모듈(총기/액티브/패시브) 구매 업적")]
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private PassiveSkillDatabase passiveSkillDatabase;
    [SerializeField] private WeaponDatabase weaponDatabase;
    [Tooltip("총기/액티브 스킬/패시브 스킬을 통틀어 처음 하나를 구매했을 때 전송할 Steam 업적 API Name입니다.")]
    [SerializeField] private string firstModulePurchaseAchievementId;
    [Tooltip("총기/액티브 스킬/패시브 스킬을 전부 구매했을 때 전송할 Steam 업적 API Name입니다.")]
    [SerializeField] private string allModulesPurchasedAchievementId;

    [Header("튜토리얼/엔딩 업적")]
    [Tooltip("튜토리얼을 완료했을 때 전송할 Steam 업적 API Name입니다.")]
    [SerializeField] private string tutorialCompleteAchievementId;
    [Tooltip("엔딩 크레딧이 표시됐을 때 전송할 Steam 업적 API Name입니다.")]
    [SerializeField] private string endingCreditsAchievementId;

    [Header("전체 도전과제 완료(메타) 업적")]
    [Tooltip("이 업적 자신을 제외한 나머지 모든 업적을 달성했을 때 전송할 Steam 업적 API Name입니다.")]
    [SerializeField] private string allAchievementsCompleteAchievementId;

#if !DISABLESTEAMWORKS
    private static SteamAchievementManager instance;

    public static SteamAchievementManager Instance => instance;

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

    private void OnEnable()
    {
        ChallengeManager.ChallengeCompleted += HandleChallengeCompleted;
        GameSaveManager.ItemPurchased += HandleItemPurchased;
        GameSaveManager.TutorialCompleted += HandleTutorialCompleted;
        EndingSceneController.CreditsShown += HandleCreditsShown;
    }

    private void OnDisable()
    {
        ChallengeManager.ChallengeCompleted -= HandleChallengeCompleted;
        GameSaveManager.ItemPurchased -= HandleItemPurchased;
        GameSaveManager.TutorialCompleted -= HandleTutorialCompleted;
        EndingSceneController.CreditsShown -= HandleCreditsShown;
    }

    // Steam 클라이언트가 게임 프로세스 시작 전에 스탯/업적을 이미 동기화해두므로
    // RequestCurrentStats() 없이 SteamManager.Initialized만 확인하면 됨(최신 Steamworks.NET에서 제거된 API).
    private void HandleChallengeCompleted(string bossId)
    {
        if (!SteamManager.Initialized || database == null)
        {
            return;
        }

        BossChallengeGroup group = database.GetGroup(bossId);
        if (group == null)
        {
            return;
        }

        int achieved = GameSaveManager.GetCompletedChallengeCount(bossId);

        if (achieved == 1)
        {
            TryUnlock(group.FirstClearAchievementId);
        }

        if (group.CheckpointChallengeCount > 0 && achieved == group.CheckpointChallengeCount)
        {
            TryUnlock(group.CheckpointAchievementId);
        }

        if (achieved == group.TotalChallengeCount)
        {
            TryUnlock(group.AllClearAchievementId);
        }
    }

    // 총기/액티브 스킬/패시브 스킬 구매는 전부 이 이벤트 하나로 통지됨(어떤 아이템인지는 구분하지 않음).
    private void HandleItemPurchased()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        int purchased = GameSaveManager.GetPurchasedModuleCount();
        int total = (skillDatabase != null ? skillDatabase.AllSkills.Count : 0)
            + (passiveSkillDatabase != null ? passiveSkillDatabase.AllSkills.Count : 0)
            + (weaponDatabase != null ? weaponDatabase.AllWeapons.Count : 0);

        if (purchased >= 1)
        {
            TryUnlock(firstModulePurchaseAchievementId);
        }

        if (total > 0 && purchased == total)
        {
            TryUnlock(allModulesPurchasedAchievementId);
        }
    }

    private void HandleTutorialCompleted()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        TryUnlock(tutorialCompleteAchievementId);
    }

    private void HandleCreditsShown()
    {
        if (!SteamManager.Initialized)
        {
            return;
        }

        TryUnlock(endingCreditsAchievementId);
    }

    private void TryUnlock(string achievementApiName)
    {
        if (string.IsNullOrEmpty(achievementApiName))
        {
            return;
        }

        if (SteamUserStats.GetAchievement(achievementApiName, out bool alreadyAchieved) && alreadyAchieved)
        {
            return;
        }

        SteamUserStats.SetAchievement(achievementApiName);
        SteamUserStats.StoreStats();

        // 방금 실제로 새 업적을 하나 달성시켰으니, 메타 업적(전체 도전과제 완료) 자신이 아니라면 나머지가 다 채워졌는지 재확인합니다.
        if (achievementApiName != allAchievementsCompleteAchievementId)
        {
            CheckAllAchievementsCompleted();
        }
    }

    // 메타 업적 자신을 제외한 나머지 모든 업적이 달성됐는지 확인하고, 그렇다면 메타 업적을 달성시킵니다.
    private void CheckAllAchievementsCompleted()
    {
        if (string.IsNullOrEmpty(allAchievementsCompleteAchievementId))
        {
            return;
        }

        foreach (string id in EnumerateTrackedAchievementIds())
        {
            if (string.IsNullOrEmpty(id) || id == allAchievementsCompleteAchievementId)
            {
                continue;
            }

            if (!SteamUserStats.GetAchievement(id, out bool achieved) || !achieved)
            {
                return;
            }
        }

        TryUnlock(allAchievementsCompleteAchievementId);
    }

    private IEnumerable<string> EnumerateTrackedAchievementIds()
    {
        if (database != null)
        {
            IReadOnlyList<BossChallengeGroup> groups = database.BossGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                yield return groups[i].FirstClearAchievementId;
                yield return groups[i].CheckpointAchievementId;
                yield return groups[i].AllClearAchievementId;
            }
        }

        yield return firstModulePurchaseAchievementId;
        yield return allModulesPurchasedAchievementId;
        yield return tutorialCompleteAchievementId;
        yield return endingCreditsAchievementId;
    }

    // 테스트/디버그용: 특정 보스에 등록된 업적 3종(첫 완료/중간점검/전체 클리어)을 Steam에서 미달성 상태로 되돌립니다.
    public void ClearBossAchievements(string bossId)
    {
        if (!SteamManager.Initialized || database == null)
        {
            return;
        }

        BossChallengeGroup group = database.GetGroup(bossId);
        if (group == null)
        {
            return;
        }

        ClearAchievementIfSet(group.FirstClearAchievementId);
        ClearAchievementIfSet(group.CheckpointAchievementId);
        ClearAchievementIfSet(group.AllClearAchievementId);
        SteamUserStats.StoreStats();
    }

    [ContextMenu("업적 초기화/선택 보스 (debugTargetBoss)")]
    private void ClearDebugTargetBossAchievements()
    {
        if (debugTargetBoss == null)
        {
            Debug.LogWarning("[SteamAchievementManager] debugTargetBoss가 비어있습니다.");
            return;
        }

        ClearBossAchievements(debugTargetBoss.Id);
        Debug.Log($"[SteamAchievementManager] {debugTargetBoss.Id} 보스의 Steam 업적을 초기화했습니다.");
    }

    // 테스트/디버그용: 데이터베이스에 등록된 모든 보스의 업적을 Steam에서 미달성 상태로 되돌립니다.
    [ContextMenu("업적 초기화/전체")]
    public void ClearAllAchievements()
    {
        if (!SteamManager.Initialized || database == null)
        {
            return;
        }

        IReadOnlyList<BossChallengeGroup> groups = database.BossGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            ClearAchievementIfSet(groups[i].FirstClearAchievementId);
            ClearAchievementIfSet(groups[i].CheckpointAchievementId);
            ClearAchievementIfSet(groups[i].AllClearAchievementId);
        }

        ClearAchievementIfSet(firstModulePurchaseAchievementId);
        ClearAchievementIfSet(allModulesPurchasedAchievementId);
        ClearAchievementIfSet(tutorialCompleteAchievementId);
        ClearAchievementIfSet(endingCreditsAchievementId);
        ClearAchievementIfSet(allAchievementsCompleteAchievementId);

        SteamUserStats.StoreStats();
    }

    private static void ClearAchievementIfSet(string achievementApiName)
    {
        if (!string.IsNullOrEmpty(achievementApiName))
        {
            SteamUserStats.ClearAchievement(achievementApiName);
        }
    }
#else
    public static SteamAchievementManager Instance => null;

    public void ClearBossAchievements(string bossId) { }

    public void ClearAllAchievements() { }
#endif
}
