using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Week14.Bootstrap
{
    public sealed class SceneTransition : MonoBehaviour
    {
        private static SceneTransition instance;

        [Header("Block Wipe")]
        [SerializeField, Min(1)] private int columns = 16;
        [SerializeField, Min(1)] private int rows = 10;
        [SerializeField, Min(0.05f)] private float coverDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float revealDuration = 0.65f;
        [SerializeField, Min(0f)] private float holdDuration = 0.08f;
        [SerializeField, Range(0f, 0.9f)] private float staggerRatio = 0.35f;
        [SerializeField] private Color blockColor = Color.black;
        [SerializeField] private int sortingOrder = 32760;
        [SerializeField, Min(0f)] private float blockOverlapPixels = 2f;

        private readonly List<BlockEntry> blocks = new();
        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform blockRoot;
        private Coroutine loadRoutine;
        private int builtColumns;
        private int builtRows;
        private Vector2 builtScreenSize;

        public static SceneTransition Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
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

        public static void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("SceneTransition.LoadScene: sceneName is empty.");
                return;
            }

            SceneTransition transition = GetExistingInstance();
            if (transition == null)
            {
                Debug.LogWarning("SceneTransition instance is missing. Place SceneTransition in the first loaded scene.");
                SceneManager.LoadScene(sceneName);
                return;
            }

            transition.BeginLoad(() => SceneManager.LoadSceneAsync(sceneName));
        }

        public static void LoadScene(int buildIndex)
        {
            SceneTransition transition = GetExistingInstance();
            if (transition == null)
            {
                Debug.LogWarning("SceneTransition instance is missing. Place SceneTransition in the first loaded scene.");
                SceneManager.LoadScene(buildIndex);
                return;
            }

            transition.BeginLoad(() => SceneManager.LoadSceneAsync(buildIndex));
        }

        private static SceneTransition GetExistingInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<SceneTransition>();
            return instance;
        }

        private void BeginLoad(Func<AsyncOperation> loadOperationFactory)
        {
            if (loadRoutine != null)
            {
                return;
            }

            loadRoutine = StartCoroutine(LoadSceneRoutine(loadOperationFactory));
        }

        private IEnumerator LoadSceneRoutine(Func<AsyncOperation> loadOperationFactory)
        {
            EnsureOverlay();
            SetOverlayVisible(true);

            yield return AnimateBlocks(true, coverDuration);
            yield return WaitUnscaled(holdDuration);

            AsyncOperation loadOperation;
            try
            {
                loadOperation = loadOperationFactory.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetOverlayVisible(false);
                loadRoutine = null;
                yield break;
            }

            while (loadOperation != null && !loadOperation.isDone)
            {
                yield return null;
            }

            EnsureOverlay();
            SetBlocksToState(true);
            yield return WaitUnscaled(holdDuration);
            yield return AnimateBlocks(false, revealDuration);

            SetOverlayVisible(false);
            loadRoutine = null;
        }

        private void EnsureOverlay()
        {
            if (canvas == null)
            {
                GameObject canvasObject = new("Block Wipe Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(transform, false);

                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                canvasGroup = canvasObject.GetComponent<CanvasGroup>();
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = true;

                GameObject rootObject = new("Blocks", typeof(RectTransform));
                rootObject.transform.SetParent(canvasObject.transform, false);

                blockRoot = rootObject.GetComponent<RectTransform>();
                blockRoot.anchorMin = Vector2.zero;
                blockRoot.anchorMax = Vector2.one;
                blockRoot.offsetMin = Vector2.zero;
                blockRoot.offsetMax = Vector2.zero;
            }

            canvas.sortingOrder = sortingOrder;
            RebuildBlocksIfNeeded();
        }

        private void RebuildBlocksIfNeeded()
        {
            int safeColumns = Mathf.Max(1, columns);
            int safeRows = Mathf.Max(1, rows);
            Vector2 screenSize = new(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

            if (builtColumns == safeColumns
                && builtRows == safeRows
                && builtScreenSize == screenSize
                && blocks.Count == safeColumns * safeRows)
            {
                RefreshBlockColor();
                return;
            }

            ClearBlocks();

            builtColumns = safeColumns;
            builtRows = safeRows;
            builtScreenSize = screenSize;

            float cellWidth = screenSize.x / safeColumns;
            float cellHeight = screenSize.y / safeRows;
            Vector2 blockSize = new(Mathf.Ceil(cellWidth) + blockOverlapPixels, Mathf.Ceil(cellHeight) + blockOverlapPixels);

            for (int row = 0; row < safeRows; row++)
            {
                for (int column = 0; column < safeColumns; column++)
                {
                    GameObject blockObject = new($"Block {row:00}-{column:00}", typeof(RectTransform), typeof(Image));
                    blockObject.transform.SetParent(blockRoot, false);

                    RectTransform rectTransform = blockObject.GetComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.sizeDelta = blockSize;

                    Image image = blockObject.GetComponent<Image>();
                    image.color = blockColor;
                    image.raycastTarget = true;

                    Vector2 coveredPosition = new(
                        -screenSize.x * 0.5f + cellWidth * (column + 0.5f),
                        screenSize.y * 0.5f - cellHeight * (row + 0.5f));

                    blocks.Add(new BlockEntry(
                        rectTransform,
                        coveredPosition,
                        GetDelay01(row, column, safeColumns),
                        screenSize,
                        blockSize));
                }
            }
        }

        private void ClearBlocks()
        {
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                if (blocks[i].RectTransform != null)
                {
                    Destroy(blocks[i].RectTransform.gameObject);
                }
            }

            blocks.Clear();
        }

        private void RefreshBlockColor()
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].RectTransform != null
                    && blocks[i].RectTransform.TryGetComponent(out Image image))
                {
                    image.color = blockColor;
                }
            }
        }

        private IEnumerator AnimateBlocks(bool cover, float duration)
        {
            EnsureOverlay();

            float safeDuration = Mathf.Max(0.01f, duration);
            float delaySpan = safeDuration * Mathf.Clamp01(staggerRatio);
            float moveDuration = Mathf.Max(0.01f, safeDuration - delaySpan);
            float elapsed = 0f;

            SetBlocksToState(!cover);

            while (elapsed < safeDuration)
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    BlockEntry block = blocks[i];
                    float delay = block.Delay01 * delaySpan;
                    float progress = Mathf.Clamp01((elapsed - delay) / moveDuration);
                    progress = EaseInOut(progress);

                    Vector2 from = cover ? block.HiddenLeft : block.CoveredPosition;
                    Vector2 to = cover ? block.CoveredPosition : block.HiddenRight;
                    block.RectTransform.anchoredPosition = Vector2.LerpUnclamped(from, to, progress);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetBlocksToState(cover);
        }

        private void SetBlocksToState(bool covered)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                BlockEntry block = blocks[i];
                block.RectTransform.anchoredPosition = covered ? block.CoveredPosition : block.HiddenLeft;
            }
        }

        private void SetOverlayVisible(bool visible)
        {
            if (canvas != null)
            {
                canvas.enabled = visible;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
            }
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        private static float EaseInOut(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float GetDelay01(int row, int column, int columnCount)
        {
            float column01 = columnCount <= 1 ? 0f : column / (float)(columnCount - 1);
            float noise = Mathf.Repeat(Mathf.Sin((row + 1) * 12.9898f + (column + 1) * 78.233f) * 43758.5453f, 1f);
            return Mathf.Clamp01(column01 * 0.72f + noise * 0.28f);
        }

        private readonly struct BlockEntry
        {
            public BlockEntry(
                RectTransform rectTransform,
                Vector2 coveredPosition,
                float delay01,
                Vector2 screenSize,
                Vector2 blockSize)
            {
                RectTransform = rectTransform;
                CoveredPosition = coveredPosition;
                Delay01 = delay01;
                HiddenLeft = new Vector2(-screenSize.x * 0.5f - blockSize.x, coveredPosition.y);
                HiddenRight = new Vector2(screenSize.x * 0.5f + blockSize.x, coveredPosition.y);
            }

            public RectTransform RectTransform { get; }
            public Vector2 CoveredPosition { get; }
            public Vector2 HiddenLeft { get; }
            public Vector2 HiddenRight { get; }
            public float Delay01 { get; }
        }
    }
}
