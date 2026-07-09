using UnityEngine;
using UnityEngine.SceneManagement;
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
            Synopsis,
            Ending
        }

        private static GameFlowController instance;
        private static bool missingInstanceWarned;

        [Header("Scene Defaults")]
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";

        [Header("Cutscene")]
        [SerializeField] private string cutsceneSceneName = "CutsceneScene";
        [SerializeField] private CutsceneDefinition synopsisCutscene;
        [SerializeField] private CutsceneDefinition endingCutscene;

        private CutsceneDefinition pendingCutscene;
        private string pendingNextSceneName;
        private PendingCutsceneCompletion pendingCompletion;

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
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.EnterBossInternal(bossData);
            }
        }

        public static void RestartCurrentScene()
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(SceneManager.GetActiveScene().buildIndex);
            }
        }

        public static void ReturnToLobby(string fallbackLobbySceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(ResolveSceneName(fallbackLobbySceneName, controller.lobbySceneName));
            }
        }

        public static void ReturnToTitle(string fallbackTitleSceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(ResolveSceneName(fallbackTitleSceneName, controller.titleSceneName));
            }
        }

        public static void LoadScene(string sceneName)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(sceneName);
            }
        }

        public static void LoadScene(int buildIndex)
        {
            if (TryGetExistingInstance() is GameFlowController controller)
            {
                controller.LoadSceneInternal(buildIndex);
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
            if (synopsisCutscene != null && !GameSaveManager.HasSeenSynopsis)
            {
                PlayCutsceneThenLoadInternal(synopsisCutscene, lobbySceneName, PendingCutsceneCompletion.Synopsis);
                return;
            }

            LoadSceneInternal(lobbySceneName);
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
            PendingCutsceneCompletion completion)
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

            if (ShouldSuppressSceneTransition(completion))
            {
                LoadSceneDirectInternal(cutsceneSceneName);
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

            player.Play(pendingCutscene, CompletePendingCutscene);
            return true;
        }

        private void CompletePendingCutscene()
        {
            string nextSceneName = ResolveSceneName(pendingNextSceneName, lobbySceneName);
            PendingCutsceneCompletion completion = pendingCompletion;

            pendingCutscene = null;
            pendingNextSceneName = null;
            pendingCompletion = PendingCutsceneCompletion.None;

            switch (completion)
            {
                case PendingCutsceneCompletion.Synopsis:
                    GameSaveManager.MarkSynopsisSeen();
                    break;
                case PendingCutsceneCompletion.Ending:
                    GameSaveManager.MarkEndingSeen();
                    break;
            }

            if (ShouldSuppressSceneTransition(completion))
            {
                LoadSceneDirectInternal(nextSceneName);
                return;
            }

            LoadSceneInternal(nextSceneName);
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

        private void LoadSceneDirectInternal(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning($"{nameof(GameFlowController)}: sceneName is empty.");
                return;
            }

            PrepareSceneChange();
            SceneManager.LoadScene(sceneName);
        }

        private void LoadSceneInternal(int buildIndex)
        {
            PrepareSceneChange();
            SceneTransition.LoadScene(buildIndex);
        }

        private static bool ShouldSuppressSceneTransition(PendingCutsceneCompletion completion)
        {
            return completion == PendingCutsceneCompletion.Synopsis;
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
