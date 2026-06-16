using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyStatusView : MonoBehaviour
    {
        private const int DefaultSortingOrder = 40;
        private const string BarsName = "EnemyStatusBars";
        private static readonly Vector2 BarsSize = new(2.35f, 0.4f);
        private static Sprite indicatorSprite;

        [SerializeField, HideInInspector] private Health health;
        [SerializeField, HideInInspector] private BulletGauge bullets;
        [SerializeField, HideInInspector] private ExecutionTarget executionTarget;
        [SerializeField, HideInInspector] private EnemyAI enemyAI;
        [SerializeField, HideInInspector] private BulletBarView bulletBarView;
        [SerializeField, HideInInspector] private SpriteRenderer lockOnRenderer;
        [SerializeField, HideInInspector] private SpriteRenderer executionRenderer;
        [SerializeField, HideInInspector] private RectTransform barsRoot;

        private Vector3 authoredBarOffset = new(0f, 0.65f, 0f);
        private Color bulletColor = new(1f, 0.55f, 0.1f, 1f);
        private Color emptyColor = Color.red;
        private Color lockOnColor = Color.white;
        private Color executionColor = Color.red;
        private bool suppressed;

        public void SetIndicators(SpriteRenderer nextLockOnRenderer, SpriteRenderer nextExecutionRenderer)
        {
            lockOnRenderer = nextLockOnRenderer;
            executionRenderer = nextExecutionRenderer;
            EnsureIndicators();
            ApplyColors();
        }

        public void Configure(EnemyData data)
        {
            if (data == null)
            {
                return;
            }

            bulletColor = data.BulletBarColor;
            emptyColor = data.EmptyBulletBarColor;
            lockOnColor = data.LockOnIndicatorColor;
            executionColor = data.ExecutionIndicatorColor;

            EnsureView();
            ApplyColors();
        }

        public void SetTargets(Health nextHealth, BulletGauge nextBullets)
        {
            health = nextHealth;
            bullets = nextBullets;
            EnsureView();
            bulletBarView?.SetTarget(bullets);
        }

        public bool OwnsRenderer(SpriteRenderer renderer)
        {
            return renderer != null && (renderer == lockOnRenderer || renderer == executionRenderer);
        }

        public void SetSuppressed(bool value)
        {
            suppressed = value;
            SetBarsVisible(!suppressed);
        }

        private void Awake()
        {
            health ??= GetComponentInParent<Health>();
            bullets ??= GetComponentInParent<BulletGauge>();
            executionTarget ??= GetComponentInParent<ExecutionTarget>();
            enemyAI ??= GetComponentInParent<EnemyAI>();

            EnsureView();
            SetTargets(health, bullets);
            ApplyColors();
        }

        private void LateUpdate()
        {
            bool alive = health != null && !health.IsDead;
            bool canShowDuringEnemyState = enemyAI == null || !enemyAI.IsExecutionLocked;
            bool barsVisible = alive && !suppressed && canShowDuringEnemyState;
            bool indicatorsVisible = alive && canShowDuringEnemyState;
            SetBarsVisible(barsVisible);

            if (!indicatorsVisible)
            {
                SetIndicatorsVisible(false);
                return;
            }

            if (barsVisible && barsRoot != null)
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

            SetRendererEnabled(lockOnRenderer, IsLockOnTarget());
            SetRendererEnabled(
                executionRenderer,
                IsHoveredExecutionTarget());
        }

        private bool IsLockOnTarget()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || player.IsExecuting || player.LockOnTarget == null)
            {
                return false;
            }

            if (player.LockOnTarget == health)
            {
                return true;
            }

            EnemyAI targetEnemy = player.LockOnTarget.GetComponent<EnemyAI>()
                ?? player.LockOnTarget.GetComponentInParent<EnemyAI>();
            return enemyAI != null && targetEnemy == enemyAI;
        }

        private bool IsHoveredExecutionTarget()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            return player != null
                && executionTarget != null
                && player.HoveredExecutionTarget == executionTarget
                && executionTarget.CanExecute(player.transform);
        }

        private void EnsureView()
        {
            enemyAI ??= GetComponentInParent<EnemyAI>();
            EnsureBars();
            EnsureIndicators();
        }

        private void EnsureBars()
        {
            if (barsRoot == null)
            {
                barsRoot = FindExistingBarsRoot();
            }

            if (barsRoot == null)
            {
                GameObject canvasObject = new(BarsName, typeof(RectTransform));
                canvasObject.transform.SetParent(transform, false);
                barsRoot = canvasObject.GetComponent<RectTransform>();
                barsRoot.sizeDelta = BarsSize;

                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = DefaultSortingOrder;
            }

            RectTransform row = FindRow("Bullet") ?? CreateRow("Bullet", barsRoot);
            row.gameObject.name = "Bullet";
            DisableChild(row, "Background");
            DisableChild(row, "Fill");

            bulletBarView = row.GetComponent<BulletBarView>() ?? row.gameObject.AddComponent<BulletBarView>();
            bulletBarView.SetBindPlayerOnStart(false);
            bulletBarView.SetColors(bulletColor, emptyColor);
            bulletBarView.SetIconLayout(10, 0.9f, 0.48f, 0.16f);
            bulletBarView.Configure();
        }

        private RectTransform FindExistingBarsRoot()
        {
            Transform existing = transform.Find(BarsName);
            return existing != null ? existing.GetComponent<RectTransform>() : null;
        }

        private RectTransform FindRow(string rowName)
        {
            Transform row = barsRoot != null ? barsRoot.Find(rowName) : null;
            return row != null ? row.GetComponent<RectTransform>() : null;
        }

        private static RectTransform CreateRow(string rowName, Transform parent)
        {
            GameObject rowObject = new(rowName, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);
            RectTransform row = rowObject.GetComponent<RectTransform>();
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;
            return row;
        }

        private static void DisableChild(Transform parent, string childName)
        {
            Transform child = parent != null ? parent.Find(childName) : null;
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void EnsureIndicators()
        {
            lockOnRenderer ??= FindIndicator("LockOnIndicator") ?? CreateIndicator("LockOnIndicator", lockOnColor, 0.52f);
            executionRenderer ??= FindIndicator("ExecutionIndicator") ?? CreateIndicator("ExecutionIndicator", executionColor, 0.42f);
            ConfigureIndicator(lockOnRenderer, lockOnColor, DefaultSortingOrder + 1);
            ConfigureIndicator(executionRenderer, executionColor, DefaultSortingOrder + 2);
        }

        private SpriteRenderer FindIndicator(string indicatorName)
        {
            Transform directChild = transform.Find(indicatorName);
            if (directChild != null && directChild.TryGetComponent(out SpriteRenderer directRenderer))
            {
                return directRenderer;
            }

            SpriteRenderer[] candidates = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && candidates[i].name == indicatorName)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private SpriteRenderer CreateIndicator(string indicatorName, Color color, float scale)
        {
            GameObject indicatorObject = new(indicatorName);
            indicatorObject.transform.SetParent(transform, false);
            indicatorObject.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = indicatorObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetIndicatorSprite();
            renderer.color = color;
            renderer.sortingOrder = DefaultSortingOrder + 1;
            renderer.enabled = false;
            return renderer;
        }

        private static void ConfigureIndicator(SpriteRenderer renderer, Color color, int sortingOrder)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.gameObject.SetActive(true);
            renderer.sprite ??= GetIndicatorSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private static Sprite GetIndicatorSprite()
        {
            if (indicatorSprite != null)
            {
                return indicatorSprite;
            }

            Texture2D texture = new(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            indicatorSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return indicatorSprite;
        }

        private void ApplyColors()
        {
            if (bulletBarView != null)
            {
                bulletBarView.SetColors(bulletColor, emptyColor);
            }

            ApplyIndicatorColor(lockOnRenderer, lockOnColor);
            ApplyIndicatorColor(executionRenderer, executionColor);
        }

        private static void ApplyIndicatorColor(SpriteRenderer renderer, Color color)
        {
            if (renderer != null)
            {
                renderer.color = color;
            }
        }

        private void SetBarsVisible(bool visible)
        {
            if (barsRoot != null)
            {
                barsRoot.gameObject.SetActive(visible);
            }
        }

        private void SetIndicatorsVisible(bool visible)
        {
            SetRendererEnabled(lockOnRenderer, visible && IsLockOnTarget());
            SetRendererEnabled(executionRenderer, visible && IsHoveredExecutionTarget());
        }

        private void ApplyWorldCenter(SpriteRenderer renderer)
        {
            if (renderer != null)
            {
                renderer.transform.position = transform.position;
            }
        }

        private static void SetRendererEnabled(SpriteRenderer renderer, bool enabled)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.gameObject.SetActive(true);
            renderer.enabled = enabled;
        }
    }
}
