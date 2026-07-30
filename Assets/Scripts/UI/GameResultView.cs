using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Challenge;
using Week14.Combat;
using Week14.Enemy;
using Week14.GameFlow;
using Week14.Save;

namespace Week14.UI
{
    public sealed class GameResultView : MonoBehaviour
    {
        private const string NewRecordPrefix = "<color=#FFD83D>NEW! </color>";

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button restartButton;
        [SerializeField] private BossChallengePanel gameOverChallengePanel;
        [SerializeField] private TMP_Text gameOverElapsedTimeText;

        [FormerlySerializedAs("titleButton")]
        [SerializeField] private Button gameOverLobbyButton;

        [Header("Victory")]
        [SerializeField] private GameObject victoryRoot;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button victoryLobbyButton;
        [SerializeField] private BossChallengePanel victoryChallengePanel;
        [SerializeField] private TMP_Text victoryElapsedTimeText;

        [Header("Scene")]
        [FormerlySerializedAs("titleSceneName")]
        [SerializeField] private string lobbySceneName = "LobbyScene";

        [Header("Challenge Reward")]
        [Tooltip("챌린지 공개 연출이 끝난 뒤 이번 전투에서 획득한 포인트를 보여줄 팝업입니다.")]
        [SerializeField] private ChallengeRewardPopupView rewardPopupView;

        private Health subscribedPlayerHealth;
        private float previousTimeScale = 1f;
        private bool resultOpen;
        private Selectable pendingFocusTarget;
        private bool victoryIsFinalBoss;
        private BossAI cachedBoss;
        private int resultOpenedFrame = -1;
        private GameObject activeResultRoot;
        private BossChallengePanel activeChallengePanel;
        private Coroutine buttonInputGateRoutine;

        private void Awake()
        {
            // 씬 로드 직후, 3페이즈 진입 시 생성되는 Hacker 홀로그램(BossAI 서브클래스) 같은 보조
            // BossAI 인스턴스가 아직 없을 때 한 번만 찾아서 캐싱한다. FindCurrentBoss()를 매번
            // 다시 호출하면 씬에 BossAI가 2개 이상일 때 어느 걸 돌려줄지 순서 보장이 없어서,
            // 홀로그램(BossData가 비어있음)을 잘못 집어 챌린지 패널이 빈 채로 뜨는 문제가 있었다.
            cachedBoss = FindFirstObjectByType<BossAI>();
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

            StopButtonInputGate();
            SetActiveResultButtonsInteractable(true);
            activeResultRoot = null;
            activeChallengePanel = null;
            resultOpen = false;
            RefreshInputBlock();
        }

        private void Update()
        {
            TrySubscribePlayer();
            TrySkipChallengeReveal();
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
            victoryRestartButton ??= FindComponentIn<Button>(victoryRoot, "RestartButton");
            victoryLobbyButton ??= FindComponentIn<Button>(victoryRoot, "LobbyButton")
                ?? FindComponent<Button>("VictoryLobbyButton");
        }

        private void BindButtons()
        {
            restartButton?.onClick.AddListener(RestartScene);
            gameOverLobbyButton?.onClick.AddListener(ReturnToLobby);
            victoryRestartButton?.onClick.AddListener(RestartScene);
            victoryLobbyButton?.onClick.AddListener(HandleVictoryLobbyButtonClicked);
        }

        // 최종보스를 처치한 승리 화면에서는, 엔딩을 아직 안 봤을 때만 로비 대신 엔딩(EndingScene)으로 진입합니다.
        // 이미 본 적 있으면(예: 재플레이) 평소처럼 로비로 보냅니다.
        private void HandleVictoryLobbyButtonClicked()
        {
            if (victoryIsFinalBoss && !GameSaveManager.HasSeenEnding)
            {
                GameFlowController.EnterEnding();
                return;
            }

            ReturnToLobby();
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
            FindCurrentBoss()?.FreezeCombatTimer();
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
            if (rewardPopupView != null && ChallengeManager.Instance != null)
            {
                rewardPopupView.Show(ChallengeManager.Instance.LastRunEarnedPoints);
            }

            RevealResultButtons();
        }

        private void ShowGameOver()
        {
            BossAI boss = FindCurrentBoss();
            BossData bossData = boss != null ? boss.BossData : null;
            gameOverChallengePanel?.PrepareReveal(bossData);

            HideResultButtonsFor(gameOverRoot);
            ShowResult(gameOverRoot, restartButton, gameOverChallengePanel);
            PlayResultSfx();
            SyncChallengeReveal(gameOverChallengePanel, gameOverRoot);
            SetElapsedTimeText(gameOverElapsedTimeText, boss);

            if (gameOverChallengePanel != null)
            {
                gameOverChallengePanel.PlayReveal(bossData);
            }
            else
            {
                RevealResultButtons();
            }
        }

        private BossAI FindCurrentBoss()
        {
            return cachedBoss;
        }

        private static void SetElapsedTimeText(TMP_Text text, BossAI boss, bool showNewRecordPrefix = false)
        {
            if (text == null)
            {
                return;
            }

            if (boss == null)
            {
                text.text = "--:--:--";
                return;
            }

            string formattedTime = BossAI.FormatCombatTime(boss.CombatElapsedSeconds);
            text.text = showNewRecordPrefix && boss.LatestClearTimeWasNewRecord
                ? NewRecordPrefix + formattedTime
                : formattedTime;
        }

