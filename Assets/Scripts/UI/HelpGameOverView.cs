using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Week14.Combat;
using Week14.Input;

namespace Week14.UI
{
    public sealed class HelpGameOverView : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode toggleHelpKey = KeyCode.F1;

        [Header("Help")]
        [SerializeField] private GameObject helpRoot;
        [SerializeField] private TMP_Text helpBodyText;
        [SerializeField] private TMP_Text helpPageText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeHelpButton;
        [SerializeField] private List<GameObject> helpPageImages = new();
        [TextArea(3, 8)]
        [SerializeField] private List<string> helpPages = new()
        {
            "이동: WASD / 방향키\n좌클릭: 왼손 권총 발사\n우클릭: 오른손 요격탄 발사",
            "적 탄이 대기 중일 때 요격하면 패링입니다.\n적 탄이 날아오는 중일 때 요격하면 방어입니다.\n우클릭 헛방 또는 방어 시 요격 각도가 줄어듭니다.",
            "적의 내구도를 모두 깎으면 처형 가능 상태가 됩니다.\n과열 중에는 행동이 제한됩니다.\n체력이 낮을수록 열기 회복 속도가 느려집니다."
        };

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private Button restartButton;

        private Health subscribedPlayerHealth;
        private int pageIndex;
        private float previousTimeScale = 1f;
        private bool helpOpen;
        private bool gameOverOpen;

        private void Awake()
        {
            CacheSceneReferences();
            BindButtons();
            SetHelpVisible(false);
            SetGameOverVisible(false);
        }

        private void OnEnable()
        {
            TrySubscribePlayer();
        }

        private void OnDisable()
        {
            UnsubscribePlayer();
            if (helpOpen || gameOverOpen)
            {
                UnfreezeGame();
            }

            helpOpen = false;
            gameOverOpen = false;
            RefreshInputBlock();
        }

        private void Update()
        {
            TrySubscribePlayer();

            if (!gameOverOpen && (GameInput.GetKeyDown(toggleHelpKey) || GameInput.GetKeyDown(KeyCode.H)))
            {
                ToggleHelp();
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

        private void SetHelpVisible(bool visible)
        {
            helpOpen = visible;
            if (helpRoot != null)
            {
                helpRoot.SetActive(visible);
            }

            if (visible)
            {
                pageIndex = Mathf.Clamp(pageIndex, 0, GetPageCount() - 1);
                RefreshHelpPage();
                FreezeGame();
            }
            else if (!gameOverOpen)
            {
                UnfreezeGame();
            }

            RefreshInputBlock();
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
                FreezeGame();
            }
            else if (!helpOpen)
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
            GameModalState.BlocksGameplayInput = helpOpen || gameOverOpen;
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
