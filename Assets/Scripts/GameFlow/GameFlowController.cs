using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Bootstrap;
using Week14.UI;

namespace Week14.GameFlow
{
    public sealed class GameFlowController : MonoBehaviour
    {
        private static GameFlowController instance;
        private static bool missingInstanceWarned;

        [Header("Scene Defaults")]
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";

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
