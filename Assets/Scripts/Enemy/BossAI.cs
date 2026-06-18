using System.Collections.Generic;
using UnityEngine;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public abstract class BossAI : MonoBehaviour
    {
        [Header("Meta")]
        [Tooltip("상태 UI 등에 표시할 보스 이름입니다. 비워두면 오브젝트 이름을 사용합니다.")]
        [SerializeField] private string displayName;
        [Tooltip("이 보스가 사용할 공통 전투 이펙트와 색상 설정입니다.")]
        [SerializeField] private CombatEffectData effectData;

        [Header("Bullet")]
        [Tooltip("보스가 보유할 수 있는 최대 탄환 수입니다.")]
        [SerializeField, Min(1)] private int maxBullets = 60;
        [Tooltip("보스 탄환이 0이 되었을 때 처형 가능 상태를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float bulletEmptyExecutionSeconds = 3f;

        [Header("Color")]
        [Tooltip("기본 상태에서 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("보스 탄환이 0이 되었을 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color bulletEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("보스가 경직 상태일 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        [Header("Detection")]
        [Tooltip("플레이어를 감지할 수 있는 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float detectionRange = 9f;

        [Header("Movement")]
        [Tooltip("보스의 기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;

        [Header("Scene References")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform fireOrigin;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyStatusView statusView;
        [SerializeField] private AttackTimingOutline attackTimingOutline;
        [SerializeField] private GunRecoilMotion gunRecoil;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private SpriteRenderer lockOnIndicator;
        [SerializeField] private SpriteRenderer executionIndicator;

        [Header("Boss Combat UI")]
        [SerializeField] private GameObject bossCombatUiRoot;
        [SerializeField] private BulletBarView bossBulletBarView;

        [Header("Status UI")]
        [SerializeField] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color bulletBarColor = new(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private Color emptyBulletBarColor = Color.red;
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [SerializeField] private Color executionIndicatorColor = Color.red;

        private readonly List<EnemyProjectile> activeProjectiles = new();
        private Health health;
        private BulletGauge bullets;
        private SpriteRenderer[] renderers;
        private Transform player;
        private bool isBulletEmpty;
        private float bulletEmptyEndsAt;
        private bool isExecutionLocked;
        private bool isBodyHitColorActive;
        private float bodyHitColorEndsAt;
        private bool isStaggered;
        private float staggerEndsAt;
        private float staggerShakeDistance;
        private float staggerShakeFrequency;
        private Vector3 staggerBaseLocalPosition;
        private bool isBossCombatUiActive;
        private bool destroyAfterDeathQueued;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Health Health => health;
        public BulletGauge Bullets => bullets;
        public Transform Player => player;
        public Rigidbody2D Body => body;
        public Transform BodyRoot => bodyRoot;
        public Transform FireOrigin => fireOrigin;
        public Transform ProjectileOrigin => projectileOrigin;
        public GunRecoilMotion GunRecoil => gunRecoil;
        public LayerMask ObstacleMask => obstacleMask;
        public Vector3 SpawnPosition { get; private set; }
        public bool IsBulletEmpty => isBulletEmpty || (bullets != null && bullets.IsEmpty);
        public bool IsExecutionLocked => isExecutionLocked;
        public bool IsStaggered => isStaggered;
        public float DetectionRange => detectionRange;
        public float MoveSpeed => moveSpeed;
        public Color NormalColor => normalColor;
        public Color BulletEmptyColor => bulletEmptyColor;
        public Color StaggeredColor => staggeredColor;
        public Color BodyHitColor => effectData != null ? effectData.EnemyBodyHitColor : new Color(1f, 0.35f, 0.25f, 1f);
        public float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : 0.08f;
        public Color StatusBarBackgroundColor => statusBarBackgroundColor;
        public Color BulletBarColor => bulletBarColor;
        public Color EmptyBulletBarColor => emptyBulletBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;

        protected CombatEffectData EffectData => effectData;

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            bullets = GetComponent<BulletGauge>();

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            bodyRoot ??= FindChild("Visual") ?? transform;
            fireOrigin ??= FindChild("Gun") ?? bodyRoot;
            projectileOrigin ??= FindChild("FireOrigin") ?? FindChild("Muzzle") ?? fireOrigin;
            if (gunRecoil == null && fireOrigin != null)
            {
                gunRecoil = fireOrigin.GetComponentInChildren<GunRecoilMotion>();
            }

            lockOnIndicator ??= FindChild("LockOnIndicator")?.GetComponent<SpriteRenderer>();
            executionIndicator ??= FindChild("ExecutionIndicator")?.GetComponent<SpriteRenderer>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        protected virtual void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        protected virtual void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }
        }

        protected virtual void Start()
        {
            SpawnPosition = transform.position;
            bullets.Configure(maxBullets, true);

            PrepareStatusViews();
            ApplyBodyStateColor();
            ResolvePlayer();
            OnBossStarted();
        }

        protected virtual void Update()
        {
            UpdateBodyHitColor();
            UpdateStagger();

            if (health.IsDead)
            {
                HideAttackTiming();
                Stop();
                return;
            }

            if (isExecutionLocked)
            {
                HideAttackTiming();
                Stop();
                return;
            }

            if (IsBulletEmpty)
            {
                HideAttackTiming();
                TickBulletEmpty();
                return;
            }

            ResolvePlayer();
            TryActivateBossCombatUiOnCombatStart();
            RotateToTarget();
            OnBossTick();
        }

        public void SetExecutionLocked(bool locked)
        {
            isExecutionLocked = locked;
            ApplyBodyStateColor();

            if (locked)
            {
                CancelBossAction();
                Stop();
            }
        }

        public virtual bool ReceivePlayerHit(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (health == null || health.IsDead)
            {
                return false;
            }

            if (IsBulletEmpty)
            {
                health.Kill();
                QueueDestroyAfterDeath();
                return true;
            }

            if (TryHandlePlayerHitBeforeDamage(bulletDamage, strongHit, hitPosition, hitDirection, hitColor))
            {
                return true;
            }

            bullets.TrySpend(bulletDamage, BulletChangeSource.Hit);
            if (bullets.IsEmpty)
            {
                BeginBulletEmpty();
            }

            FlashBodyHitColor();
            PlayPlayerAttackImpact(hitPosition, hitDirection, hitColor);
            PlayEnemyHitCameraImpact(hitDirection);
            OnPlayerHitAfterDamage(bulletDamage, strongHit, hitPosition, hitDirection, hitColor);
            return true;
        }

        public bool IsPlayerDetected()
        {
            return player != null && Vector2.Distance(transform.position, player.position) <= detectionRange;
        }

        public bool CanSeePlayer()
        {
            if (player == null)
            {
                return false;
            }

            float distance = Vector2.Distance(transform.position, player.position);
            if (distance > detectionRange)
            {
                return false;
            }

            Vector2 direction = (player.position - transform.position).normalized;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleMask);
            return hit.collider == null;
        }

        public float DistanceToPlayer()
        {
            return player != null ? Vector2.Distance(transform.position, player.position) : float.MaxValue;
        }

        public void MoveToward(Vector2 target)
        {
            if (body == null)
            {
                return;
            }

            Vector2 direction = (target - (Vector2)transform.position).normalized;
            body.linearVelocity = direction * moveSpeed;
        }

        public void Stop()
        {
            if (body == null)
            {
                return;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        public void ShowAttackTiming(float remainingSeconds, float durationSeconds)
        {
            ShowAttackTiming(remainingSeconds, durationSeconds, 0, 0);
        }

        public void ShowAttackTiming(float remainingSeconds, float durationSeconds, int loadedBulletCount, int totalBulletCount)
        {
            if (remainingSeconds <= 0f || durationSeconds <= 0f)
            {
                ShowAttackBullets(loadedBulletCount, totalBulletCount);
                return;
            }

            EnsureAttackTimingOutline();
            attackTimingOutline.Show(remainingSeconds, durationSeconds, loadedBulletCount, totalBulletCount);
        }

        public void ShowAttackBullets(int loadedBulletCount, int totalBulletCount)
        {
            if (totalBulletCount <= 0)
            {
                HideAttackTiming();
                return;
            }

            EnsureAttackTimingOutline();
            attackTimingOutline.ShowBullets(loadedBulletCount, totalBulletCount);
        }

        public void HideAttackTiming()
        {
            if (attackTimingOutline != null)
            {
                attackTimingOutline.Hide();
            }
        }

        public bool CanSpawnEnemyProjectile()
        {
            return bullets != null && !bullets.IsEmpty;
        }

        public void RegisterActiveProjectile(EnemyProjectile projectile)
        {
            PruneInactiveProjectiles();
            if (projectile == null || activeProjectiles.Contains(projectile))
            {
                return;
            }

            activeProjectiles.Add(projectile);
        }

        public void UnregisterActiveProjectile(EnemyProjectile projectile)
        {
            if (projectile != null)
            {
                activeProjectiles.Remove(projectile);
            }
        }

        public Vector2 GetFacingDirection()
        {
            return bodyRoot != null ? (Vector2)bodyRoot.right : Vector2.right;
        }

        protected virtual void OnBossStarted() { }
        protected abstract void OnBossTick();
        protected virtual void CancelBossAction() { }
        protected virtual void OnBossDied() { }
        protected virtual void OnBulletEmptyBegan() { }
        protected virtual void OnBulletEmptyRecovered() { }
        protected virtual bool TryHandlePlayerHitBeforeDamage(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor) => false;
        protected virtual void OnPlayerHitAfterDamage(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor) { }

        protected EnemyProjectile SpawnBossProjectile(
            EnemyProjectile prefab,
            Vector3 position,
            Vector2 direction,
            int projectileBulletDamage,
            float chargeSeconds,
            float speed,
            float lifetime,
            float radius,
            Color color,
            float trailSeconds,
            float trailWidth,
            float homingSeconds,
            float homingTurnDegrees,
            bool playRecoil,
            Vector3? muzzleFlashPosition = null,
            float muzzleFlashScale = 0.9f)
        {
            if (!CanSpawnEnemyProjectile())
            {
                return null;
            }

            EnemyProjectile projectile = EnemyProjectile.Spawn(
                prefab,
                bullets,
                position,
                direction,
                projectileBulletDamage,
                chargeSeconds,
                speed,
                lifetime,
                radius,
                color,
                trailSeconds,
                trailWidth,
                homingSeconds,
                homingTurnDegrees);

            if (projectile == null)
            {
                return null;
            }

            if (muzzleFlashScale > 0f)
            {
                ProjectileVfx.PlayMuzzleFlash(muzzleFlashPosition ?? position, direction, color, muzzleFlashScale);
            }
            if (playRecoil)
            {
                gunRecoil?.Play(direction);
            }

            return projectile;
        }

        protected void BeginStagger(float seconds, float shakeDistance, float shakeFrequency)
        {
            if (seconds <= 0f || isExecutionLocked || IsBulletEmpty)
            {
                return;
            }

            if (!isStaggered)
            {
                staggerBaseLocalPosition = bodyRoot != null ? bodyRoot.localPosition : Vector3.zero;
            }

            isStaggered = true;
            staggerEndsAt = Time.time + seconds;
            staggerShakeDistance = Mathf.Max(0f, shakeDistance);
            staggerShakeFrequency = Mathf.Max(0f, shakeFrequency);
            ApplyBodyStateColor();
        }

        protected Vector2 GetPredictedPlayerPosition(Vector3 originPosition, float projectileSpeed, float leadPredictionSeconds)
        {
            if (player == null)
            {
                return originPosition;
            }

            Vector2 targetPosition = player.position;
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            if (playerBody == null || leadPredictionSeconds <= 0f || projectileSpeed <= 0f)
            {
                return targetPosition;
            }

            float distance = Vector2.Distance(originPosition, targetPosition);
            float travelSeconds = distance / projectileSpeed;
            float leadSeconds = Mathf.Min(leadPredictionSeconds, travelSeconds);
            return targetPosition + playerBody.linearVelocity * leadSeconds;
        }

        protected Vector2 GetDirectionToPlayer(Vector3 originPosition)
        {
            if (player == null)
            {
                return Vector2.left;
            }

            Vector2 direction = (Vector2)player.position - (Vector2)originPosition;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        protected static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private void ResolvePlayer()
        {
            if (player == null && PlayerCombatController.Active != null)
            {
                player = PlayerCombatController.Active.transform;
            }
        }

        private void RotateToTarget()
        {
            if (player == null || bodyRoot == null)
            {
                return;
            }

            Vector2 bodyDirection = (Vector2)(player.position - bodyRoot.position);
            RotateRight(bodyRoot, bodyDirection);

            if (fireOrigin != null && fireOrigin != bodyRoot)
            {
                Transform origin = projectileOrigin != null ? projectileOrigin : fireOrigin;
                Vector2 fireDirection = (Vector2)(player.position - origin.position);
                RotateRight(fireOrigin, fireDirection);
            }
        }

        private void PrepareStatusViews()
        {
            if (UsesBossCombatUi())
            {
                SuppressEnemyStatusView();
                PrepareBossCombatUi();
                BindBossCombatUiTargetsIfVisible();
                SetBossCombatUiVisible(false);
                return;
            }

            EnsureStatusView();
        }

        private void EnsureStatusView()
        {
            if (statusView == null)
            {
                statusView = GetComponentInChildren<EnemyStatusView>();
            }

            if (statusView == null)
            {
                statusView = gameObject.AddComponent<EnemyStatusView>();
            }

            statusView.SetSuppressed(false);
            statusView.SetIndicators(lockOnIndicator, executionIndicator);
            statusView.Configure(this);
            statusView.SetTargets(health, bullets);
        }

        private bool UsesBossCombatUi()
        {
            return bossCombatUiRoot != null || bossBulletBarView != null;
        }

        private void PrepareBossCombatUi()
        {
            if (bossCombatUiRoot != null)
            {
                bossBulletBarView ??= bossCombatUiRoot.GetComponentInChildren<BulletBarView>(true);
            }

            if (bossBulletBarView != null)
            {
                bossBulletBarView.SetBindPlayerOnStart(false);
                bossBulletBarView.SetColors(bulletBarColor, emptyBulletBarColor);
            }
        }

        private void BindBossCombatUiTargetsIfVisible()
        {
            if (IsBossCombatUiVisible())
            {
                bossBulletBarView?.SetTarget(bullets);
            }
        }

        private void TryActivateBossCombatUiOnCombatStart()
        {
            if (isBossCombatUiActive || !UsesBossCombatUi() || !IsPlayerDetected())
            {
                return;
            }

            PrepareBossCombatUi();
            SuppressEnemyStatusView();
            SetBossCombatUiVisible(true);
            bossBulletBarView?.SetTarget(bullets);
        }

        private bool IsBossCombatUiVisible()
        {
            if (bossCombatUiRoot != null)
            {
                return bossCombatUiRoot.activeInHierarchy;
            }

            return bossBulletBarView != null && bossBulletBarView.gameObject.activeInHierarchy;
        }

        private void SetBossCombatUiVisible(bool visible)
        {
            if (bossCombatUiRoot != null)
            {
                bossCombatUiRoot.SetActive(visible);
            }
            else if (bossBulletBarView != null)
            {
                bossBulletBarView.gameObject.SetActive(visible);
            }

            isBossCombatUiActive = visible;
        }

        private void SuppressEnemyStatusView()
        {
            EnemyStatusView rootStatusView = GetComponent<EnemyStatusView>() ?? gameObject.AddComponent<EnemyStatusView>();
            EnemyStatusView[] statusViews = GetComponentsInChildren<EnemyStatusView>(true);
            for (int i = 0; i < statusViews.Length; i++)
            {
                if (statusViews[i] != null && statusViews[i] != rootStatusView)
                {
                    statusViews[i].SetSuppressed(true);
                }
            }

            statusView = rootStatusView;
            statusView.SetIndicators(lockOnIndicator, executionIndicator);
            statusView.Configure(this);
            statusView.SetTargets(health, bullets);
            statusView.SetSuppressed(true);
        }

        private void PlayPlayerAttackImpact(Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            Color sparkColor = effectData != null ? effectData.AttackImpactSparkColor : Color.Lerp(hitColor, Color.white, 0.35f);
            Color backSparkColor = effectData != null ? effectData.AttackImpactBackSparkColor : Color.Lerp(hitColor, new Color(1f, 0.72f, 0.12f, 1f), 0.55f);
            Color flameColor = effectData != null ? effectData.AttackImpactFlameColor : backSparkColor;
            Color ringColor = effectData != null ? effectData.AttackImpactRingColor : Color.Lerp(hitColor, Color.white, 0.35f);
            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                sparkColor,
                backSparkColor,
                flameColor,
                ringColor,
                effectData != null ? effectData.AttackImpactSparkCount : 14,
                effectData != null ? effectData.AttackImpactBackSparkCount : 6,
                effectData != null ? effectData.AttackImpactFlameCount : 8,
                effectData != null ? effectData.AttackImpactEffectScale : 0.65f);
        }

        private void FlashBodyHitColor()
        {
            isBodyHitColorActive = true;
            bodyHitColorEndsAt = Time.time + BodyHitColorSeconds;
            ApplyBodyStateColor();
        }

        private void UpdateBodyHitColor()
        {
            if (!isBodyHitColorActive || Time.time < bodyHitColorEndsAt)
            {
                return;
            }

            isBodyHitColorActive = false;
            ApplyBodyStateColor();
        }

        private void UpdateStagger()
        {
            if (!isStaggered)
            {
                return;
            }

            Transform target = bodyRoot != null ? bodyRoot : transform;
            if (Time.time >= staggerEndsAt)
            {
                isStaggered = false;
                if (bodyRoot != null)
                {
                    bodyRoot.localPosition = staggerBaseLocalPosition;
                }

                ApplyBodyStateColor();
                return;
            }

            if (target == null || staggerShakeDistance <= 0f || staggerShakeFrequency <= 0f)
            {
                return;
            }

            float sign = Mathf.Sin(Time.time * staggerShakeFrequency * Mathf.PI * 2f) >= 0f ? 1f : -1f;
            Vector3 offset = Vector3.right * (staggerShakeDistance * sign);
            if (bodyRoot != null)
            {
                bodyRoot.localPosition = staggerBaseLocalPosition + offset;
            }
            else
            {
                target.position += offset * Time.deltaTime;
            }
        }

        private void BeginBulletEmpty()
        {
            if (isBulletEmpty || health.IsDead)
            {
                return;
            }

            isBulletEmpty = true;
            bulletEmptyEndsAt = Time.time + bulletEmptyExecutionSeconds;
            CancelBossAction();
            Stop();
            ApplyBodyStateColor();
            OnBulletEmptyBegan();
        }

        private void TickBulletEmpty()
        {
            if (!isBulletEmpty)
            {
                BeginBulletEmpty();
            }

            if (Time.time >= bulletEmptyEndsAt)
            {
                RecoverFromBulletEmpty();
                return;
            }

            Stop();
        }

        private void RecoverFromBulletEmpty()
        {
            if (health == null || health.IsDead || bullets == null)
            {
                return;
            }

            isBulletEmpty = false;
            bulletEmptyEndsAt = 0f;
            bullets.Restore(Mathf.Max(1, (bullets.MaxBullets + 2) / 3), BulletChangeSource.Generic);
            ApplyBodyStateColor();
            OnBulletEmptyRecovered();
        }

        private void ApplyBodyStateColor()
        {
            if (renderers == null)
            {
                return;
            }

            Color color = normalColor;
            if (isStaggered)
            {
                color = staggeredColor;
            }
            else if (isBodyHitColorActive)
            {
                color = BodyHitColor;
            }
            else if (isExecutionLocked || IsBulletEmpty)
            {
                color = bulletEmptyColor;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || IsStatusRenderer(renderers[i]) || IsUnderFireOrigin(renderers[i].transform))
                {
                    continue;
                }

                renderers[i].color = color;
            }
        }

        private bool IsStatusRenderer(SpriteRenderer renderer)
        {
            return statusView != null && statusView.OwnsRenderer(renderer);
        }

        private bool IsUnderFireOrigin(Transform target)
        {
            return fireOrigin != null
                && fireOrigin != bodyRoot
                && (target == fireOrigin || target.IsChildOf(fireOrigin));
        }

        private void HandleDied(Health _)
        {
            DestroyActiveProjectiles();
            OnBossDied();
        }

        private void DestroyActiveProjectiles()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] != null)
                {
                    activeProjectiles[i].DestroyFromOwner();
                }
            }

            activeProjectiles.Clear();
        }

        private void PruneInactiveProjectiles()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] == null)
                {
                    activeProjectiles.RemoveAt(i);
                }
            }
        }

        private void QueueDestroyAfterDeath()
        {
            if (destroyAfterDeathQueued || isExecutionLocked)
            {
                return;
            }

            destroyAfterDeathQueued = true;
            Destroy(gameObject);
        }

        private void EnsureAttackTimingOutline()
        {
            if (attackTimingOutline == null)
            {
                attackTimingOutline = GetComponent<AttackTimingOutline>() ?? gameObject.AddComponent<AttackTimingOutline>();
            }

            attackTimingOutline.SetTarget(bodyRoot);
        }

        private static void PlayEnemyHitCameraImpact(Vector2 direction)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, 0.08f, 0.12f, 0.05f);
        }

        private static void RotateRight(Transform target, Vector2 direction)
        {
            if (target == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Transform FindChild(string childName)
        {
            Transform found = transform.Find(childName);
            return found != null ? found : FindChildRecursive(transform, childName);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
