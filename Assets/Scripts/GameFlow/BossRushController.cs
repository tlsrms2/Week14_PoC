using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Audio;
using Week14.Combat;
using Week14.Enemy;
using Week14.Save;

namespace Week14.GameFlow
{
    public static class BossRushController
    {
        private enum RunState
        {
            Inactive,
            Running,
            Completed
        }

        private const int RequiredBossCount = 5;

        private static RunState state;
        private static string[] bossSceneNames = Array.Empty<string>();
        private static string[] bossIds = Array.Empty<string>();
        private static float[] currentRunBossSeconds = Array.Empty<float>();
        private static int currentBossIndex;
        private static float completedBossSeconds;
        private static BossAI activeBoss;
        private static bool eventsSubscribed;
        private static bool latestClearTimeWasNewRecord;
        private static string bossRushBgmId;
        private static float bossRushBgmFadeSeconds;
        private static bool debugCheatsEnabled;
        private static bool debugInvulnerabilityApplied;

        public static bool IsRunning => state == RunState.Running;
        public static bool HasRunContext => state != RunState.Inactive;
        public static bool ShouldUseShortBossIntro => state == RunState.Running;
        public static bool ShouldSuppressVictoryResult => state == RunState.Running;
        public static bool LatestClearTimeWasNewRecord => latestClearTimeWasNewRecord;
        public static bool IsDebugRun => state == RunState.Running && debugCheatsEnabled;
        public static bool DebugOneHitBosses => state == RunState.Running && debugCheatsEnabled;
        public static float ElapsedSeconds => completedBossSeconds
            + (state == RunState.Running && activeBoss != null ? activeBoss.CombatElapsedSeconds : 0f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            CancelRun();
        }

        public static bool StartRun(
            string[] sceneNames,
            string[] runBossIds,
            string bgmId,
            float bgmFadeSeconds,
            bool enableDebugCheats)
        {
            if (!TryCopySceneNames(sceneNames, out string[] copiedSceneNames))
            {
                return false;
            }

            if (!TryCopyBossIds(runBossIds, out string[] copiedBossIds))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(bgmId))
            {
                Debug.LogError($"{nameof(BossRushController)}: 보스러시 전용 BGM ID가 비어 있습니다.");
                return false;
            }

            bossSceneNames = copiedSceneNames;
            bossIds = copiedBossIds;
            currentRunBossSeconds = new float[RequiredBossCount];
            state = RunState.Running;
            currentBossIndex = 0;
            completedBossSeconds = 0f;
            activeBoss = null;
            latestClearTimeWasNewRecord = false;
            bossRushBgmId = bgmId.Trim();
            bossRushBgmFadeSeconds = Mathf.Max(0f, bgmFadeSeconds);
            debugCheatsEnabled = enableDebugCheats && Debug.isDebugBuild;
            SetDebugInvulnerability(debugCheatsEnabled);

            SubscribeEvents();
            EnsureBgmPlaying();
            GameFlowController.LoadScene(bossSceneNames[currentBossIndex]);
            return true;
        }

        public static bool RestartRun()
        {
            return bossSceneNames.Length == RequiredBossCount
                && StartRun(
                    bossSceneNames,
                    bossIds,
                    bossRushBgmId,
                    bossRushBgmFadeSeconds,
                    debugCheatsEnabled);
        }

        public static void CancelRun()
        {
            UnsubscribeEvents();
            SetDebugInvulnerability(false);
            state = RunState.Inactive;
            bossSceneNames = Array.Empty<string>();
            bossIds = Array.Empty<string>();
            currentRunBossSeconds = Array.Empty<float>();
            currentBossIndex = 0;
            completedBossSeconds = 0f;
            activeBoss = null;
            latestClearTimeWasNewRecord = false;
            bossRushBgmId = null;
            bossRushBgmFadeSeconds = 0f;
            debugCheatsEnabled = false;
        }

        public static void EnsureBgmPlaying()
        {
            if (IsRunning && !string.IsNullOrWhiteSpace(bossRushBgmId))
            {
                SoundManager.PlayBgm(bossRushBgmId, bossRushBgmFadeSeconds);
            }
        }

        public static string FormatTime(float seconds)
        {
            float safeSeconds = Mathf.Max(0f, seconds);
            int centiseconds = Mathf.FloorToInt(safeSeconds * 100f);
            int minutes = centiseconds / 6000;
            int remainingSeconds = centiseconds / 100 % 60;
            int remainingCentiseconds = centiseconds % 100;
            return $"{minutes:00}:{remainingSeconds:00}:{remainingCentiseconds:00}";
        }

