using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Challenge;
using Week14.Combat;
using Week14.Enemy;
using Week14.GameFlow;

namespace Week14.UI
{
    public sealed class GameResultView : MonoBehaviour
    {
        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button restartButton;
        [SerializeField] private BossChallengePanel gameOverChallengePanel;

        [FormerlySerializedAs("titleButton")]
        [SerializeField] private Button gameOverLobbyButton;

        [Header("Victory")]
        [SerializeField] private GameObject victoryRoot;
        [SerializeField] private Button victoryLobbyButton;
        [SerializeField] private BossChallengePanel victoryChallengePanel;

        [Header("Scene")]
        [FormerlySerializedAs("titleSceneName")]
        [SerializeField] private string lobbySceneName = "LobbyScene";

        [Header("Challenge Reward")]
        [Tooltip("챌린지 공개 연출이 끝난 뒤 이번 전투에서 획득한 포인트를 보여줄 팝업입니다.")]
        [SerializeField] private ChallengeRewardPopupView rewardPopupView;

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

            if (gameOverChallengePanel != null)
            {
                gameOverChallengePanel.RevealCompleted += HandleChallengeRevealCompleted;
            }

            if (victoryChallengePanel != null)
            {
                victoryChallengePanel.RevealCompleted += HandleChallengeRevealCompleted;
            }
        }

        private void OnDisable()
        {
            BossAI.Defeated -= HandleBossDefeated;
            UnsubscribePlayer();

            if (gameOverChallengePanel != null)
            {
                gameOverChallengePanel.RevealCompleted -= HandleChallengeRevealCompleted;
            }

            if (victoryChallengePanel != null)
            {
                victoryChallengePanel.RevealCompleted -= HandleChallengeRevealCompleted;
            }

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
            GameFlowController.RestartCurrentScene();
        }

        public void ReturnToLobby()
        {
            GameFlowController.ReturnToLobby(lobbySceneName);
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
            yield return PlayerDeathSequence.Play();
            ShowGameOver();
        }

        private void HandleBossDefeated(BossAI boss)
        {
            ShowVictory(boss);
        }

        private void HandleChallengeRevealCompleted()
        {
            if (rewardPopupView == null || ChallengeManager.Instance == null)
            {
                return;
            }

            rewardPopupView.Show(ChallengeManager.Instance.LastRunEarnedPoints);
        }

        private void ShowGameOver()
        {
            BossData bossData = FindCurrentBossData();
            gameOverChallengePanel?.PrepareReveal(bossData);

            ShowResult(gameOverRoot, restartButton);
            SyncChallengeReveal(gameOverChallengePanel, gameOverRoot);

            if (gameOverChallengePanel != null)
            {
                gameOverChallengePanel.PlayReveal(bossData);
            }
        }

        private static BossData FindCurrentBossData()
        {
            BossAI boss = FindFirstObjectByType<BossAI>();
            return boss != null ? boss.BossData : null;
        }

        private void ShowVictory(BossAI boss)
        {
            BossData bossData = boss != null ? boss.BossData : null;
            GameObject targetRoot = victoryRoot != null ? victoryRoot : gameOverRoot;
            victoryChallengePanel?.PrepareReveal(bossData);

            Selectable focusTarget = victoryLobbyButton != null ? victoryLobbyButton : gameOverLobbyButton;
            ShowResult(targetRoot, focusTarget);
            SyncChallengeReveal(victoryChallengePanel, targetRoot);

            RefreshVictorySummary(boss);
        }

        // PixelBlockRevealView.CacheContent()는 패널이 SetActive(true)될 때(OnEnable→Play())마다
        // contentGraphics 리스트를 통째로 비우고 위치 기반 타이밍을 새로 계산한다. 그래서 색 동기화는
        // ShowResult() 전에 해도 되지만(색 캐시는 Dictionary라 안 지워짐), 타이밍 동기화는 반드시
        // ShowResult()로 CacheContent()가 다시 실행된 뒤에 해야 한다 — 안 그러면 곧바로 덮어써진다.
        private static void SyncChallengeReveal(BossChallengePanel panel, GameObject root)
        {
            if (panel == null || root == null)
            {
                return;
            }

            PixelBlockRevealView[] revealViews = root.GetComponentsInChildren<PixelBlockRevealView>(true);
            for (int i = 0; i < revealViews.Length; i++)
            {
                panel.SyncColorsTo(revealViews[i]);
            }
        }

        private void RefreshVictorySummary(BossAI boss)
        {
            BossData bossData = boss != null ? boss.BossData : null;
            if (victoryChallengePanel != null)
            {
                victoryChallengePanel.PlayReveal(bossData);
            }
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
