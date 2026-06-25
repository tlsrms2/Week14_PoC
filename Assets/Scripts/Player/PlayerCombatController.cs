using UnityEngine;
using Week14.Bootstrap;
using Week14.Enemy;
using Week14.Input;
using Week14.UI;
using Week14.Weapons;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Combat
{
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        public static PlayerCombatController Active { get; private set; }
        public static bool IsExecutionCinematicActive => Active != null && Active.IsExecuting;

        [SerializeField] private PlayerCombatConfig config;
        [SerializeField] private PlayerVisualRig visual;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform combatCenter;
        [SerializeField] private Transform leftGunOrigin;
        [SerializeField] private Transform leftGunFireOrigin;
        [SerializeField] private LayerMask enemyMask = ~0;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private ExecutionImageEffect executionImage;
        [SerializeField] private PlayerHP playerHpView;
        [SerializeField, Min(0f)] private float finalDeathCameraReturnSeconds = 0.25f;
        [SerializeField, Min(0f), Tooltip("보스 사망 연출이 끝난 후 게임승리 패널이 뜨기까지의 대기 시간입니다.")]
        private float victoryPanelDelaySeconds = 0f;
        [SerializeField, Tooltip("마우스 위치를 따라다닐 패링 조준선 SpriteRenderer입니다. 씬/프리팹에 직접 만든 오브젝트를 연결합니다.")]
        private SpriteRenderer mouseParryReticleRenderer;
        [SerializeField] private MouseParryReticle mouseParryReticle;

        private Health health;
        private BulletGauge bullets;
        private CameraFollow2D cameraFollow;
        private Health lockOnTarget;
        private SpriteRenderer[] bodyRenderers;
        private Color[] bodyBaseColors;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput playerInput;
#endif
        private PlayerCombatContext playerCombatContext;
        private PlayerCombatRig playerCombatRig;
        private PlayerDamageReceiver damageReceiver;
        private PlayerAimController aimController;
        private PlayerLockOnController lockOnController;
        private PlayerShooter shooter;
        private PlayerParryController parryController;
        private PlayerExecutionPresentation executionPresentation;
        private PlayerExecutionController executionController;
        private PlayerDashController dashController;
        private bool lockOnSuppressed;

        internal PlayerCombatContext Context => playerCombatContext ??= new PlayerCombatContext(this);
        private PlayerCombatRig Rig => playerCombatRig ??= new PlayerCombatRig(Context);
        private PlayerDamageReceiver DamageReceiver => damageReceiver ??= new PlayerDamageReceiver(Context);
        private PlayerAimController AimController => aimController ??= new PlayerAimController(Context);
        private PlayerLockOnController LockOnController => lockOnController ??= new PlayerLockOnController(Context, AimController);
        private PlayerShooter Shooter => shooter ??= new PlayerShooter(Context, AimController);
        private PlayerParryController ParryController => parryController ??= new PlayerParryController(Context, AimController, Rig);
        private PlayerExecutionPresentation ExecutionPresentation => executionPresentation ??= new PlayerExecutionPresentation(Context);
        private PlayerExecutionController ExecutionController => executionController ??= new PlayerExecutionController(
            Context,
            Rig,
            AimController,
            LockOnController,
            ExecutionPresentation);
        private PlayerDashController DashController => dashController ??= new PlayerDashController(Context);

        public Health Health => Context.Health;
        public BulletGauge Bullets => Context.Bullets;
        public Health LockOnTarget => Context.LockOnTarget;
        public ExecutionTarget HoveredExecutionTarget => ExecutionController.HoveredExecutionTarget;
        public bool IsExecuting => ExecutionController.IsExecuting;
        public PlayerCombatConfig Config => Context.Config;
        public bool CanMove => CanAct && !IsBodyContactStaggered && !IsDashing;
        public bool IsBodyContactStaggered => DamageReceiver.IsBodyContactStaggered;
        public bool IsDashing => DashController.IsDashing;
        public bool ShouldStopMovementWhenBlocked => (!IsBodyContactStaggered && !IsDashing)
            || IsPlayerControlLocked;
        private bool CanAct => !GameModalState.BlocksGameplayInput
            && !IsPlayerControlLocked
            && !health.IsDead;
        private bool IsPlayerControlLocked => IsExecuting
            || BossAI.IsAnyFinalDeathSequencePlaying
            || IsWaitingForVictoryPanel;
        private bool IsWaitingForVictoryPanel => ExecutionController.IsWaitingForVictoryPanel;

        internal sealed class PlayerCombatContext
        {
            private readonly PlayerCombatController controller;

            internal PlayerCombatContext(PlayerCombatController controller)
            {
                this.controller = controller;
            }

            public PlayerCombatController Owner => controller;
            public MonoBehaviour CoroutineHost => controller;
            public GameObject PlayerGameObject => controller.gameObject;
            public Transform PlayerTransform => controller.transform;
            public PlayerCombatConfig Config => controller.config;
            public PlayerVisualRig Visual => controller.visual;
            public Transform BodyRoot
            {
                get => controller.bodyRoot;
                internal set => controller.bodyRoot = value;
            }
            public Transform CombatCenter
            {
                get => controller.combatCenter;
                internal set => controller.combatCenter = value;
            }
            public Transform CombatCenterOrigin => controller.combatCenter != null
                ? controller.combatCenter
                : (controller.bodyRoot != null ? controller.bodyRoot : controller.transform);
            public Transform LeftGunOrigin
            {
                get => controller.leftGunOrigin;
                internal set => controller.leftGunOrigin = value;
            }
            public Transform LeftGunFireOrigin
            {
                get => controller.leftGunFireOrigin;
                internal set => controller.leftGunFireOrigin = value;
            }
            public LayerMask EnemyMask => controller.enemyMask;
            public Rigidbody2D Body => controller.body;
            public Health Health => controller.health;
            public BulletGauge Bullets => controller.bullets;
            public Health LockOnTarget
            {
                get => controller.lockOnTarget;
                internal set => controller.lockOnTarget = value;
            }
            public CameraFollow2D CameraFollow => controller.GetCameraFollow();
            public ExecutionImageEffect ExecutionImage => controller.executionImage;
            public PlayerHP PlayerHpView
            {
                get => controller.playerHpView;
                internal set => controller.playerHpView = value;
            }
            public SpriteRenderer MouseParryReticleRenderer => controller.mouseParryReticleRenderer;
            public MouseParryReticle MouseParryReticle
            {
                get => controller.mouseParryReticle;
                internal set => controller.mouseParryReticle = value;
            }
            public SpriteRenderer[] BodyRenderers
            {
                get => controller.bodyRenderers;
                internal set => controller.bodyRenderers = value;
            }
            public Color[] BodyBaseColors
            {
                get => controller.bodyBaseColors;
                internal set => controller.bodyBaseColors = value;
            }
            public float FinalDeathCameraReturnSeconds => controller.finalDeathCameraReturnSeconds;
            public float VictoryPanelDelaySeconds => controller.victoryPanelDelaySeconds;
            public bool IsExecuting => controller.IsExecuting;
            public bool IsDashing => controller.IsDashing;
            public bool IsWaitingForVictoryPanel => controller.IsWaitingForVictoryPanel;
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            bullets = GetComponent<BulletGauge>();

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

#if ENABLE_INPUT_SYSTEM
            BindPlayerInput();
#endif
            ResolveRigReferences();
            ResolveMouseParryReticleReference();
            CacheMouseParryReticleBaseScale();
            CacheBodyRenderers();

            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }
        }

