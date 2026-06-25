using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class HelpGameOverView : MonoBehaviour
    {
        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private TMP_Text gameOverTitleText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button titleButton;
        [SerializeField] private string titleSceneName = "TitleScene";
        [SerializeField] private string gameOverTitle = "GAME OVER";
        [SerializeField] private string victoryTitle = "VICTORY";

        private Health subscribedPlayerHealth;
        private float previousTimeScale = 1f;
        private bool gameOverOpen;

        private void Awake()
        {
            CacheSceneReferences();
            BindButtons();
            SetGameOverVisible(false);
        }

        private void OnEnable()
        {
            TrySubscribePlayer();
            BossAI.Defeated += HandleBossDefeated;
        }

        private void OnDisable()
        {
            BossAI.Defeated -= HandleBossDefeated;
            UnsubscribePlayer();

            if (gameOverOpen)
            {
                UnfreezeGame();
            }

            gameOverOpen = false;
            RefreshInputBlock();
        }

        private void Update()
        {
            TrySubscribePlayer();
        }

        public void RestartScene()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToTitle()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(titleSceneName);
        }

        private void CacheSceneReferences()
        {
            gameOverRoot ??= FindGameObject("GameOverRoot");
            gameOverTitleText ??= FindComponent<TMP_Text>("GameOverTitleText");
            restartButton ??= FindComponent<Button>("RestartButton");
            titleButton ??= FindComponent<Button>("TitleButton");
        }

        private void BindButtons()
        {
            restartButton?.onClick.AddListener(RestartScene);
            titleButton?.onClick.AddListener(GoToTitle);
        }

        private GameObject FindGameObject(string childName)
        {
            Transform child = FindChildRecursive(transform, childName);
            return child != null ? child.gameObject : null;
        }

        private T FindComponent<T>(string childName) where T : Component
        {
            Transform child = FindChildRecursive(transform, childName);
            return child != null ? child.GetComponent<T>() : null;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName)
                {
                    return child;
                }

                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private void TrySubscribePlayer()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            Health nextHealth = player != null ? player.Health : null;
            if (subscribedPlayerHealth == nextHealth)
            {
                return;
            }

            UnsubscribePlayer();
            subscribedPlayerHealth = nextHealth;
            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died += HandlePlayerDied;
            }
        }

        private void UnsubscribePlayer()
        {
            if (subscribedPlayerHealth == null)
            {
                return;
            }

            subscribedPlayerHealth.Died -= HandlePlayerDied;
            subscribedPlayerHealth = null;
        }

        private void HandlePlayerDied(Health _)
        {
            SoundManager.StopBgm();
            ShowGameOver(gameOverTitle);
        }

        private void HandleBossDefeated(BossAI _)
        {
            ShowGameOver(victoryTitle);
        }

        private void ShowGameOver(string title)
        {
            if (gameOverTitleText != null)
            {
                gameOverTitleText.text = title;
            }

            SetGameOverVisible(true);
        }

        private static void FocusSelectable(Selectable target)
        {
            if (target == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void SetGameOverVisible(bool visible)
        {
            gameOverOpen = visible;
            if (gameOverRoot != null)
            {
                gameOverRoot.SetActive(visible);
            }

            if (visible)
            {
                FocusSelectable(restartButton);
                FreezeGame();
            }
            else
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private void RefreshInputBlock()
        {
            GameModalState.BlocksGameplayInput = gameOverOpen;
        }

        private void FreezeGame()
        {
            if (Time.timeScale > 0f)
            {
                previousTimeScale = Time.timeScale;
            }

            Time.timeScale = 0f;
        }

        private void UnfreezeGame()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }
    }
}
