using UnityEngine;
using UnityEngine.UI;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyStatusView : MonoBehaviour
    {
        private const int TextureSize = 64;
        private const int DefaultSortingOrder = 40;
        private const string BarsName = "EnemyStatusBars";

        [SerializeField, HideInInspector] private Health durability;
        [SerializeField, HideInInspector] private HeatGauge heat;
        [SerializeField, HideInInspector] private ExecutionTarget executionTarget;
        [SerializeField, HideInInspector] private DurabilityBarView durabilityBarView;
        [SerializeField, HideInInspector] private HeatBarView heatBarView;
        [SerializeField, HideInInspector] private Image barBackgroundImage;
        [SerializeField, HideInInspector] private Image durabilityBackgroundImage;
        [SerializeField, HideInInspector] private Image durabilityFillImage;
        [SerializeField, HideInInspector] private Image heatBackgroundImage;
        [SerializeField, HideInInspector] private Image heatFillImage;
        [SerializeField, HideInInspector] private SpriteRenderer lockOnRenderer;
        [SerializeField, HideInInspector] private SpriteRenderer executionRenderer;
        [SerializeField, HideInInspector] private RectTransform barsRoot;

        private Vector3 authoredBarOffset = new Vector3(0f, 1.1f, 0f);
        private bool hasAuthoredBarOffset;
        private bool warnedMissingFillImages;
        private Color barBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        private Color durabilityBarColor = new Color(0.75f, 0.95f, 1f, 1f);
        private Color heatBarColor = new Color(1f, 0.55f, 0.1f, 1f);
        private Color overheatedBarColor = Color.red;
        private Color lockOnColor = Color.white;
        private Color executionColor = Color.red;


        public void Configure(EnemyData data)
        {
            if (data == null)
            {
                return;
            }

            barBackgroundColor = data.StatusBarBackgroundColor;
            durabilityBarColor = data.DurabilityBarColor;
            heatBarColor = data.HeatBarColor;
            overheatedBarColor = data.OverheatedBarColor;
            lockOnColor = data.LockOnIndicatorColor;
            executionColor = data.ExecutionIndicatorColor;

            EnsureView();
            ApplyColors();
        }

        public void SetTargets(Health nextDurability, HeatGauge nextHeat)
        {
            durability = nextDurability;
            heat = nextHeat;
            EnsureView();
            durabilityBarView?.SetTarget(durability);
            heatBarView?.SetTarget(heat);
        }

        public bool OwnsRenderer(SpriteRenderer renderer)
        {
            return renderer != null && (renderer == lockOnRenderer || renderer == executionRenderer);
        }

        private void Awake()
        {
            if (durability == null)
            {
                durability = GetComponentInParent<Health>();
            }

            if (heat == null)
            {
                heat = GetComponentInParent<HeatGauge>();
            }

            if (executionTarget == null)
            {
                executionTarget = GetComponentInParent<ExecutionTarget>();
            }

            EnsureView();
            SetTargets(durability, heat);
            ApplyColors();
        }

        private void LateUpdate()
        {
            bool alive = durability != null && !durability.IsDead;
            SetRootVisible(alive);
            if (!alive)
            {
                return;
            }

            if (barsRoot != null)
            {
                barsRoot.position = transform.position + authoredBarOffset;

                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    barsRoot.rotation = mainCamera.transform.rotation;
                }
            }

            ApplyWorldCenter(lockOnRenderer);
            ApplyWorldCenter(executionRenderer);

            if (lockOnRenderer != null)
            {
                lockOnRenderer.enabled = PlayerCombatController.Active != null
                    && !PlayerCombatController.Active.IsExecuting
                    && PlayerCombatController.Active.LockOnTarget == durability;
            }

            if (executionRenderer != null)
            {
                executionRenderer.enabled = executionTarget != null
                    && PlayerCombatController.Active != null
                    && executionTarget.CanExecute(PlayerCombatController.Active.transform);
            }
        }

        private void EnsureView()
        {
            EnsureBars();
            EnsureIndicators();
        }

        private void EnsureBars()
        {
            if (barsRoot != null)
            {
                CaptureBarOffset();
                CacheBarImages();
                return;
            }

            barsRoot = FindBarsRoot();
            if (barsRoot != null)
            {
                CaptureBarOffset();
                CacheBarImages();
                return;
            }

            GameObject canvasObject = new GameObject(BarsName, typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            barsRoot = canvasObject.GetComponent<RectTransform>();
            barsRoot.localPosition = new Vector3(0f, 1.1f, 0f);
            barsRoot.sizeDelta = new Vector2(1.2f, 0.22f);
            CaptureBarOffset();

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = DefaultSortingOrder;

            barBackgroundImage = CreateImage("Background", canvasObject.transform, barBackgroundColor);
            Stretch(barBackgroundImage.rectTransform);

            RectTransform durabilityRow = CreateRow("Durability", canvasObject.transform, 0.5f, 1f);
            durabilityBackgroundImage = CreateImage("Background", durabilityRow, barBackgroundColor);
            Stretch(durabilityBackgroundImage.rectTransform);
            durabilityFillImage = CreateImage("Fill", durabilityRow, durabilityBarColor);
            ConfigureFill(durabilityFillImage);
            durabilityBarView = durabilityRow.gameObject.AddComponent<DurabilityBarView>();
            durabilityBarView.SetBindPlayerOnStart(false);
            durabilityBarView.Configure(durabilityFillImage);

            RectTransform heatRow = CreateRow("Heat", canvasObject.transform, 0f, 0.5f);
            heatBackgroundImage = CreateImage("Background", heatRow, barBackgroundColor);
            Stretch(heatBackgroundImage.rectTransform);
            heatFillImage = CreateImage("Fill", heatRow, heatBarColor);
            ConfigureFill(heatFillImage);
            heatBarView = heatRow.gameObject.AddComponent<HeatBarView>();
            heatBarView.SetBindPlayerOnStart(false);
            heatBarView.Configure(heatFillImage);
        }

        private void CacheBarImages()
        {
            CaptureBarOffset();

            barBackgroundImage ??= FindImage("Background");
            durabilityBackgroundImage ??= FindImage("Durability/Background");
            durabilityFillImage ??= FindImage("Durability/Fill");
            heatBackgroundImage ??= FindImage("Heat/Background");
            heatFillImage ??= FindImage("Heat/Fill");
            durabilityFillImage ??= FindFillImage("Durability", 0);
            heatFillImage ??= FindFillImage("Heat", 1);

            durabilityBarView ??= FindBarView<DurabilityBarView>("Durability");
            heatBarView ??= FindBarView<HeatBarView>("Heat");

            if (durabilityBarView == null && durabilityFillImage != null)
            {
                Transform fillParent = durabilityFillImage.transform.parent;
                if (fillParent != null)
                {
                    durabilityBarView = fillParent.gameObject.AddComponent<DurabilityBarView>();
                }
            }

            if (heatBarView == null && heatFillImage != null)
            {
                Transform fillParent = heatFillImage.transform.parent;
                if (fillParent != null)
                {
                    heatBarView = fillParent.gameObject.AddComponent<HeatBarView>();
                }
            }

            if (durabilityBarView != null && durabilityFillImage != null)
            {
                durabilityBarView.SetBindPlayerOnStart(false);
                ConfigureFillMode(durabilityFillImage);
                durabilityBarView.Configure(durabilityFillImage);
            }

            if (heatBarView != null && heatFillImage != null)
            {
                heatBarView.SetBindPlayerOnStart(false);
                ConfigureFillMode(heatFillImage);
                heatBarView.Configure(heatFillImage);
            }

            if (!warnedMissingFillImages && (durabilityFillImage == null || heatFillImage == null))
            {
                warnedMissingFillImages = true;
                Debug.LogWarning($"{nameof(EnemyStatusView)} requires Durability/Fill and Heat/Fill images under {BarsName}.", this);
            }
        }

        private void EnsureIndicators()
        {
            if (lockOnRenderer == null)
            {
                lockOnRenderer = FindIndicator("LockOnIndicator");
            }

            if (lockOnRenderer == null)
            {
                lockOnRenderer = CreateIndicator("LockOnIndicator", lockOnColor, DefaultSortingOrder + 1, 1.1f);
            }

            if (executionRenderer == null)
            {
                executionRenderer = FindIndicator("ExecutionIndicator");
            }

            if (executionRenderer == null)
            {
                executionRenderer = CreateIndicator("ExecutionIndicator", executionColor, DefaultSortingOrder + 2, 0.9f);
            }
        }

        private void ApplyColors()
        {
            if (barBackgroundImage != null)
            {
                barBackgroundImage.color = barBackgroundColor;
            }

            if (durabilityBackgroundImage != null)
            {
                durabilityBackgroundImage.color = barBackgroundColor;
            }

            if (durabilityFillImage != null)
            {
                durabilityFillImage.color = durabilityBarColor;
            }

            if (heatBackgroundImage != null)
            {
                heatBackgroundImage.color = barBackgroundColor;
            }

            if (heatBarView != null)
            {
                heatBarView.SetColors(heatBarColor, overheatedBarColor);
            }

            ApplyIndicatorColor(lockOnRenderer, lockOnColor);
            ApplyIndicatorColor(executionRenderer, executionColor);
        }

        private void CaptureBarOffset()
        {
            if (hasAuthoredBarOffset || barsRoot == null)
            {
                return;
            }

            authoredBarOffset = barsRoot.localPosition;
            hasAuthoredBarOffset = true;
        }

        private void ApplyWorldCenter(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.transform.position = transform.position;
            renderer.transform.rotation = Quaternion.identity;
        }

        private void SetRootVisible(bool visible)
        {
            if (barsRoot != null)
            {
                barsRoot.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                if (lockOnRenderer != null)
                {
                    lockOnRenderer.enabled = false;
                }

                if (executionRenderer != null)
                {
                    executionRenderer.enabled = false;
                }
            }
        }

        private SpriteRenderer CreateIndicator(string name, Color color, int order, float defaultScale)
        {
            GameObject indicatorObject = new GameObject(name);
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.localScale = Vector3.one * defaultScale;

            SpriteRenderer renderer = indicatorObject.AddComponent<SpriteRenderer>();
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private SpriteRenderer FindIndicator(string name)
        {
            Transform indicator = transform.Find(name);
            return indicator != null ? indicator.GetComponent<SpriteRenderer>() : null;
        }

        private RectTransform FindBarsRoot()
        {
            Transform existing = transform.Find(BarsName);
            if (existing == null)
            {
                existing = FindChildRecursive(transform, BarsName);
            }

            return existing != null ? existing.GetComponent<RectTransform>() : null;
        }

        private Image FindImage(string path)
        {
            Transform imageTransform = barsRoot != null ? barsRoot.Find(path) : null;
            if (imageTransform == null)
            {
                imageTransform = FindNestedPath(barsRoot, path);
            }

            return imageTransform != null ? imageTransform.GetComponent<Image>() : null;
        }

        private Image FindFillImage(string rowName, int fallbackIndex)
        {
            if (barsRoot == null)
            {
                return null;
            }

            Image[] images = barsRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null
                    && images[i].name == "Fill"
                    && HasParentNamed(images[i].transform, rowName))
                {
                    return images[i];
                }
            }

            int fillIndex = 0;
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null || images[i].name != "Fill")
                {
                    continue;
                }

                if (fillIndex == fallbackIndex)
                {
                    return images[i];
                }

                fillIndex++;
            }

            return null;
        }

        private T FindBarView<T>(string path) where T : Component
        {
            Transform barTransform = barsRoot != null ? barsRoot.Find(path) : null;
            if (barTransform == null)
            {
                barTransform = FindChildRecursive(barsRoot, path);
            }

            return barTransform != null ? barTransform.GetComponent<T>() : null;
        }

        private static bool HasParentNamed(Transform transformToCheck, string parentName)
        {
            Transform current = transformToCheck != null ? transformToCheck.parent : null;
            while (current != null)
            {
                if (current.name == parentName)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static Transform FindNestedPath(Transform root, string path)
        {
            if (root == null)
            {
                return null;
            }

            int separator = path.IndexOf('/');
            if (separator < 0)
            {
                return FindChildRecursive(root, path);
            }

            string parentName = path.Substring(0, separator);
            string childName = path.Substring(separator + 1);
            Transform parent = FindChildRecursive(root, parentName);
            return FindChildRecursive(parent, childName);
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static RectTransform CreateRow(string name, Transform parent, float anchorMinY, float anchorMaxY)
        {
            GameObject rowObject = new GameObject(name, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);

            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0f, anchorMinY);
            row.anchorMax = new Vector2(1f, anchorMaxY);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;
            return row;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.AddComponent<Image>();
            image.sprite = CreateUiSprite();
            image.color = color;
            return image;
        }

        private static void ConfigureFill(Image image)
        {
            ConfigureFillMode(image);
            Stretch(image.rectTransform);
        }

        private static void ConfigureFillMode(Image image)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private void ApplyIndicatorColor(SpriteRenderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            if (renderer.sprite == null)
            {
                renderer.sprite = renderer == lockOnRenderer ? CreateRingSprite() : CreateFilledCircleSprite();
            }

            renderer.color = color;
        }

        private static Sprite CreateRingSprite()
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            float center = (TextureSize - 1) * 0.5f;
            float outer = center;
            float inner = center * 0.78f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    texture.SetPixel(x, y, distance <= outer && distance >= inner ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        }

        private static Sprite CreateFilledCircleSprite()
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            float center = (TextureSize - 1) * 0.5f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    texture.SetPixel(x, y, distance <= center ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        }

        private static Sprite CreateUiSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
