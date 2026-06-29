using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class PixelBlockRevealView : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Graphic panelGraphic;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Reveal")]
        [FormerlySerializedAs("revealDuration")]
        [SerializeField, Min(0.05f)] private float openDuration = 0.9f;
        [SerializeField, Min(0.05f)] private float closeDuration = 0.45f;
        [SerializeField, Min(4f)] private float cellSize = 28f;
        [SerializeField, Min(1)] private int maxCellCount = 1000;
        [SerializeField, Min(0f)] private float cellOverlap = 1f;
        [SerializeField, Range(0f, 0.5f)] private float edgeRandomness = 0.14f;
        [SerializeField, Range(0.01f, 0.5f)] private float cellAppearWindow = 0.16f;
        [SerializeField, Range(0f, 1f)] private float blockFadeStart = 0.9f;
        [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private Shader panelRevealShader;

        [Header("Effect")]
        [SerializeField, Range(0.05f, 1f)] private float effectCellDensity = 0.55f;

        [Header("Content")]
        [SerializeField] private bool syncContentWithReveal = true;
        [SerializeField, Range(0f, 1f)] private float contentFadeStart = 0.92f;
        [SerializeField, Range(0.01f, 0.5f)] private float contentFadeWindow = 0.08f;
        [SerializeField] private bool blockInteractionUntilComplete = true;

        [Header("Effect Color")]
        [SerializeField] private Color edgeColor = new(0.5f, 1f, 1f, 0.95f);
        [SerializeField] private Color glowColor = new(0.13f, 0.95f, 1f, 0.28f);
        [SerializeField, Range(0f, 1f)] private float panelRevealTintStrength = 1f;
        [SerializeField, Min(0f)] private float panelRevealTintDuration = 0.2f;
        [SerializeField, Min(1f)] private float glowScale = 2.2f;

        [Header("Performance")]
        [SerializeField] private bool isolateRuntimeCanvases = true;

        private const float InvisibleAlpha = 0.001f;
        private const string PanelRevealShaderName = "Week14/UI/PixelRevealMask";
        private static readonly int RevealProgressProperty = Shader.PropertyToID("_RevealProgress");
        private static readonly int PanelRectProperty = Shader.PropertyToID("_PanelRect");
        private static readonly int GridSizeProperty = Shader.PropertyToID("_GridSize");
        private static readonly int CellWindowProperty = Shader.PropertyToID("_CellWindow");
        private static readonly int EdgeRandomnessProperty = Shader.PropertyToID("_EdgeRandomness");
        private static readonly int MaxCellDelayProperty = Shader.PropertyToID("_MaxCellDelay");
        private static readonly int RevealTintColorProperty = Shader.PropertyToID("_RevealTintColor");
        private static readonly int RevealTintStrengthProperty = Shader.PropertyToID("_RevealTintStrength");
        private static readonly int RevealTintDurationProperty = Shader.PropertyToID("_RevealTintDuration");

        private readonly List<PixelCell> cells = new();
        private readonly List<GraphicState> contentGraphics = new();
        private readonly List<SelectableState> contentSelectables = new();
        private readonly Dictionary<Graphic, Color> originalGraphicColors = new();
        private readonly Dictionary<Selectable, bool> originalSelectableStates = new();
        private RectTransform panelRect;
        private RectTransform cellRoot;
        private Coroutine revealRoutine;
        private Color panelOriginalColor;
        private Material panelOriginalMaterial;
        private Material panelRevealMaterial;
        private bool panelColorCached;
        private bool panelRevealShaderWarningShown;
        private int gridColumns = 1;
        private int gridRows = 1;
        private float currentProgress;
        private float activeRevealDuration = 0.9f;
        private readonly Vector3[] graphicWorldCorners = new Vector3[4];

        private void Awake()
        {
            CacheTarget();
            BuildCells();
            CacheContent();
            SetProgress(0f);
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDestroy()
        {
            RestoreContent();
            RestorePanel();
            DestroyPanelRevealMaterial();
        }

        [ContextMenu("Play Reveal")]
        public void Play()
        {
            CacheTarget();
            BuildCells();
            CacheContent();
            PlayFromTo(0f, 1f, openDuration, null);
        }

        public void PlayHide(Action onComplete = null)
        {
            CacheTarget();
            BuildCells();
            CacheContent();
            PlayFromTo(currentProgress, 0f, closeDuration, onComplete);
        }

        public static bool TryPlayHide(GameObject root, Action onComplete)
        {
            if (root == null || !root.activeInHierarchy)
            {
                return false;
            }

            PixelBlockRevealView revealView = root.GetComponent<PixelBlockRevealView>()
                ?? root.GetComponentInChildren<PixelBlockRevealView>();
            if (revealView == null || !revealView.isActiveAndEnabled)
            {
                return false;
            }

            revealView.PlayHide(onComplete);
            return true;
        }

        [ContextMenu("Show Immediate")]
        public void ShowImmediate()
        {
            CacheTarget();
            BuildCells();
            CacheContent();
            SetProgress(1f);
        }

        [ContextMenu("Hide Immediate")]
        public void HideImmediate()
        {
            CacheTarget();
            BuildCells();
            CacheContent();
            SetProgress(0f);
        }

        private void PlayFromTo(float from, float to, float duration, Action onComplete)
        {
            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
            }

            activeRevealDuration = Mathf.Max(0.0001f, duration);
            revealRoutine = StartCoroutine(PlayRoutine(from, to, activeRevealDuration, onComplete));
        }

        private IEnumerator PlayRoutine(float from, float to, float duration, Action onComplete)
        {
            SetProgress(from);

            for (float elapsed = 0f; elapsed < duration; elapsed += GetDeltaTime())
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetProgress(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetProgress(to);
            revealRoutine = null;
            onComplete?.Invoke();
        }

        private void CacheTarget()
        {
            panelGraphic ??= GetComponent<Graphic>();
            panelRect = panelGraphic != null ? panelGraphic.rectTransform : transform as RectTransform;

            if (panelGraphic != null && !panelColorCached)
            {
                panelOriginalColor = panelGraphic.color;
                panelOriginalMaterial = panelGraphic.material;
                panelColorCached = true;
            }
        }

        private void BuildCells()
        {
            if (panelRect == null)
            {
                return;
            }

            if (cellRoot == null)
            {
                GameObject rootObject = new("Pixel Block Reveal Cells", typeof(RectTransform));
                rootObject.transform.SetParent(panelRect, false);
                cellRoot = rootObject.GetComponent<RectTransform>();
                cellRoot.anchorMin = Vector2.zero;
                cellRoot.anchorMax = Vector2.one;
                cellRoot.offsetMin = Vector2.zero;
                cellRoot.offsetMax = Vector2.zero;
                cellRoot.SetAsLastSibling();
            }
            ConfigureRuntimeCanvas(cellRoot);

            Rect rect = panelRect.rect;
            CalculateGrid(rect, out int columns, out int rows);
            gridColumns = columns;
            gridRows = rows;
            int requiredCount = columns * rows;
            PreparePanelRevealMaterial();

            while (cells.Count < requiredCount)
            {
                cells.Add(CreateCell(cells.Count));
            }

            for (int i = requiredCount; i < cells.Count; i++)
            {
                cells[i].Root.gameObject.SetActive(false);
            }

            Vector2 actualCellSize = new(rect.width / columns, rect.height / rows);
            Vector2 blockSize = actualCellSize + Vector2.one * cellOverlap;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    Vector2 position = new(
                        rect.xMin + actualCellSize.x * (column + 0.5f),
                        rect.yMax - actualCellSize.y * (row + 0.5f));

                    if (!ShouldShowEffectCell(index))
                    {
                        cells[index].Root.gameObject.SetActive(false);
                        continue;
                    }

                    ConfigureCell(cells[index], index, position, blockSize, rect);
                }
            }
        }

        private PixelCell CreateCell(int index)
        {
            GameObject rootObject = new($"Pixel Cell {index:000}", typeof(RectTransform));
            rootObject.transform.SetParent(cellRoot, false);

            RectTransform root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);

            Image glow = CreateImage("Glow", root);
            Image edge = CreateImage("Edge", root);
            return new PixelCell(root, edge, glow);
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = true;
            return image;
        }

        private void ConfigureCell(PixelCell cell, int index, Vector2 position, Vector2 size, Rect rect)
        {
            cell.Root.gameObject.SetActive(true);
            cell.Root.anchoredPosition = position;
            cell.Root.sizeDelta = size;

            float normalizedX = rect.width <= 0f ? 0f : position.x / (rect.width * 0.5f);
            float normalizedY = rect.height <= 0f ? 0f : position.y / (rect.height * 0.5f);
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            float randomOffset = (Random01(index * 17.17f + 3.31f) - 0.5f) * edgeRandomness;
            float normalizedDistance = Mathf.Clamp01((distance / Mathf.Sqrt(2f)) + randomOffset);
            cell.Delay = normalizedDistance * GetMaxCellDelay();
            cell.LastEffectAlpha = -1f;
            cell.LastEffectScale = -1f;
        }

        private bool ShouldShowEffectCell(int index)
        {
            return effectCellDensity >= 1f || Random01(index * 31.71f + 9.13f) <= effectCellDensity;
        }

        private void CacheContent()
        {
            contentGraphics.Clear();
            contentSelectables.Clear();

            Transform root = contentRoot != null ? contentRoot : panelRect;
            if (root == null)
            {
                return;
            }

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == panelGraphic || IsRuntimeRevealGraphic(graphic))
                {
                    continue;
                }

                if (!originalGraphicColors.TryGetValue(graphic, out Color originalColor))
                {
                    originalColor = graphic.color;
                    originalGraphicColors.Add(graphic, originalColor);
                }

                contentGraphics.Add(new GraphicState(graphic, originalColor, GetGraphicRevealEnd(graphic)));
            }

            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (!originalSelectableStates.TryGetValue(selectable, out bool originalInteractable))
                {
                    originalInteractable = selectable.interactable;
                    originalSelectableStates.Add(selectable, originalInteractable);
                }

                contentSelectables.Add(new SelectableState(selectable, originalInteractable));
            }
        }

        private bool IsRuntimeRevealGraphic(Graphic graphic)
        {
            return cellRoot != null && graphic.transform.IsChildOf(cellRoot);
        }

        private void SetProgress(float progress)
        {
            currentProgress = Mathf.Clamp01(progress);
            float t = revealCurve.Evaluate(currentProgress);
            SetPanelProgress(t);
            SetCellProgress(t);
            SetContentProgress(t);
        }

        private void SetPanelProgress(float progress)
        {
            if (panelGraphic == null || !panelColorCached)
            {
                return;
            }

            Color color = panelOriginalColor;
            panelGraphic.color = color;
            PreparePanelRevealMaterial();

            if (panelRevealMaterial != null)
            {
                UpdatePanelRevealMaterial(progress);
                return;
            }

            color.a = progress >= 1f ? panelOriginalColor.a : 0f;
            panelGraphic.color = color;
        }

        private void SetCellProgress(float progress)
        {
            float fadeStart = Mathf.Max(blockFadeStart, 1f - cellAppearWindow * 0.25f);
            float finalFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, 1f, progress));
            bool completed = progress >= 1f;

            for (int i = 0; i < cells.Count; i++)
            {
                PixelCell cell = cells[i];
                if (!cell.Root.gameObject.activeSelf)
                {
                    continue;
                }

                if (progress < cell.Delay)
                {
                    SetEffectCellAlpha(cell, 0f);
                    continue;
                }

                if (!completed
                    && progress >= cell.Delay + cellAppearWindow
                    && Mathf.Approximately(cell.LastEffectAlpha, 0f))
                {
                    continue;
                }

                float local = Mathf.Clamp01((progress - cell.Delay) / cellAppearWindow);
                float pulse = Mathf.Sin(local * Mathf.PI) * (1f - finalFade);
                float edgeAlpha = pulse;

                if (edgeAlpha <= InvisibleAlpha)
                {
                    SetEffectCellAlpha(cell, 0f);
                    continue;
                }

                float scale = Mathf.Lerp(0.55f, 1f, EaseOut(local));
                SetEffectCellScale(cell, scale);
                SetEffectCellAlpha(cell, edgeAlpha);
            }
        }

        private void SetContentProgress(float progress)
        {
            for (int i = 0; i < contentGraphics.Count; i++)
            {
                GraphicState state = contentGraphics[i];
                if (state.Graphic == null)
                {
                    continue;
                }

                float trigger = syncContentWithReveal ? state.RevealEnd : contentFadeStart;
                float alpha = progress >= trigger ? 1f : 0f;
                Color color = state.OriginalColor;
                color.a *= alpha;
                state.Graphic.color = color;
            }

            bool interactable = !blockInteractionUntilComplete || progress >= 1f;
            for (int i = 0; i < contentSelectables.Count; i++)
            {
                SelectableState state = contentSelectables[i];
                if (state.Selectable != null)
                {
                    state.Selectable.interactable = state.OriginalInteractable && interactable;
                }
            }
        }

        private void RestoreContent()
        {
            for (int i = 0; i < contentGraphics.Count; i++)
            {
                GraphicState state = contentGraphics[i];
                if (state.Graphic != null)
                {
                    state.Graphic.color = state.OriginalColor;
                }
            }

            for (int i = 0; i < contentSelectables.Count; i++)
            {
                SelectableState state = contentSelectables[i];
                if (state.Selectable != null)
                {
                    state.Selectable.interactable = state.OriginalInteractable;
                }
            }
        }

        private void RestorePanel()
        {
            if (panelGraphic != null && panelColorCached)
            {
                panelGraphic.color = panelOriginalColor;
                panelGraphic.material = panelOriginalMaterial;
            }
        }

        private void PreparePanelRevealMaterial()
        {
            if (panelGraphic == null)
            {
                return;
            }

            Shader shader = ResolvePanelRevealShader();
            if (shader == null)
            {
                return;
            }

            if (panelRevealMaterial == null || panelRevealMaterial.shader != shader)
            {
                DestroyPanelRevealMaterial();
                panelRevealMaterial = new Material(shader)
                {
                    name = "Pixel Reveal Panel Material"
                };
            }

            if (panelGraphic.material != panelRevealMaterial)
            {
                panelGraphic.material = panelRevealMaterial;
            }
        }

        private Shader ResolvePanelRevealShader()
        {
            if (panelRevealShader != null)
            {
                return panelRevealShader;
            }

            panelRevealShader = Shader.Find(PanelRevealShaderName);
            if (panelRevealShader == null && !panelRevealShaderWarningShown)
            {
                Debug.LogWarning($"PixelBlockRevealView: {PanelRevealShaderName} shader not found.", this);
                panelRevealShaderWarningShown = true;
            }

            return panelRevealShader;
        }

        private void UpdatePanelRevealMaterial(float progress)
        {
            if (panelRevealMaterial == null || panelRect == null)
            {
                return;
            }

            Rect rect = panelRect.rect;
            panelRevealMaterial.SetFloat(RevealProgressProperty, Mathf.Clamp01(progress));
            panelRevealMaterial.SetVector(PanelRectProperty, new Vector4(rect.xMin, rect.yMin, rect.width, rect.height));
            panelRevealMaterial.SetVector(GridSizeProperty, new Vector4(Mathf.Max(1, gridColumns), Mathf.Max(1, gridRows), 0f, 0f));
            panelRevealMaterial.SetFloat(CellWindowProperty, Mathf.Max(0.0001f, cellAppearWindow));
            panelRevealMaterial.SetFloat(EdgeRandomnessProperty, edgeRandomness);
            panelRevealMaterial.SetFloat(MaxCellDelayProperty, GetMaxCellDelay());
            panelRevealMaterial.SetColor(RevealTintColorProperty, edgeColor);
            panelRevealMaterial.SetFloat(RevealTintStrengthProperty, panelRevealTintStrength);
            panelRevealMaterial.SetFloat(RevealTintDurationProperty, GetNormalizedTintDuration());
        }

        private float GetNormalizedTintDuration()
        {
            return activeRevealDuration <= 0f ? 0f : Mathf.Clamp01(panelRevealTintDuration / activeRevealDuration);
        }

        private void DestroyPanelRevealMaterial()
        {
            if (panelRevealMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(panelRevealMaterial);
            }
            else
            {
                DestroyImmediate(panelRevealMaterial);
            }

            panelRevealMaterial = null;
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private float GetGraphicRevealEnd(Graphic graphic)
        {
            if (panelRect == null || graphic == null)
            {
                return contentFadeWindow;
            }

            RectTransform rectTransform = graphic.rectTransform;
            rectTransform.GetWorldCorners(graphicWorldCorners);

            float maxDistance = 0f;
            for (int i = 0; i < graphicWorldCorners.Length; i++)
            {
                Vector2 localPoint = panelRect.InverseTransformPoint(graphicWorldCorners[i]);
                float normalizedDistance = GetNormalizedDistance(localPoint, panelRect.rect);
                maxDistance = Mathf.Max(maxDistance, normalizedDistance);
            }

            return Mathf.Clamp01(maxDistance * GetMaxCellDelay() + contentFadeWindow);
        }

        private float GetMaxCellDelay()
        {
            return Mathf.Clamp01(1f - cellAppearWindow);
        }

        private void CalculateGrid(Rect rect, out int columns, out int rows)
        {
            columns = Mathf.Max(1, Mathf.CeilToInt(rect.width / cellSize));
            rows = Mathf.Max(1, Mathf.CeilToInt(rect.height / cellSize));

            int currentCount = columns * rows;
            if (currentCount <= maxCellCount)
            {
                return;
            }

            float aspect = rect.height <= 0f ? 1f : rect.width / rect.height;
            columns = Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(maxCellCount * aspect)));
            rows = Mathf.Max(1, Mathf.FloorToInt(maxCellCount / (float)columns));

            while (columns * rows > maxCellCount)
            {
                if (columns >= rows)
                {
                    columns--;
                }
                else
                {
                    rows--;
                }

                columns = Mathf.Max(1, columns);
                rows = Mathf.Max(1, rows);
            }
        }

        private void ConfigureRuntimeCanvas(RectTransform root)
        {
            if (!isolateRuntimeCanvases || root == null)
            {
                return;
            }

            Canvas canvas = root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = root.gameObject.AddComponent<Canvas>();
            }

            canvas.overrideSorting = false;
            canvas.pixelPerfect = false;
        }

        private static float GetNormalizedDistance(Vector2 localPoint, Rect rect)
        {
            float normalizedX = rect.width <= 0f ? 0f : localPoint.x / (rect.width * 0.5f);
            float normalizedY = rect.height <= 0f ? 0f : localPoint.y / (rect.height * 0.5f);
            float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            return Mathf.Clamp01(distance / Mathf.Sqrt(2f));
        }

        private void SetEffectCellAlpha(PixelCell cell, float alpha)
        {
            if (Mathf.Approximately(cell.LastEffectAlpha, alpha))
            {
                return;
            }

            cell.LastEffectAlpha = alpha;
            SetImageColor(cell.Edge, edgeColor, alpha);
            SetImageColor(cell.Glow, glowColor, alpha);
        }

        private void SetEffectCellScale(PixelCell cell, float scale)
        {
            if (Mathf.Approximately(cell.LastEffectScale, scale))
            {
                return;
            }

            cell.LastEffectScale = scale;
            cell.Root.localScale = new Vector3(scale, scale, 1f);
            cell.Glow.rectTransform.localScale = new Vector3(glowScale, glowScale, 1f);
        }

        private static void SetImageColor(Image image, Color baseColor, float alpha)
        {
            Color color = baseColor;
            color.a *= alpha;
            image.color = color;
        }

        private static float EaseOut(float value)
        {
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private static float Random01(float seed)
        {
            return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
        }

        private sealed class PixelCell
        {
            public PixelCell(RectTransform root, Image edge, Image glow)
            {
                Root = root;
                Edge = edge;
                Glow = glow;
            }

            public RectTransform Root { get; }
            public Image Edge { get; }
            public Image Glow { get; }
            public float Delay { get; set; }
            public float LastEffectAlpha { get; set; } = -1f;
            public float LastEffectScale { get; set; } = -1f;
        }

        private readonly struct GraphicState
        {
            public GraphicState(Graphic graphic, Color originalColor, float revealEnd)
            {
                Graphic = graphic;
                OriginalColor = originalColor;
                RevealEnd = revealEnd;
            }

            public Graphic Graphic { get; }
            public Color OriginalColor { get; }
            public float RevealEnd { get; }
        }

        private readonly struct SelectableState
        {
            public SelectableState(Selectable selectable, bool originalInteractable)
            {
                Selectable = selectable;
                OriginalInteractable = originalInteractable;
            }

            public Selectable Selectable { get; }
            public bool OriginalInteractable { get; }
        }
    }
}
