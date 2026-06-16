using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Week14.Combat;
using Week14.Input;

namespace Week14.UI
{
    public sealed class HelpGameOverView : MonoBehaviour
    {
        private const float FocusMoveThreshold = 0.5f;

        [Header("Start")]
        [SerializeField] private StartControlSelect startControlSelect;

        [Header("Help")]
        [SerializeField] private GameObject helpRoot;
        [SerializeField] private TMP_Text helpBodyText;
        [SerializeField] private TMP_Text helpPageText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeHelpButton;
        [SerializeField] private GamepadSensitivitySettings gamepadSensitivitySettings;
        [SerializeField] private List<GameObject> helpPageImages = new();
        [TextArea(3, 8)]
        [SerializeField] private List<string> helpPages = new()
        {
            "이동: WASD / 왼쪽 스틱\n공격: 좌클릭 / 게임패드 공격 버튼\n패링: 우클릭 / 게임패드 패링 버튼",
            "탄환이 가득 차 있으면 패링할 수 없습니다.\n탄환이 남아 있으면 적 공격을 방어합니다.\n패링 실패 시 패링 조준 각도가 줄어듭니다.",
            "적의 탄환을 모두 깎으면 처형 가능 상태가 됩니다.\n처형 중에는 이동이 제한됩니다.\n처형 성공 시 탄환을 회복합니다."
        };

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button restartButton;

        private Health subscribedPlayerHealth;
        private int pageIndex;
        private float previousTimeScale = 1f;
        private bool startOpen = true;
        private bool helpOpen;
        private bool gameOverOpen;
        private bool sensitivityFocused;
        private bool focusMoveHeld;

        private void Awake()
        {
            CacheSceneReferences();
            BindButtons();
            SetHelpVisible(false);
            SetGameOverVisible(false);
            SetStartControlVisible(true);
        }

        private void OnEnable()
        {
            TrySubscribePlayer();
            if (startControlSelect != null)
            {
                startControlSelect.Selected += HandleStartControlSelected;
            }
        }

        private void OnDisable()
        {
            UnsubscribePlayer();
            if (startControlSelect != null)
            {
                startControlSelect.Selected -= HandleStartControlSelected;
            }

            if (startOpen || helpOpen || gameOverOpen)
            {
                UnfreezeGame();
            }

            startOpen = false;
            helpOpen = false;
            gameOverOpen = false;
            RefreshInputBlock();
        }

        private void Update()
        {
            TrySubscribePlayer();

            if (startOpen)
            {
                startControlSelect?.TickGamepadInput();
                return;
            }

            if (!gameOverOpen && GameInput.HelpDown)
            {
                ToggleHelp();
            }

            if (helpOpen)
            {
                TickHelpGamepadFocus();
                gamepadSensitivitySettings?.TickGamepadInput(sensitivityFocused);
            }
        }

        public void ToggleHelp()
        {
            SetHelpVisible(!helpOpen);
        }

        public void ShowPreviousPage()
        {
            pageIndex = Mathf.Max(0, pageIndex - 1);
            RefreshHelpPage();
        }

        public void ShowNextPage()
        {
            pageIndex = Mathf.Min(GetPageCount() - 1, pageIndex + 1);
            RefreshHelpPage();
        }

        public void CloseHelp()
        {
            SetHelpVisible(false);
        }

        public void RestartScene()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleStartControlSelected(GameplayControlMode mode)
        {
            GameInput.SelectControlMode(mode);
            SetStartControlVisible(false);
        }

        private void CacheSceneReferences()
        {
            helpRoot ??= FindGameObject("HelpRoot");
            gameOverRoot ??= FindGameObject("GameOverRoot");
            helpBodyText ??= FindComponent<TMP_Text>("HelpBodyText");
            helpPageText ??= FindComponent<TMP_Text>("HelpPageText");
            previousButton ??= FindComponent<Button>("PreviousButton");
            nextButton ??= FindComponent<Button>("NextButton");
            closeHelpButton ??= FindComponent<Button>("CloseHelpButton");
            restartButton ??= FindComponent<Button>("RestartButton");

            if (helpPageImages.Count == 0)
            {
                CacheHelpPageImages();
            }
        }

