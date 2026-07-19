using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Cutscene;
using Week14.Save;
using Week14.UI;

namespace Week14.GameFlow
{
    public sealed class GameFlowController : MonoBehaviour
    {
        private enum PendingCutsceneCompletion
        {
            None,
            Prologue,
            Past,
            Ending
        }

        private static GameFlowController instance;
        private static bool missingInstanceWarned;
        private static bool bossRestartEntryPending;

        [Header("Scene Defaults")]
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private string tutorialSceneName = "TutorialScene";
        [SerializeField] private string endingSceneName = "EndingScene";

        [Header("Cutscene")]
        [SerializeField] private string cutsceneSceneName = "CutsceneScene";
        [FormerlySerializedAs("synopsisCutscene")]
        [SerializeField] private CutsceneDefinition prologueCutscene;
        [SerializeField] private CutsceneDefinition pastCutscene;
        [SerializeField] private CutsceneDefinition endingCutscene;

        private CutsceneDefinition pendingCutscene;
        private string pendingNextSceneName;
        private PendingCutsceneCompletion pendingCompletion;
        private bool? pendingSkippableOverride;
        private bool pendingStartsCovered;

        public static GameFlowController Instance => TryGetExistingInstance();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            missingInstanceWarned = false;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static void StartGame()
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.StartGameInternal();
            }
        }

        public static void EnterBoss(BossData bossData)
        {
            bossRestartEntryPending = false;
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.EnterBossInternal(bossData);
            }
        }

        public static void RestartCurrentScene()
        {
            SoundManager.StopBgm();
            bossRestartEntryPending = true;
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            SceneTransition.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static bool ConsumeBossRestartEntry()
        {
            bool wasRestart = bossRestartEntryPending;
            bossRestartEntryPending = false;
            return wasRestart;
        }

        public static void ReturnToLobby(string fallbackLobbySceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(ResolveSceneName(fallbackLobbySceneName, controller.lobbySceneName));
                return;
            }

            SceneTransition.LoadScene(fallbackLobbySceneName);
        }

        public static void ContinueAfterTutorial(string fallbackLobbySceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.ContinueAfterTutorialInternal(ResolveSceneName(fallbackLobbySceneName, controller.lobbySceneName));
                return;
            }

            SceneTransition.LoadScene(fallbackLobbySceneName);
        }

        public static void ReturnToTitle(string fallbackTitleSceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(ResolveSceneName(fallbackTitleSceneName, controller.titleSceneName));
                return;
            }

            SceneTransition.LoadScene(fallbackTitleSceneName);
        }

        public static void LoadScene(string sceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(sceneName);
                return;
            }

            SceneTransition.LoadScene(sceneName);
        }

        public static void LoadScene(int buildIndex)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(buildIndex);
                return;
            }

            SceneTransition.LoadScene(buildIndex);
        }

        public static void EnterEnding()
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(controller.endingSceneName);
            }
        }

        public static bool PlayPendingCutscene(CutscenePlayer player)
        {
            return TryGetExistingInstance() is GameFlowController controller
                && controller.PlayPendingCutsceneInternal(player);
        }

        public static void PlayEndingThenReturnToTitle()
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.PlayEndingThenLoadInternal(controller.titleSceneName);
            }
        }

        public static void PlayEndingThenReturnToLobby()
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.PlayEndingThenLoadInternal(controller.lobbySceneName);
            }
        }

        private static GameFlowController TryGetExistingInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<GameFlowController>();
            if (instance != null)
            {
                return instance;
            }

            if (!missingInstanceWarned)
            {
                missingInstanceWarned = true;
                Debug.LogWarning($"{nameof(GameFlowController)} instance is missing. Place it in the first loaded scene.");
            }

            return null;
        }

        private void StartGameInternal()
        {
            if (!GameSaveManager.HasCompletedTutorial)
            {
                if (prologueCutscene != null)
                {
                    PlayCutsceneThenLoadInternal(
                        prologueCutscene,
                        tutorialSceneName,
                        PendingCutsceneCompletion.Prologue);
                    return;
                }

                LoadSceneInternal(tutorialSceneName);
                return;
            }

            if (!GameSaveManager.HasSeenPast && pastCutscene != null)
            {
                PlayCutsceneThenLoadInternal(pastCutscene, lobbySceneName, PendingCutsceneCompletion.Past);
                return;
            }

            LoadSceneInternal(lobbySceneName);
        }

        private void ContinueAfterTutorialInternal(string nextLobbySceneName)
        {
            if (!GameSaveManager.HasSeenPast && pastCutscene != null)
            {
                PlayCutsceneThenLoadInternal(pastCutscene, nextLobbySceneName, PendingCutsceneCompletion.Past);
                return;
            }

            LoadSceneInternal(nextLobbySceneName);
        }

        private void EnterBossInternal(BossData bossData)
        {
            if (bossData == null)
            {
                return;
            }

            LoadSceneInternal(bossData.SceneName);
        }

        private void PlayEndingThenLoadInternal(string nextSceneName)
        {
            if (endingCutscene != null && !GameSaveManager.HasSeenEnding)
            {
                PlayCutsceneThenLoadInternal(endingCutscene, nextSceneName, PendingCutsceneCompletion.Ending);
                return;
            }

            LoadSceneInternal(nextSceneName);
        }

        private void PlayCutsceneThenLoadInternal(
            CutsceneDefinition cutscene,
            string nextSceneName,
            PendingCutsceneCompletion completion,
            bool? skippableOverride = null)
        {
            if (cutscene == null)
            {
                LoadSceneInternal(nextSceneName);
                return;
            }

            if (string.IsNullOrWhiteSpace(cutsceneSceneName))
            {
                Debug.LogWarning($"{nameof(GameFlowController)}: cutsceneSceneName is empty.");
                LoadSceneInternal(nextSceneName);
                return;
            }

            pendingCutscene = cutscene;
            pendingNextSceneName = nextSceneName;
            pendingCompletion = completion;
            pendingSkippableOverride = skippableOverride;
            pendingStartsCovered = completion == PendingCutsceneCompletion.Prologue
                || completion == PendingCutsceneCompletion.Past;

            if (pendingStartsCovered)
            {
                LoadSceneKeepingCoveredInternal(cutsceneSceneName);
                return;
            }

            LoadSceneInternal(cutsceneSceneName);
        }

        private bool PlayPendingCutsceneInternal(CutscenePlayer player)
        {
            if (pendingCutscene == null)
            {
                return false;
            }

            if (player == null)
            {
                Debug.LogWarning($"{nameof(GameFlowController)}: {nameof(CutscenePlayer)} is missing in cutscene scene.");
                CompletePendingCutscene();
                return true;
            }

            player.Play(
                pendingCutscene,
                CompletePendingCutscene,
                keepCoveredOnCompleted: true,
                skippableOverride: pendingSkippableOverride,
                startCovered: pendingStartsCovered);
            return true;
        }

        private void CompletePendingCutscene()
        {
            string nextSceneName = ResolveSceneName(pendingNextSceneName, lobbySceneName);
            PendingCutsceneCompletion completion = pendingCompletion;

            pendingCutscene = null;
            pendingNextSceneName = null;
            pendingCompletion = PendingCutsceneCompletion.None;
            pendingSkippableOverride = null;
            pendingStartsCovered = false;

            switch (completion)
            {
                case PendingCutsceneCompletion.Prologue:
                    GameSaveManager.MarkPrologueSeen();
                    break;
                case PendingCutsceneCompletion.Past:
                    GameSaveManager.MarkPastSeen();
                    break;
                case PendingCutsceneCompletion.Ending:
                    GameSaveManager.MarkEndingSeen();
                    break;
            }

            LoadSceneFromCoveredInternal(nextSceneName);
        }

        private void LoadSceneInternal(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"{nameof(GameFlowController)}: sceneName is empty.");
                return;
            }

            PrepareSceneChange();
            SceneTransition.LoadScene(sceneName);
        }

        private void LoadSceneFromCoveredInternal(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"{nameof(GameFlowController)}: sceneName is empty.");
                return;
            }

            PrepareSceneChange();
            SceneTransition.LoadSceneFromCovered(sceneName);
        }

        private void LoadSceneKeepingCoveredInternal(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"{nameof(GameFlowController)}: sceneName is empty.");
                return;
            }

            PrepareSceneChange();
            SceneTransition.LoadSceneAndKeepCovered(sceneName);
        }

        private void LoadSceneInternal(int buildIndex)
        {
            PrepareSceneChange();
            SceneTransition.LoadScene(buildIndex);
        }

        private static string ResolveSceneName(string preferredSceneName, string fallbackSceneName)
        {
            return !string.IsNullOrWhiteSpace(preferredSceneName)
                ? preferredSceneName
                : fallbackSceneName;
        }

        private static void PrepareSceneChange()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            GameModalState.BlocksGameplayInput = false;
        }
    }
}
