using UnityEngine;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyStatusView : MonoBehaviour
    {
        private const int DefaultSortingOrder = 40;
        private const float ExecutionIndicatorRotationSpeedDegrees = 180f;
        private static Sprite indicatorSprite;

        [SerializeField, HideInInspector] private Health health;
        [SerializeField, HideInInspector] private ExecutionTarget executionTarget;
        [SerializeField, HideInInspector] private EnemyAI enemyAI;
        [SerializeField, HideInInspector] private BossAI bossAI;
        [SerializeField, HideInInspector] private Drone drone;
        [SerializeField, HideInInspector] private SpriteRenderer lockOnRenderer;
        [SerializeField, HideInInspector] private SpriteRenderer executionRenderer;

        private Transform worldTarget;
        private Color lockOnColor = Color.white;
        private Color executionColor = Color.red;
        private float executionIndicatorAngle;

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

            lockOnColor = data.LockOnIndicatorColor;
            executionColor = data.ExecutionIndicatorColor;

            EnsureView();
            ApplyColors();
        }

        public void Configure(BossAI boss)
        {
            if (boss == null)
            {
                return;
            }

            bossAI = boss;
            lockOnColor = boss.LockOnIndicatorColor;
            executionColor = boss.ExecutionIndicatorColor;

            EnsureView();
            ApplyColors();
        }

        public void Configure(Drone nextDrone)
        {
            if (nextDrone == null)
            {
                return;
            }

            drone = nextDrone;
            executionTarget = nextDrone.GetComponent<ExecutionTarget>();
            lockOnColor = nextDrone.LockOnIndicatorColor;
            executionColor = nextDrone.ExecutionIndicatorColor;

            EnsureView();
            ApplyColors();
        }

        public void SetWorldTarget(Transform target)
        {
            worldTarget = target;
        }

        public void SetTarget(Health nextHealth)
        {
            health = nextHealth;
            EnsureView();
        }

        public bool OwnsRenderer(SpriteRenderer renderer)
        {
            return renderer != null && (renderer == lockOnRenderer || renderer == executionRenderer);
        }

        public void SetSuppressed(bool value)
        {
            _ = value;
        }

        private void Awake()
        {
            health ??= GetComponentInParent<Health>();
            executionTarget ??= GetComponentInParent<ExecutionTarget>();
            enemyAI ??= GetComponentInParent<EnemyAI>();
            bossAI ??= GetComponentInParent<BossAI>();
            drone ??= GetComponentInParent<Drone>();

            EnsureView();
            SetTarget(health);
            ApplyColors();
        }

        private void LateUpdate()
        {
            bool alive = health != null && !health.IsDead;
            bool canShowDuringEnemyState = (enemyAI == null || !enemyAI.IsExecutionLocked)
                && (bossAI == null || !bossAI.IsExecutionLocked)
                && (drone == null || !drone.IsExecutionLocked);
            bool indicatorsVisible = alive && canShowDuringEnemyState;

            Vector3 center = GetWorldCenter();
            if (!indicatorsVisible)
            {
                SetIndicatorsVisible(false);
                return;
            }

            ApplyOwnedWorldCenter(lockOnRenderer, center);
            ApplyOwnedWorldCenter(executionRenderer, center);

            bool executionVisible = IsHoveredExecutionTarget();
            SetRendererEnabled(lockOnRenderer, !executionVisible && IsLockOnTarget());
            SetRendererEnabled(executionRenderer, executionVisible);
            RotateExecutionIndicator(executionVisible);
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
            if (enemyAI != null && targetEnemy == enemyAI)
            {
                return true;
            }

            BossAI targetBoss = player.LockOnTarget.GetComponent<BossAI>()
                ?? player.LockOnTarget.GetComponentInParent<BossAI>();
            if (bossAI != null && targetBoss == bossAI)
            {
                return true;
            }

            Drone targetDrone = player.LockOnTarget.GetComponent<Drone>()
                ?? player.LockOnTarget.GetComponentInParent<Drone>();
            return drone != null && targetDrone == drone;
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
            bossAI ??= GetComponentInParent<BossAI>();
            drone ??= GetComponentInParent<Drone>();
            EnsureIndicators();
        }

        private void EnsureIndicators()
        {
            lockOnRenderer ??= FindIndicator("LockOnIndicator") ?? CreateIndicator("LockOnIndicator", lockOnColor, 0.52f);
            executionRenderer ??= FindIndicator("ExecutionIndicator") ?? CreateIndicator("ExecutionIndicator", executionColor, 0.42f);
            ConfigureOwnedIndicator(lockOnRenderer, lockOnColor, DefaultSortingOrder + 1);
            ConfigureOwnedIndicator(executionRenderer, executionColor, DefaultSortingOrder + 2);
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

        private void ConfigureOwnedIndicator(SpriteRenderer renderer, Color color, int sortingOrder)
        {
            if (renderer == null || !IsOwnedIndicator(renderer))
            {
                return;
            }

            renderer.gameObject.SetActive(true);
            renderer.sprite ??= GetIndicatorSprite();
            if (!ShouldKeepAuthoredIndicatorColor(renderer))
            {
                renderer.color = color;
            }
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
            ApplyOwnedIndicatorColor(lockOnRenderer, lockOnColor);
            ApplyOwnedIndicatorColor(executionRenderer, executionColor);
        }

        private void ApplyOwnedIndicatorColor(SpriteRenderer renderer, Color color)
        {
            if (renderer != null && IsOwnedIndicator(renderer) && !ShouldKeepAuthoredIndicatorColor(renderer))
            {
                renderer.color = color;
            }
        }

        private bool ShouldKeepAuthoredIndicatorColor(SpriteRenderer renderer)
        {
            return renderer == lockOnRenderer;
        }

        private void SetIndicatorsVisible(bool visible)
        {
            SetRendererEnabled(lockOnRenderer, visible && IsLockOnTarget());
            SetRendererEnabled(executionRenderer, visible && IsHoveredExecutionTarget());
        }

        private Vector3 GetWorldCenter()
        {
            return worldTarget != null ? worldTarget.position : transform.position;
        }

        private void ApplyOwnedWorldCenter(SpriteRenderer renderer, Vector3 center)
        {
            if (renderer != null && IsOwnedIndicator(renderer))
            {
                renderer.transform.position = center;
                renderer.transform.rotation = Quaternion.identity;
            }
        }

        private bool IsOwnedIndicator(SpriteRenderer renderer)
        {
            return renderer != null && renderer.transform.IsChildOf(transform);
        }

        private void RotateExecutionIndicator(bool visible)
        {
            if (!visible || executionRenderer == null)
            {
                return;
            }

            executionIndicatorAngle = Mathf.Repeat(
                executionIndicatorAngle + ExecutionIndicatorRotationSpeedDegrees * Time.deltaTime,
                360f);
            executionRenderer.transform.rotation = Quaternion.Euler(0f, 0f, executionIndicatorAngle);
        }

        private static void SetRendererEnabled(SpriteRenderer renderer, bool enabled)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = enabled;
        }
    }
}
