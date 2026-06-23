using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Bootstrap;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.UI
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject optionsPanelRoot;
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string lobbySceneName = "LobbyScene";

        private float previousTimeScale = 1f;
        private bool isPaused;

        private void Awake()
        {
            SetPaused(false);
        }

        private void OnDisable()
        {
            if (isPaused)
            {
                Unfreeze();
            }

            isPaused = false;
            GameModalState.BlocksGameplayInput = false;
        }

        private void Update()
        {
            if (EscapePressed())
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            SetPaused(!isPaused);
        }

        public void Resume()
        {
            SetPaused(false);
        }

        public void OpenOptions()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

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

        private void SetPaused(bool paused)
        {
            isPaused = paused;

            if (panelRoot != null)
            {
                panelRoot.SetActive(paused);
            }

            if (!paused)
            {
                CloseOptions();
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

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