        private static bool TryCopySceneNames(string[] sceneNames, out string[] copiedSceneNames)
        {
            copiedSceneNames = Array.Empty<string>();
            if (sceneNames == null || sceneNames.Length != RequiredBossCount)
            {
                Debug.LogError($"{nameof(BossRushController)}: 보스 씬을 정확히 {RequiredBossCount}개 지정해야 합니다.");
                return false;
            }

            copiedSceneNames = new string[RequiredBossCount];
            for (int i = 0; i < sceneNames.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(sceneNames[i]))
                {
                    Debug.LogError($"{nameof(BossRushController)}: {i + 1}번째 보스 씬 이름이 비어 있습니다.");
                    copiedSceneNames = Array.Empty<string>();
                    return false;
                }

                string sceneName = sceneNames[i].Trim();
                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogError($"{nameof(BossRushController)}: '{sceneName}' 씬이 Build Profiles의 Scene List에 없습니다.");
                    copiedSceneNames = Array.Empty<string>();
                    return false;
                }

                copiedSceneNames[i] = sceneName;
            }

            return true;
        }

        private static bool TryCopyBossIds(string[] runBossIds, out string[] copiedBossIds)
        {
            copiedBossIds = Array.Empty<string>();
            if (runBossIds == null || runBossIds.Length != RequiredBossCount)
            {
                Debug.LogError($"{nameof(BossRushController)}: 보스 ID를 정확히 {RequiredBossCount}개 지정해야 합니다.");
                return false;
            }

            copiedBossIds = new string[RequiredBossCount];
            for (int i = 0; i < runBossIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(runBossIds[i]))
                {
                    Debug.LogError($"{nameof(BossRushController)}: {i + 1}번째 보스 ID가 비어 있습니다.");
                    copiedBossIds = Array.Empty<string>();
                    return false;
                }

                copiedBossIds[i] = runBossIds[i].Trim();
            }

            return true;
        }

        private static void SubscribeEvents()
        {
            if (eventsSubscribed)
            {
                return;
            }

            BossAI.CombatStarted += HandleBossCombatStarted;
            BossAI.Defeated += HandleBossDefeated;
            eventsSubscribed = true;
        }

        private static void UnsubscribeEvents()
        {
            if (!eventsSubscribed)
            {
                return;
            }

            BossAI.CombatStarted -= HandleBossCombatStarted;
            BossAI.Defeated -= HandleBossDefeated;
            eventsSubscribed = false;
        }

        private static void HandleBossCombatStarted(BossAI boss)
        {
            if (state != RunState.Running
                || boss == null
                || boss.BossData == null
                || !IsExpectedBossScene())
            {
                return;
            }

            activeBoss = boss;
        }

        private static void HandleBossDefeated(BossAI boss)
        {
            if (state != RunState.Running || boss == null || boss != activeBoss)
            {
                return;
            }

            float bossClearSeconds = boss.CombatElapsedSeconds;
            completedBossSeconds += bossClearSeconds;
            currentRunBossSeconds[currentBossIndex] = bossClearSeconds;
            activeBoss = null;

            if (currentBossIndex < bossSceneNames.Length - 1)
            {
                currentBossIndex++;
                GameFlowController.LoadScene(bossSceneNames[currentBossIndex]);
                return;
            }

            state = RunState.Completed;
            SoundManager.StopBgm(0f);
            latestClearTimeWasNewRecord =
                GameSaveManager.TrySetBestBossRushTime(completedBossSeconds);
            GameSaveManager.TrySetBestBossRushBossTimes(bossIds, currentRunBossSeconds);
            SetDebugInvulnerability(false);
            UnsubscribeEvents();
        }

        private static void SetDebugInvulnerability(bool active)
        {
            if (debugInvulnerabilityApplied == active)
            {
                return;
            }

            debugInvulnerabilityApplied = active;
            if (active)
            {
                PlayerCombatController.PushExternalInvulnerability();
            }
            else
            {
                PlayerCombatController.PopExternalInvulnerability();
            }
        }

        private static bool IsExpectedBossScene()
        {
            return currentBossIndex >= 0
                && currentBossIndex < bossSceneNames.Length
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    bossSceneNames[currentBossIndex],
                    StringComparison.Ordinal);
        }
    }
}