        private void ShowVictory(BossAI boss)
        {
            BossData bossData = boss != null ? boss.BossData : null;
            victoryIsFinalBoss = bossData != null && bossData.IsFinalBoss;
            GameObject targetRoot = victoryRoot != null ? victoryRoot : gameOverRoot;
            victoryChallengePanel?.PrepareReveal(bossData);

            Selectable focusTarget = victoryRestartButton != null
                ? victoryRestartButton
                : victoryLobbyButton != null
                    ? victoryLobbyButton
                    : gameOverLobbyButton;
            HideResultButtonsFor(targetRoot);
            ShowResult(targetRoot, focusTarget, victoryChallengePanel);
            PlayResultSfx();
            SyncChallengeReveal(victoryChallengePanel, targetRoot);
            SetElapsedTimeText(victoryElapsedTimeText, boss, true);

            RefreshVictorySummary(boss);
        }

        private static void PlayResultSfx()
        {
            SoundManager.PlaySfx(SoundEvent.UI_ResultPanelPopup);
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
            else
            {
                RevealResultButtons();
            }
        }

        private void ShowResult(
            GameObject targetRoot,
            Selectable focusTarget,
            BossChallengePanel challengePanel)
        {
            activeResultRoot = targetRoot;
            activeChallengePanel = challengePanel;
            pendingFocusTarget = focusTarget;
            SetResultVisible(true, targetRoot);
        }

        private void HideResultButtonsFor(GameObject targetRoot)
        {
            if (targetRoot == gameOverRoot)
            {
                SetButtonsActive(false, restartButton, gameOverLobbyButton);
            }

            if (targetRoot == victoryRoot)
            {
                SetButtonsActive(false, victoryRestartButton, victoryLobbyButton);
            }
        }

        // 챌린지 공개 연출이 끝난 뒤(연출 패널이 없으면 결과 화면이 뜨자마자) 재시작/로비 버튼을 등장시킵니다.
        private void RevealResultButtons()
        {
            if (gameOverRoot != null && gameOverRoot.activeSelf)
            {
                SetButtonsActive(true, restartButton, gameOverLobbyButton);
            }

            if (victoryRoot != null && victoryRoot.activeSelf)
            {
                SetButtonsActive(true, victoryRestartButton, victoryLobbyButton);
            }

            FocusSelectable(pendingFocusTarget);
            pendingFocusTarget = null;
        }

        private static void SetButtonsActive(bool active, params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].gameObject.SetActive(active);
                }
            }
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
            SetResultVisible(visible, null);
        }

        private void SetResultVisible(bool visible, GameObject activeRoot)
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
                resultOpenedFrame = Time.frameCount;
                FreezeGame();
            }
            else
            {
                StopButtonInputGate();
                SetActiveResultButtonsInteractable(true);
                activeResultRoot = null;
                activeChallengePanel = null;
                pendingFocusTarget = null;
                resultOpenedFrame = -1;
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private void TrySkipChallengeReveal()
        {
            // 보스를 쓰러뜨린 공격 클릭이 같은 프레임에 결과 연출까지 건너뛰지 않도록 다음 프레임부터 받습니다.
            if (!resultOpen || Time.frameCount <= resultOpenedFrame || !WasLeftClickPressedThisFrame())
            {
                return;
            }

            if (activeChallengePanel == null || !activeChallengePanel.IsRevealPlaying)
            {
                return;
            }

            activeChallengePanel.CompleteRevealImmediately();
            SyncChallengeReveal(activeChallengePanel, activeResultRoot);
            ShowPixelRevealsImmediate(activeResultRoot);
            GateResultButtonsUntilClickReleased();
        }

        private static bool WasLeftClickPressedThisFrame()
        {
            return Mouse.current?.leftButton.wasPressedThisFrame == true;
        }

        private static void ShowPixelRevealsImmediate(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            PixelBlockRevealView[] revealViews = root.GetComponentsInChildren<PixelBlockRevealView>(true);
            for (int i = 0; i < revealViews.Length; i++)
            {
                revealViews[i].ShowImmediate();
            }
        }

        private void GateResultButtonsUntilClickReleased()
        {
            StopButtonInputGate();
            SetActiveResultButtonsInteractable(false);
            buttonInputGateRoutine = StartCoroutine(EnableResultButtonsAfterClickReleased());
        }

        private IEnumerator EnableResultButtonsAfterClickReleased()
        {
            // 현재 스킵 클릭의 PointerDown/PointerUp이 방금 나타난 버튼 클릭으로 이어지지 않게
            // 최소 한 프레임을 넘기고, 버튼을 뗀 뒤에 상호작용을 허용합니다.
            do
            {
                yield return null;
            }
            while (Mouse.current?.leftButton.isPressed == true);

            SetActiveResultButtonsInteractable(true);
            buttonInputGateRoutine = null;
        }

        private void StopButtonInputGate()
        {
            if (buttonInputGateRoutine == null)
            {
                return;
            }

            StopCoroutine(buttonInputGateRoutine);
            buttonInputGateRoutine = null;
        }

        private void SetActiveResultButtonsInteractable(bool interactable)
        {
            if (activeResultRoot != null && activeResultRoot == victoryRoot)
            {
                SetButtonsInteractable(interactable, victoryRestartButton, victoryLobbyButton);
                return;
            }

            if (activeResultRoot != null && activeResultRoot == gameOverRoot)
            {
                SetButtonsInteractable(interactable, restartButton, gameOverLobbyButton);
            }
        }

        private static void SetButtonsInteractable(bool interactable, params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].interactable = interactable;
                }
            }
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
