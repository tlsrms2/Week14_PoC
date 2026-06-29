using System;
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

        private void OnDisable()
        {
            if (isPaused)
            {
                Unfreeze();
            }

            isPaused = false;
            isClosing = false;
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
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            CloseThenLoadScene(() => SceneTransition.LoadScene(buildIndex));
        }

        public void ReturnToLobby()
        {
            CloseThenLoadScene(() => SceneTransition.LoadScene(lobbySceneName));
        }

        public void ReturnToTitle()
        {
            CloseThenLoadScene(() => SceneTransition.LoadScene(titleSceneName));
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
            if (paused)
            {
                OpenPauseMenu();
                return;
            }

            ClosePauseMenu(null);
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
        }

        private void ClosePauseMenu(Action onClosed)
        {
            if (isClosing)
            {
                return;
            }

            if (!isPaused)
            {
                onClosed?.Invoke();
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
                panelRevealView.PlayHide(() => CompleteClose(onClosed));
                return;
            }

            CompleteClose(onClosed);
        }

        private void CompleteClose(Action onClosed)
        {
            isPaused = false;
            isClosing = false;

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }

            Unfreeze();
            GameModalState.BlocksGameplayInput = false;
            onClosed?.Invoke();
        }

        private void CloseThenLoadScene(Action loadScene)
        {
            ClosePauseMenu(() =>
            {
                Time.timeScale = 1f;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                GameModalState.BlocksGameplayInput = true;
                loadScene?.Invoke();
            });
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
