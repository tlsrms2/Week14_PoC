using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Enemy;
using Week14.Weapons;

namespace Week14.UI
{
    public sealed class GameResultView : MonoBehaviour
    {
        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button restartButton;

        [FormerlySerializedAs("titleButton")]
        [SerializeField] private Button gameOverLobbyButton;

        [Header("Victory")]
        [SerializeField] private GameObject victoryRoot;
        [SerializeField] private Button victoryLobbyButton;
        [SerializeField] private TMP_Text equippedWeaponText;
        [SerializeField] private Image equippedWeaponIcon;
        [SerializeField] private TMP_Text defeatedBossText;
        [SerializeField] private Image defeatedBossPortrait;

        [Header("Scene")]
        [FormerlySerializedAs("titleSceneName")]
        [SerializeField] private string lobbySceneName = "LobbyScene";

        private Health subscribedPlayerHealth;
        private float previousTimeScale = 1f;
        private bool resultOpen;

        private void Awake()
        {
            CacheSceneReferences();
            BindButtons();
            SetResultVisible(false);
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

            if (resultOpen)
            {
                UnfreezeGame();
            }

            resultOpen = false;
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

        public void ReturnToLobby()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneTransition.LoadScene(lobbySceneName);
        }

        private void CacheSceneReferences()
        {
            gameOverRoot ??= FindGameObject("GameOverRoot");
            victoryRoot ??= FindGameObject("VictoryRoot");
            restartButton ??= FindComponent<Button>("RestartButton");
            gameOverLobbyButton ??= FindComponentIn<Button>(gameOverRoot, "LobbyButton")
                ?? FindComponent<Button>("GameOverLobbyButton");
            victoryLobbyButton ??= FindComponentIn<Button>(victoryRoot, "LobbyButton")
                ?? FindComponent<Button>("VictoryLobbyButton");
            equippedWeaponText ??= FindComponentIn<TMP_Text>(victoryRoot, "EquippedWeaponText")
                ?? FindComponentIn<TMP_Text>(victoryRoot, "VictoryWeaponText");
            equippedWeaponIcon ??= FindComponentIn<Image>(victoryRoot, "EquippedWeaponIcon")
                ?? FindComponentIn<Image>(victoryRoot, "VictoryWeaponIcon");
            defeatedBossText ??= FindComponentIn<TMP_Text>(victoryRoot, "DefeatedBossText")
                ?? FindComponentIn<TMP_Text>(victoryRoot, "VictoryBossText");
            defeatedBossPortrait ??= FindComponentIn<Image>(victoryRoot, "DefeatedBossPortrait")
                ?? FindComponentIn<Image>(victoryRoot, "VictoryBossPortrait");
        }

        private void BindButtons()
        {
            restartButton?.onClick.AddListener(RestartScene);
            gameOverLobbyButton?.onClick.AddListener(ReturnToLobby);
            if (victoryLobbyButton != gameOverLobbyButton)
            {
                victoryLobbyButton?.onClick.AddListener(ReturnToLobby);
            }
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

        private T FindComponentIn<T>(GameObject root, string childName) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            Transform child = FindChildRecursive(root.transform, childName);
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
            StartCoroutine(PlayPlayerDeathThenShowGameOver());
        }

        private IEnumerator PlayPlayerDeathThenShowGameOver()
        {
            SoundManager.StopBgm();

            PlayerCombatController player = PlayerCombatController.Active;
            CameraFollow2D playerCamera = player?.CameraFollow;
            PlayerCombatConfig config = player?.Config;
            Transform deathFocusTarget = player != null
                ? (player.BodyRoot != null ? player.BodyRoot : player.transform)
                : null;

            if (playerCamera != null && deathFocusTarget != null)
            {
                playerCamera.BeginCinematicFocus(
                    deathFocusTarget,
                    config != null ? config.DeathCameraFocusWeight : 1f,
                    config != null ? config.DeathCameraZoomMultiplier : 0.7f);
            }

            // 카메라 줌인이 끝날 때까지(정상 시간으로) 기다린 뒤 적/투사체를 멈춥니다. 줌인이 끝나지 않아도 최대 대기 시간을 넘기면 진행합니다.
            float maxWorldFreezeDelaySeconds = config != null ? config.DeathWorldFreezeDelaySeconds : 0.3f;
            float zoomWaitElapsed = 0f;
            while (playerCamera != null
                && !playerCamera.IsCinematicZoomSettled()
                && zoomWaitElapsed < maxWorldFreezeDelaySeconds)
            {
                zoomWaitElapsed += Time.deltaTime;
                yield return null;
            }

            float previousTimeScaleBeforeDeath = Time.timeScale;
            Time.timeScale = 0f;

            PlayerVisualRig visual = player?.Visual;
            if (visual != null)
            {
                // deathAnimator는 UnscaledTime으로 갱신되므로 Time.timeScale=0이어도 정상 재생됩니다.
                visual.PlayDeath();
                float deathAnimationSeconds = config != null ? config.DeathAnimationSeconds : 1.5333333f;
                if (deathAnimationSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(deathAnimationSeconds);
                }
            }

            Time.timeScale = previousTimeScaleBeforeDeath;

            playerCamera?.EndCinematicFocus();
            ShowGameOver();
        }

        private void HandleBossDefeated(BossAI boss)
        {
            ShowVictory(boss);
        }

        private void ShowGameOver()
        {
            ShowResult(gameOverRoot, restartButton);
        }

        private void ShowVictory(BossAI boss)
        {
            RefreshVictorySummary(boss);

            GameObject targetRoot = victoryRoot != null ? victoryRoot : gameOverRoot;
            Selectable focusTarget = victoryLobbyButton != null ? victoryLobbyButton : gameOverLobbyButton;
            ShowResult(targetRoot, focusTarget);
        }

        private void RefreshVictorySummary(BossAI boss)
        {
            BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null
                ? WeaponLoadoutManager.Instance.CurrentWeapon
                : null;
            BossData bossData = boss != null ? boss.BossData : null;

            SetText(equippedWeaponText, weapon != null ? weapon.DisplayName : string.Empty);
            SetImage(equippedWeaponIcon, weapon != null ? weapon.Icon : null);

            string bossName = bossData != null && !string.IsNullOrWhiteSpace(bossData.BossName)
                ? bossData.BossName
                : boss != null ? boss.DisplayName : string.Empty;
            SetText(defeatedBossText, bossName);
            SetImage(defeatedBossPortrait, bossData != null ? bossData.Icon : null);
        }

        private void ShowResult(GameObject targetRoot, Selectable focusTarget)
        {
            SetResultVisible(true, targetRoot, focusTarget);
        }

        private static void FocusSelectable(Selectable target)
        {
            if (target == null || EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void SetResultVisible(bool visible)
        {
            SetResultVisible(visible, null, null);
        }

        private void SetResultVisible(bool visible, GameObject activeRoot, Selectable focusTarget)
        {
            resultOpen = visible;
            if (visible && activeRoot == null)
            {
                activeRoot = gameOverRoot;
            }

            SetRootVisible(gameOverRoot, visible && activeRoot == gameOverRoot);
            SetRootVisible(victoryRoot, visible && activeRoot == victoryRoot);

            if (visible)
            {
                FocusSelectable(focusTarget);
                FreezeGame();
            }
            else
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private static void SetRootVisible(GameObject root, bool visible)
        {
            if (root != null)
            {
                root.SetActive(visible);
            }
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static void SetImage(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void RefreshInputBlock()
        {
            GameModalState.BlocksGameplayInput = resultOpen;
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