        private void CacheHelpPageImages()
        {
            Transform imageRoot = FindChildRecursive(transform, "HelpPageImages");
            if (imageRoot == null)
            {
                return;
            }

            foreach (Transform child in imageRoot)
            {
                helpPageImages.Add(child.gameObject);
            }
        }

        private void BindButtons()
        {
            previousButton?.onClick.AddListener(ShowPreviousPage);
            nextButton?.onClick.AddListener(ShowNextPage);
            closeHelpButton?.onClick.AddListener(CloseHelp);
            restartButton?.onClick.AddListener(RestartScene);
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
            SetHelpVisible(false);
            SetGameOverVisible(true);
        }

        private void SetStartControlVisible(bool visible)
        {
            startOpen = visible;
            startControlSelect?.SetVisible(visible);

            if (visible)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                FreezeGame();
            }
            else if (!helpOpen && !gameOverOpen)
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private void SetHelpVisible(bool visible)
        {
            helpOpen = visible;
            if (helpRoot != null)
            {
                helpRoot.SetActive(visible);
            }

            gamepadSensitivitySettings?.SetVisible(visible);

            if (visible)
            {
                pageIndex = Mathf.Clamp(pageIndex, 0, GetPageCount() - 1);
                sensitivityFocused = false;
                focusMoveHeld = false;
                RefreshHelpPage();
                FocusSensitivity();
                FreezeGame();
            }
            else if (!startOpen && !gameOverOpen)
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private void TickHelpGamepadFocus()
        {
            float vertical = GameInput.Move.y;
            bool moveUpDown = vertical >= FocusMoveThreshold && !focusMoveHeld;
            bool moveDownDown = vertical <= -FocusMoveThreshold && !focusMoveHeld;

            if (Mathf.Abs(vertical) < FocusMoveThreshold)
            {
                focusMoveHeld = false;
            }
            else
            {
                focusMoveHeld = true;
            }

            if (sensitivityFocused)
            {
                if (GameInput.UiUpDown || moveUpDown)
                {
                    sensitivityFocused = false;
                    FocusTopHelpButton();
                }

                return;
            }

            if (GameInput.UiDownDown || moveDownDown)
            {
                FocusSensitivity();
            }
        }

        private void FocusTopHelpButton()
        {
            Selectable target = closeHelpButton != null && closeHelpButton.interactable
                ? closeHelpButton
                : GetFirstInteractableTopButton();
            FocusSelectable(target);
        }

        private Selectable GetFirstInteractableTopButton()
        {
            if (previousButton != null && previousButton.interactable)
            {
                return previousButton;
            }

            if (nextButton != null && nextButton.interactable)
            {
                return nextButton;
            }

            return closeHelpButton;
        }

        private void FocusSensitivity()
        {
            Selectable target = gamepadSensitivitySettings != null ? gamepadSensitivitySettings.Selectable : null;
            if (target == null || !target.interactable)
            {
                return;
            }

            sensitivityFocused = true;
            FocusSelectable(target);
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
                sensitivityFocused = false;
                focusMoveHeld = false;
                FocusSelectable(restartButton);
                FreezeGame();
            }
            else if (!startOpen && !helpOpen)
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
        }

        private void RefreshHelpPage()
        {
            int pageCount = GetPageCount();
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);

            if (helpBodyText != null)
            {
                helpBodyText.text = helpPages.Count > 0 ? helpPages[pageIndex] : string.Empty;
            }

            if (helpPageText != null)
            {
                helpPageText.text = $"{pageIndex + 1} / {pageCount}";
            }

            for (int i = 0; i < helpPageImages.Count; i++)
            {
                if (helpPageImages[i] != null)
                {
                    helpPageImages[i].SetActive(i == pageIndex);
                }
            }

            if (previousButton != null)
            {
                previousButton.interactable = pageIndex > 0;
            }

            if (nextButton != null)
            {
                nextButton.interactable = pageIndex < pageCount - 1;
            }
        }

        private int GetPageCount()
        {
            return Mathf.Max(1, helpPages.Count);
        }

        private void RefreshInputBlock()
        {
            GameModalState.BlocksGameplayInput = startOpen || helpOpen || gameOverOpen;
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
