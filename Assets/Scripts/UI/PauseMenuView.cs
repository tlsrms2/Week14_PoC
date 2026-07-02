using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Bootstrap;

namespace Week14.UI
{
    public sealed class PauseMenuView : MonoBehaviour, IBackClosable
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject optionsPanelRoot;
        [SerializeField] private PixelBlockRevealView panelRevealView;
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";

        private float previousTimeScale = 1f;
        private bool isPaused;
        private bool isClosing;

        private void Awake()
        {
            CachePanelRevealView();
            SetPausedImmediate(false);
        }

        private void OnEnable()
        {
            UIBackStack.BackPressedWithoutTarget += HandleBackPressedWithoutTarget;
        }

        private void OnDisable()
        {
            UIBackStack.BackPressedWithoutTarget -= HandleBackPressedWithoutTarget;
            UIBackStack.Remove(this);

            if (isPaused)
            {
                Unfreeze();
            }

            isPaused = false;
            isClosing = false;
            GameModalState.BlocksGameplayInput = false;
        }

        public void TogglePause()
        {
            if (isClosing)
            {
                return;
            }

            if (!isPaused && GameModalState.BlocksGameplayInput)
            {
                return;
            }

            SetPaused(!isPaused);
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void OpenOptions()
        {
            if (optionsPanelRoot != null)
            {
                optionsPanelRoot.SetActive(true);
            }
        }

        public void CloseOptions()
        {
            if (optionsPanelRoot != null)
            {
                optionsPanelRoot.SetActive(false);
            }

            if (panelRoot != null && isPaused)
            {
                panelRoot.SetActive(true);
            }
        }

        public void RestartStage()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToLobby()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(lobbySceneName);
        }

        public void ReturnToTitle()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(titleSceneName);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public bool CloseByBack()
        {
            if (isClosing)
            {
                return true;
            }

            if (!isPaused)
            {
                return false;
            }

            if (optionsPanelRoot != null && optionsPanelRoot.activeSelf)
            {
                CloseOptions();
                return true;
            }

            ClosePauseMenu();
            return true;
        }

        private void SetPaused(bool paused)
        {
            if (paused)
            {
                OpenPauseMenu();
                return;
            }

            ClosePauseMenu();
        }

        private void OpenPauseMenu()
        {
            isPaused = true;
            isClosing = false;

            if (panelRoot != null && !panelRoot.activeSelf)
            {
                panelRoot.SetActive(true);
            }

            if (optionsPanelRoot != null)
            {
                optionsPanelRoot.SetActive(false);
            }

            Freeze();
            GameModalState.BlocksGameplayInput = true;
            UIBackStack.Push(this);
        }

        private void ClosePauseMenu()
        {
            if (isClosing)
            {
                return;
            }

            if (!isPaused)
            {
                return;
            }

            if (optionsPanelRoot != null)
            {
                optionsPanelRoot.SetActive(false);
            }

            CachePanelRevealView();

            if (panelRoot != null && panelRoot.activeInHierarchy && panelRevealView != null)
            {
                isClosing = true;
                GameModalState.BlocksGameplayInput = true;
                panelRevealView.PlayHide(CompleteClose);
                return;
            }

            CompleteClose();
        }

        private void CompleteClose()
        {
            isPaused = false;
            isClosing = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            Unfreeze();
            GameModalState.BlocksGameplayInput = false;
            UIBackStack.Remove(this);
        }

        private void SetPausedImmediate(bool paused)
        {
            isPaused = paused;
            isClosing = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(paused);
            }

            if (optionsPanelRoot != null)
            {
                optionsPanelRoot.SetActive(false);
            }

            if (paused)
            {
                Freeze();
            }
            else
            {
                Unfreeze();
            }

            GameModalState.BlocksGameplayInput = paused;
            if (paused)
            {
                UIBackStack.Push(this);
            }
            else
            {
                UIBackStack.Remove(this);
            }
        }

        private void HandleBackPressedWithoutTarget()
        {
            TogglePause();
        }

        private void CachePanelRevealView()
        {
            if (panelRevealView == null && panelRoot != null)
            {
                panelRevealView = panelRoot.GetComponent<PixelBlockRevealView>();
            }
        }

        private void Freeze()
        {
            if (Time.timeScale > 0f)
            {
                previousTimeScale = Time.timeScale;
            }

            Time.timeScale = 0f;
        }

        private void Unfreeze()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }

    }
}