#if ENABLE_INPUT_SYSTEM
        private void BindPlayerInput()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            if (playerInput == null)
            {
                playerInput = GetComponentInParent<PlayerInput>();
            }

            if (playerInput == null)
            {
                playerInput = GetComponentInChildren<PlayerInput>();
            }

            GameInput.Bind(playerInput);
        }
#endif

        private void OnEnable()
        {
            Active = this;
#if ENABLE_INPUT_SYSTEM
            BindPlayerInput();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            GameInput.Unbind(playerInput);
#endif

            if (Active == this)
            {
                Active = null;
            }

            CameraFollow2D activeCamera = GetCameraFollow();
            if (activeCamera != null)
            {
                activeCamera.EndCinematicFocus();
                activeCamera.SetFocusTarget(null);
            }

            SetMouseParryReticleVisible(false);
            SetProjectileLockOnIndicatorVisible(false);
            RestorePlayerHpAfterExecution();
            StopExecutionShotDim();
            executionImage?.Stop();
            DamageReceiver.StopHitStop();
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatController)} requires {nameof(PlayerCombatConfig)}.", this);
                return;
            }

            BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null ? WeaponLoadoutManager.Instance.CurrentWeapon : null;
            bullets.Configure(weapon != null ? weapon.MaxAmmo : config.MaxBullets, true);
        }

        public void SetConfig(PlayerCombatConfig nextConfig)
        {
            config = nextConfig;
        }

        private void Update()
        {
            UpdateCursorPresentation();

            if (health.IsDead)
            {
                StopBody();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetLockOnTarget(null);
                SetHoveredExecutionTarget(null);
                return;
            }

            if (IsExecuting)
            {
                StopBody();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                return;
            }

            if (IsPlayerControlLocked || GameModalState.BlocksGameplayInput)
            {
                StopBody();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                SetLockOnTarget(null);
                return;
            }

            if (config == null)
            {
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                return;
            }

            ClearInvalidLockOnTarget();
            UpdateLockOnTarget();
            UpdateHoveredExecutionTarget();
            RotateToAim();
            UpdateMouseParryRangeRecovery();
            UpdateMouseParryReticle();
            UpdateProjectileLockOnTarget();
            UpdateMouseParryReticleThreat();
            UpdateProjectileLockOnIndicator();
            UpdateBodyColor();
            UpdateDashAutoParry();

            if (GameInput.LeftAttackDown && CanAct)
            {
                if (!TryBeginExecution())
                {
                    TryShootEnemy();
                }
            }

            if (GameInput.RightAttackDown)
            {
                if (!CanAct)
                {
                    Debug.LogWarning($"[Player] 패링 입력 차단됨: Modal={GameModalState.BlocksGameplayInput}, IsExecuting={IsExecuting}, FinalDeath={BossAI.IsAnyFinalDeathSequencePlaying}, WaitingVictory={IsWaitingForVictoryPanel}, Dead={health.IsDead}");
                }
                else if (!TryParryProjectile())
                {
                    ApplyMouseParryMissPenalty();
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 hitPosition = collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position);
            TryReceiveEnemyBodyContact(collision.collider, hitPosition);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 hitPosition = collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position);
            TryReceiveEnemyBodyContact(collision.collider, hitPosition);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryReceiveEnemyBodyContact(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryReceiveEnemyBodyContact(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        public bool ReceiveAttack(int bulletDamage)
        {
            return DamageReceiver.ReceiveAttack(bulletDamage);
        }

        private void TryReceiveEnemyBodyContact(Collider2D other, Vector2 hitPosition)
        {
            DamageReceiver.TryReceiveEnemyBodyContact(other, hitPosition);
        }

        public bool ReceiveAttack(int bulletDamage, Vector3 hitPosition, Vector2 hitDirection)
        {
            return DamageReceiver.ReceiveAttack(bulletDamage, hitPosition, hitDirection);
        }

        private void UpdateBodyColor(bool force = false)
        {
            DamageReceiver.UpdateBodyColor(force);
        }

        public void PlayParryImpact(Vector3 position)
        {
            ParryController.PlayParryImpact(position);
        }

        public void PlayParryImpact(Vector3 position, Vector2 direction)
        {
            ParryController.PlayParryImpact(position, direction);
        }

        public void PlayReloadAnimation()
        {
            visual?.PlayReload();
        }

        public bool TryDash(float distance, float duration, float autoParryRadius)
        {
            return DashController.TryDash(distance, duration, autoParryRadius);
        }

        public bool FireSkillProjectile(int damage, float sizeMultiplier, Color color)
        {
            return Shooter.TryFireSkillProjectile(damage, sizeMultiplier, color);
        }

        private bool TryShootEnemy()
        {
            return Shooter.TryShootEnemy();
        }

        private bool TryBeginExecution()
        {
            return ExecutionController.TryBeginExecution();
        }

        private void UpdateHoveredExecutionTarget()
        {
            ExecutionController.UpdateHoveredExecutionTarget();
        }

        private void RestorePlayerHpAfterExecution()
        {
            ExecutionController.RestorePlayerHpAfterExecution();
        }

        private void StopExecutionShotDim()
        {
            ExecutionController.StopExecutionShotDim();
        }

        private bool TryParryProjectile()
        {
            return ParryController.TryParryProjectile();
        }

        private void UpdateLockOnTarget()
        {
            if (lockOnSuppressed)
            {
                LockOnController.SetLockOnTarget(null);
                return;
            }

            LockOnController.UpdateLockOnTarget();
        }

        public void SetLockOnSuppressed(bool suppressed)
        {
            lockOnSuppressed = suppressed;
        }

        private void ClearInvalidLockOnTarget()
        {
            LockOnController.ClearInvalidLockOnTarget();
        }

        private void SetLockOnTarget(Health nextTarget)
        {
            LockOnController.SetLockOnTarget(nextTarget);
        }

        private CameraFollow2D GetCameraFollow()
        {
            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }

            return cameraFollow;
        }

        private void RotateToAim()
        {
            AimController.RotateToAim();
        }

        private void UpdateProjectileLockOnTarget()
        {
            ParryController.UpdateProjectileLockOnTarget();
        }

        private void ApplyMouseParryMissPenalty()
        {
            ParryController.ApplyMouseParryMissPenalty();
        }

        private void UpdateMouseParryRangeRecovery()
        {
            ParryController.UpdateMouseParryRangeRecovery();
        }

        private void CacheMouseParryReticleBaseScale()
        {
            ParryController.CacheMouseParryReticleBaseScale();
        }

        private void UpdateMouseParryReticle()
        {
            ParryController.UpdateMouseParryReticle();
        }

        private void UpdateMouseParryReticleThreat()
        {
            ParryController.UpdateMouseParryReticleThreat();
        }

        private bool TryGetMouseParryDiamondCorners(out Vector3 top, out Vector3 right, out Vector3 bottom, out Vector3 left)
        {
            return ParryController.TryGetMouseParryDiamondCorners(out top, out right, out bottom, out left);
        }

        private void UpdateCursorPresentation()
        {
            bool gameplayMouseActive = config != null
                && !GameModalState.BlocksGameplayInput
                && !IsExecuting
                && health != null
                && !health.IsDead;

            Cursor.visible = !gameplayMouseActive;
            Cursor.lockState = CursorLockMode.None;
        }

        private void SetMouseParryReticleVisible(bool visible)
        {
            ParryController.SetMouseParryReticleVisible(visible);
        }

        private void ResolveMouseParryReticleReference()
        {
            Rig.ResolveMouseParryReticleReference();
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetMouseParryDiamondCorners(out Vector3 top, out Vector3 right, out Vector3 bottom, out Vector3 left))
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.48f, 0f, 0.95f);
            Gizmos.DrawLine(top, right);
            Gizmos.DrawLine(right, bottom);
            Gizmos.DrawLine(bottom, left);
            Gizmos.DrawLine(left, top);
            Gizmos.DrawLine(top, bottom);
            Gizmos.DrawLine(left, right);
        }

        private void UpdateProjectileLockOnIndicator()
        {
            ParryController.UpdateProjectileLockOnIndicator();
        }

        private void UpdateDashAutoParry()
        {
            if (!IsDashing)
            {
                return;
            }

            ParryController.AutoParryProjectilesNear(Context.CombatCenterOrigin.position, DashController.AutoParryRadius);
        }

        private void SetProjectileLockOnIndicatorVisible(bool visible)
        {
            ParryController.SetProjectileLockOnIndicatorVisible(visible);
        }

        private void SetHoveredExecutionTarget(ExecutionTarget nextTarget)
        {
            ExecutionController.SetHoveredExecutionTarget(nextTarget);
        }

        private void ResolveRigReferences()
        {
            Rig.ResolveReferences();
        }

        private void CacheBodyRenderers()
        {
            Rig.CacheBodyRenderers();
        }

        private void StopBody()
        {
            Rig.StopBody();
        }

    }
}
