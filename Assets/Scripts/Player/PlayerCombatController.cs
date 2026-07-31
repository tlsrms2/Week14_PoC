using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;
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
        private const string ExecutionImageSfxId = "Execute";
        private static readonly Color InvulnerableAmmoRefillTint = new Color(1f, 0.72f, 0.04f, 1f);
        private const float InvulnerableAmmoRefillTintAmount = 0.45f;

        public static PlayerCombatController Active { get; private set; }
        public static bool IsExecutionCinematicActive => Active != null && Active.IsExecuting;

        // IsExecutionCinematicActive는 처형 컷신 재생 구간만 커버하고, 마지막 목숨을 끊는 처형은
        // 최종 샷이 꽂힌 뒤(IsExecuting이 이미 false로 꺼진 뒤) 사망 애니메이션 재생~결과 패널 표시
        // 직전까지 IsWaitingForVictoryPanel 구간이 이어진다. ESC 일시정지처럼 "처형 연출이 끝나기
        // 전에는 절대 끼어들면 안 되는" 용도는 이 둘을 합쳐서 봐야 한다.
        public static bool IsAnyExecutionInProgress =>
            Active != null && (Active.IsExecuting || Active.IsWaitingForVictoryPanel);
        private static int externalCombatPermissionCount;
        private static int leftAttackSuppressionCount;
        private static int parrySuppressionCount;
        private static int mouseParryReticleSuppressionCount;
        private static int pointerInputSuppressionCount;
        private static int externalInvulnerabilityCount;

        public static event Action<PlayerCombatController> AttackReceived;

        [SerializeField] private PlayerCombatConfig config;
        [SerializeField] private PlayerVisualRig visual;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform combatCenter;
        [SerializeField] private Transform leftGunOrigin;
        [SerializeField] private Transform leftGunFireOrigin;
        [SerializeField] private Transform rightGunFireOrigin;
        [SerializeField, Tooltip("야구배트 이펙트가 자식으로 생성될 위치입니다. 플레이어 프리팹의 앵커 Transform을 연결합니다.")]
        private Transform baseballBatVfxAnchor;
        [SerializeField] private LayerMask enemyMask = ~0;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private ExecutionImageEffect executionImage;
        [SerializeField] private PlayerHP playerHpView;
        [SerializeField, Min(0f)] private float finalDeathCameraReturnSeconds = 0.25f;
        [SerializeField, Tooltip("마우스 위치를 따라다닐 패링 조준선 SpriteRenderer입니다. 씬/프리팹에 직접 만든 오브젝트를 연결합니다.")]
        private SpriteRenderer mouseParryReticleRenderer;
        [SerializeField] private MouseParryReticle mouseParryReticle;
        [SerializeField] private SniperChargeLaserEffect sniperChargeLaserEffect;

        private Health health;
        private BulletGauge bullets;
        private CameraFollow2D cameraFollow;
        private Health lockOnTarget;
        private SpriteRenderer[] bodyRenderers;
        private Color[] bodyBaseColors;
        private Sprite[] bodyBaseSprites;
        private Vector3[] bodyBaseLocalPositions;
        private Quaternion[] bodyBaseLocalRotations;
        private Vector3[] bodyBaseLocalScales;
        private bool[] bodyBaseFlipX;
        private bool[] bodyBaseFlipY;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput playerInput;
#endif
        private PlayerCombatContext playerCombatContext;
        private PlayerCombatRig playerCombatRig;
        private PlayerDamageReceiver damageReceiver;
        private PlayerAimController aimController;
        private PlayerLockOnController lockOnController;
        private bool hasMouseParryReticleBaseColor;
        private Color mouseParryReticleBaseColor;
        private PlayerShooter shooter;
        private PlayerParryController parryController;
        private PlayerExecutionPresentation executionPresentation;
        private PlayerExecutionController executionController;
        private PlayerDashController dashController;
        private bool lockOnSuppressed = true;
        private int externalMovementLockCount;
        private int deathPreventionChargesRemaining;
        private float deathPreventionClearRadius;
        private float deathPreventionInvulnerabilitySeconds;
        private GameObject deathPreventionBlankVfxPrefab;
        private bool nextAttackDamageMultiplierArmed;
        private float nextAttackDamageMultiplier = 1f;
        private bool invulnerableAmmoRefillActive;
        private float invulnerableAmmoRefillDurationSeconds;
        private float moveSpeedMultiplier = 1f;
        private float weaponMoveSpeedMultiplier = 1f;

        public readonly struct PlayerAttackEchoInfo
        {
            public PlayerAttackEchoInfo(
                BaseWeaponSO weapon,
                int damage,
                float range,
                float reflectedProjectileSpeed,
                int ammoSpent)
            {
                Weapon = weapon;
                Damage = damage;
                Range = range;
                ReflectedProjectileSpeed = reflectedProjectileSpeed;
                AmmoSpent = ammoSpent;
            }

            public BaseWeaponSO Weapon { get; }
            public int Damage { get; }
            public float Range { get; }
            public float ReflectedProjectileSpeed { get; }
            public int AmmoSpent { get; }
        }

        public event Action<PlayerAttackEchoInfo> PlayerAttackPerformed;
        private Coroutine invulnerableAmmoRefillRoutine;
        private float invulnerableAmmoRefillParryClearRadius;
        private GameObject invulnerableAmmoRefillBlankVfxPrefab;
        private Action invulnerableAmmoRefillOnComplete;
        private float invulnerableAmmoRefillPostParryInvulnerabilitySeconds;

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
        public Transform LeftGunOrigin => Context.LeftGunOrigin;
        public Transform LeftFireOrigin => Rig.GetLeftFireOrigin();
        public Transform RightFireOrigin => Rig.GetRightFireOrigin();
        public bool IsReticleVisible => config != null
            && !GameModalState.BlocksGameplayInput
            && !IsPlayerControlLocked
            && health != null
            && !health.IsDead;
        public PlayerVisualRig Visual => Context.Visual;
        public CameraFollow2D CameraFollow => Context.CameraFollow;
        public Transform BodyRoot => Context.BodyRoot;
        public PlayerHP PlayerHpView => Context.PlayerHpView;
        public Health LockOnTarget => Context.LockOnTarget;
        public ExecutionTarget HoveredExecutionTarget => ExecutionController.HoveredExecutionTarget;
        public bool IsExecuting => ExecutionController.IsExecuting;
        public PlayerCombatConfig Config => Context.Config;
        public float MoveSpeedMultiplier => moveSpeedMultiplier * weaponMoveSpeedMultiplier;

        internal void PlayExecutionImageForCinematic(float secondsUntilKillMoment)
        {
            executionImage?.Play(
                Mathf.Max(0f, secondsUntilKillMoment),
                () => SoundManager.PlaySfx(ExecutionImageSfxId));
        }

        public bool CanMove => CanAct && !IsExternallyMovementLocked && !IsBodyContactStaggered && !IsDashing;
        public bool IsBodyContactStaggered => DamageReceiver.IsBodyContactStaggered;
        public bool IsDashing => DashController.IsDashing;
        public bool IsExternallyMovementLocked => externalMovementLockCount > 0;
        public bool ShouldStopMovementWhenBlocked => (!IsBodyContactStaggered && !IsDashing)
            || IsPlayerControlLocked
            || IsExternallyMovementLocked;
        private bool CanAct => !GameModalState.BlocksGameplayInput
            && !IsPlayerControlLocked
            && !health.IsDead;
        private bool CanShoot => CanAct && (BossAI.IsAnyCombatStarted || externalCombatPermissionCount > 0);
        private static bool IsLeftAttackSuppressed => leftAttackSuppressionCount > 0;
        private static bool IsParrySuppressed => parrySuppressionCount > 0;
        internal static bool IsMouseParryReticleSuppressed => mouseParryReticleSuppressionCount > 0;
        private static bool IsPointerInputSuppressed => pointerInputSuppressionCount > 0;
        private bool IsPlayerControlLocked => IsExecuting
            || BossAI.IsAnyFinalDeathSequencePlaying
            || IsWaitingForVictoryPanel;
        internal static bool IsExternallyInvulnerable => externalInvulnerabilityCount > 0;
        private bool IsWaitingForVictoryPanel => ExecutionController.IsWaitingForVictoryPanel;

        public void PushExternalMovementLock()
        {
            externalMovementLockCount++;
        }

        public void PopExternalMovementLock()
        {
            externalMovementLockCount = Mathf.Max(0, externalMovementLockCount - 1);
        }

        public static void PushExternalCombatPermission()
        {
            externalCombatPermissionCount++;
        }

        public static void PopExternalCombatPermission()
        {
            externalCombatPermissionCount = Mathf.Max(0, externalCombatPermissionCount - 1);
        }

        public static void PushLeftAttackSuppression()
        {
            leftAttackSuppressionCount++;
        }

        public static void PopLeftAttackSuppression()
        {
            leftAttackSuppressionCount = Mathf.Max(0, leftAttackSuppressionCount - 1);
        }

        public static void PushParrySuppression()
        {
            parrySuppressionCount++;
        }

        public static void PopParrySuppression()
        {
            parrySuppressionCount = Mathf.Max(0, parrySuppressionCount - 1);
        }

        public static void PushPointerInputSuppression()
        {
            pointerInputSuppressionCount++;
        }

        public static void PopPointerInputSuppression()
        {
            pointerInputSuppressionCount = Mathf.Max(0, pointerInputSuppressionCount - 1);
        }

        public static void PushMouseParryReticleSuppression()
        {
            mouseParryReticleSuppressionCount++;
            Active?.SetMouseParryReticleVisible(false);
        }

        public static void PopMouseParryReticleSuppression()
        {
            mouseParryReticleSuppressionCount = Mathf.Max(0, mouseParryReticleSuppressionCount - 1);
        }

        public static void PushExternalInvulnerability()
        {
            externalInvulnerabilityCount++;
        }

        public static void PopExternalInvulnerability()
        {
            externalInvulnerabilityCount = Mathf.Max(0, externalInvulnerabilityCount - 1);
        }

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
            public Transform RightGunFireOrigin
            {
                get => controller.rightGunFireOrigin;
                internal set => controller.rightGunFireOrigin = value;
            }
            public Transform BaseballBatVfxAnchor => controller.baseballBatVfxAnchor != null
                ? controller.baseballBatVfxAnchor
                : CombatCenterOrigin;
            public void HideBaseballBatDisplay() => controller.Shooter.HideBaseballBatDisplay();
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
            public SniperChargeLaserEffect SniperChargeLaserEffect => controller.sniperChargeLaserEffect;
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
            public Sprite[] BodyBaseSprites
            {
                get => controller.bodyBaseSprites;
                internal set => controller.bodyBaseSprites = value;
            }
            public Vector3[] BodyBaseLocalPositions
            {
                get => controller.bodyBaseLocalPositions;
                internal set => controller.bodyBaseLocalPositions = value;
            }
            public Quaternion[] BodyBaseLocalRotations
            {
                get => controller.bodyBaseLocalRotations;
                internal set => controller.bodyBaseLocalRotations = value;
            }
            public Vector3[] BodyBaseLocalScales
            {
                get => controller.bodyBaseLocalScales;
                internal set => controller.bodyBaseLocalScales = value;
            }
            public bool[] BodyBaseFlipX
            {
                get => controller.bodyBaseFlipX;
                internal set => controller.bodyBaseFlipX = value;
            }
            public bool[] BodyBaseFlipY
            {
                get => controller.bodyBaseFlipY;
                internal set => controller.bodyBaseFlipY = value;
            }
            public float FinalDeathCameraReturnSeconds => controller.finalDeathCameraReturnSeconds;
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
            CancelActiveCharge();
            externalMovementLockCount = 0;

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
            visual?.EndExecutionVisual();
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

        public void SetMoveSpeedMultiplier(float multiplier)
        {
            moveSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public void SetWeaponMoveSpeedMultiplier(float multiplier)
        {
            weaponMoveSpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        private void Update()
        {
            UpdateCursorPresentation();

            if (health.IsDead)
            {
                CancelActiveCharge();
                StopBody();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetLockOnTarget(null);
                SetHoveredExecutionTarget(null);
                return;
            }

            if (IsExecuting)
            {
                CancelActiveCharge();
                StopBody();
                DamageReceiver.UpdateBodyColor();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                return;
            }

            if (IsPlayerControlLocked || GameModalState.BlocksGameplayInput)
            {
                CancelActiveCharge();
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

            if (IsPointerInputSuppressed)
            {
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                if (Shooter.IsCharging)
                {
                    Shooter.EndCharge();
                }

                UpdateBodyColor();
                UpdateDashAutoParry();
                return;
            }

            ClearInvalidLockOnTarget();
            UpdateLockOnTarget();
            UpdateHoveredExecutionTarget();
            RotateToAim();
            Shooter.UpdateBaseballBatDisplay();
            UpdateMouseParryRangeRecovery();
            bool isParrySuppressed = IsParrySuppressed;
            UpdateMouseParryReticle();
            UpdateProjectileLockOnTarget();
            UpdateMouseParryReticleThreat();
            UpdateProjectileLockOnIndicator();

            UpdateBodyColor();
            UpdateDashAutoParry();

            bool isLeftAttackSuppressed = IsLeftAttackSuppressed;
            if (isLeftAttackSuppressed && Shooter.IsCharging)
            {
                Shooter.EndCharge();
            }

            if (!isLeftAttackSuppressed && GameInput.LeftAttackDown && CanAct)
            {
                if (!TryBeginExecution() && CanShoot)
                {
                    Shooter.BeginAttack();
                }
            }

            if (!isLeftAttackSuppressed && GameInput.LeftAttackHeld && CanShoot)
            {
                Shooter.HoldAttack(Time.deltaTime);
            }

            if (!isLeftAttackSuppressed && GameInput.LeftAttackUp)
            {
                Shooter.ReleaseAttack();
            }

            if (!isParrySuppressed && GameInput.RightAttackDown)
            {
                if (!CanAct)
                {
                    Debug.LogWarning($"[Player] 패링 입력 차단됨: Modal={GameModalState.BlocksGameplayInput}, IsExecuting={IsExecuting}, FinalDeath={BossAI.IsAnyFinalDeathSequencePlaying}, WaitingVictory={IsWaitingForVictoryPanel}, Dead={health.IsDead}");
                }
                else if (Shooter.IsCharging)
                {
                }
                else if (!TryParryProjectile())
                {
                    ApplyMouseParryMissPenalty();
                }
            }
        }

        private void CancelActiveCharge()
        {
            if (shooter?.IsCharging == true)
            {
                shooter.EndCharge();
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

        internal void TryReceiveEnemyBodyContact(Collider2D other, Vector2 hitPosition)
        {
            DamageReceiver.TryReceiveEnemyBodyContact(other, hitPosition);
        }

        public bool ReceiveAttack(int bulletDamage, Vector3 hitPosition, Vector2 hitDirection)
        {
            return DamageReceiver.ReceiveAttack(bulletDamage, hitPosition, hitDirection);
        }

        public void ApplyExternalKnockback(Vector2 direction, float speed, float staggerSeconds)
        {
            DamageReceiver.ApplyExternalKnockback(direction, speed, staggerSeconds);
        }

        public void FlashBodyColor(Color color, float seconds)
        {
            DamageReceiver.FlashBodyColor(color, seconds);
        }

        internal void BeginExecutionBodyColor()
        {
            DamageReceiver.BeginExecutionBodyColor();
        }

        public void SetHackerParryVisual(bool hacked)
        {
            Rig.ResolveMouseParryReticleReference();
            SpriteRenderer renderer = Context.MouseParryReticleRenderer;
            if (renderer != null)
            {
                if (!hasMouseParryReticleBaseColor)
                {
                    mouseParryReticleBaseColor = renderer.color;
                    hasMouseParryReticleBaseColor = true;
                }

                renderer.color = hacked ? new Color(1f, 0.12f, 0.08f, 1f) : mouseParryReticleBaseColor;
            }

            Context.MouseParryReticle?.SetHacked(hacked);
        }

        internal void NotifyAttackReceived()
        {
            AttackReceived?.Invoke(this);
        }

        private void UpdateBodyColor(bool force = false)
        {
            DamageReceiver.UpdateBodyColor(force);
            ApplyInvulnerableAmmoRefillTint(force);
        }

        public void PlayParryImpact(Vector3 position)
        {
            ParryController.PlayParryImpact(position);
        }

        public void PlayParryImpact(Vector3 position, Vector2 direction)
        {
            ParryController.PlayParryImpact(position, direction);
        }

        public void PlayParryImpact(Vector3 position, Vector2 direction, bool restoreBullets)
        {
            ParryController.PlayParryImpact(position, direction, restoreBullets);
        }

        public void PlayReloadAnimation()
        {
            visual?.PlayReload();
        }

        public bool TryDash(float distance, float duration, float autoParryRadius)
        {
            return DashController.TryDash(distance, duration, autoParryRadius);
        }

        public bool TryDash(
            float distance,
            float duration,
            float autoParryRadius,
            RollSkillVfxSettings vfxSettings)
        {
            return DashController.TryDash(distance, duration, autoParryRadius, vfxSettings);
        }

        public bool TryDash(float distance, float duration)
        {
            return TryDash(distance, duration, 0f);
        }

        // charges는 씬(재도전)마다 ApplyPassive로 다시 채워집니다(누적되지 않고 항상 이 값으로 수렴).
        public void ConfigureDeathPrevention(int charges, float clearRadius, float invulnerabilitySeconds, GameObject blankVfxPrefab)
        {
            deathPreventionChargesRemaining = Mathf.Max(0, charges);
            deathPreventionClearRadius = Mathf.Max(0f, clearRadius);
            deathPreventionInvulnerabilitySeconds = Mathf.Max(0f, invulnerabilitySeconds);
            deathPreventionBlankVfxPrefab = blankVfxPrefab;
        }

        public void ClearDeathPrevention()
        {
            deathPreventionChargesRemaining = 0;
            deathPreventionBlankVfxPrefab = null;
        }

        // 사망 판정을 대체할 수 있으면 소모하고 true를 반환합니다. PlayerDamageReceiver가
        // health.Kill()을 부르기 직전에 호출해서, Died 이벤트(게임오버 UI 등)가 아예 뜨지 않게 막습니다.
        internal bool TryConsumeDeathPrevention(Vector2 position)
        {
            if (deathPreventionChargesRemaining <= 0)
            {
                return false;
            }

            deathPreventionChargesRemaining--;
            SoundManager.PlaySfx(GameplaySfxIds.ModuleEmergency);

            if (deathPreventionClearRadius > 0f)
            {
                ParryController.AutoParryProjectilesNear(position, deathPreventionClearRadius);
                PlayBlankVfx(deathPreventionBlankVfxPrefab, position);
            }

            if (deathPreventionInvulnerabilitySeconds > 0f)
            {
                StartCoroutine(TemporaryInvulnerabilityRoutine(deathPreventionInvulnerabilitySeconds));
            }

            return true;
        }

        private static IEnumerator TemporaryInvulnerabilityRoutine(float seconds)
        {
            PushExternalInvulnerability();
            yield return new WaitForSeconds(seconds);
            PopExternalInvulnerability();
        }

        // 대시(구르기)의 자동 패링과 동일한 통로입니다. center 반경 안의 요격 가능한 적 투사체를
        // 흡수(파괴)하고, 실제로 흡수한 개수를 반환합니다.
        public int AutoParryProjectilesNear(Vector2 center, float radius, RollSkillVfxSettings vfxSettings)
        {
            return ParryController.AutoParryProjectilesNear(center, radius, vfxSettings);
        }

        // 야구방망이가 반사 불가(요격 전용) 투사체를 때렸을 때 마우스 즉시 패링과 동일한 성공 처리를 타도록
        // PlayerShooter가 호출하는 통로입니다.
        internal bool TryParryProjectileForMelee(EnemyProjectile target)
        {
            return ParryController.TryParryProjectileForMelee(target);
        }

        internal bool TryParryProjectileForCinematic(EnemyProjectile target)
        {
            return ParryController.TryParryProjectileForCinematic(target);
        }

        internal bool TryParryProjectileForCinematic(
            EnemyProjectile target,
            bool playPresentation)
        {
            return ParryController.TryParryProjectileForCinematic(
                target,
                playPresentation);
        }

        // 다음으로 성공하는 공격 1회(무기 종류 무관: 권총 한 발, 샷건 한 발의 전체 펠릿, 스나이퍼 차지샷 1회)에만
        // 배율을 적용하고 자동으로 해제됩니다. PlayerShooter의 각 발사 지점(TryShootEnemy/FireSpread/FireSingle)이
        // 공격이 실제로 나가는 걸 확정한 시점에 ConsumeNextAttackDamageMultiplier를 호출해서 소모합니다.
        public void ArmNextAttackDamageMultiplier(float multiplier)
        {
            nextAttackDamageMultiplierArmed = multiplier > 1f;
            nextAttackDamageMultiplier = Mathf.Max(1f, multiplier);
        }

        internal void NotifyPlayerAttackPerformed(
            int damage,
            float range = 0f,
            float reflectedProjectileSpeed = 0f,
            int ammoSpent = 0)
        {
            if (damage > 0)
            {
                BaseWeaponSO weapon = WeaponLoadoutManager.Instance != null ? WeaponLoadoutManager.Instance.CurrentWeapon : null;
                PlayerAttackPerformed?.Invoke(new PlayerAttackEchoInfo(
                    weapon,
                    damage,
                    range,
                    reflectedProjectileSpeed,
                    ammoSpent));
            }
        }

        internal float ConsumeNextAttackDamageMultiplier()
        {
            if (!nextAttackDamageMultiplierArmed)
            {
                return 1f;
            }

            nextAttackDamageMultiplierArmed = false;
            return nextAttackDamageMultiplier;
        }

        public bool FireSkillProjectile(int damage, float sizeMultiplier, Color color)
        {
            return Shooter.TryFireSkillProjectile(damage, sizeMultiplier, color);
        }

        // seconds 동안 외부 무적(PushExternalInvulnerability)을 유지하면서, 그동안 무적 때문에 막힌 피격이
        // 처음 한 번 발생하면(NotifyInvulnerableHit) 탄환을 최대치로 채우고, parryClearRadius 안의 적 투사체를
        // 제거하며, 히트스탑 + 카메라 임팩트를 재생한 뒤 — 남은 무적 시간을 기다리지 않고 그 즉시 종료합니다.
        // 종료 직후에는 원래의 긴 무적 대신 postParryInvulnerabilitySeconds만큼 짧은 일반 무적을 잠깐 부여해서,
        // 무적이 꺼지는 그 순간 같은 프레임에 몰린 다른 공격에 바로 맞아버리는 걸 막아줍니다.
        // onComplete는 그렇게 조기 종료되는 시점이나, 한 번도 안 맞고 지속시간이 다 지난 시점에 정확히 한 번 호출됩니다
        // (쿨타임 지연 시작용 콜백 등으로 쓰임).
        public void BeginInvulnerableAmmoRefill(
            float seconds,
            float parryClearRadius,
            float postParryInvulnerabilitySeconds,
            GameObject blankVfxPrefab,
            Action onComplete)
        {
            if (invulnerableAmmoRefillRoutine != null)
            {
                StopCoroutine(invulnerableAmmoRefillRoutine);
                FinishInvulnerableAmmoRefill();
            }

            invulnerableAmmoRefillParryClearRadius = Mathf.Max(0f, parryClearRadius);
            invulnerableAmmoRefillPostParryInvulnerabilitySeconds = Mathf.Max(0f, postParryInvulnerabilitySeconds);
            invulnerableAmmoRefillBlankVfxPrefab = blankVfxPrefab;
            invulnerableAmmoRefillOnComplete = onComplete;
            invulnerableAmmoRefillDurationSeconds = Mathf.Max(0f, seconds);
            invulnerableAmmoRefillActive = true;
            PushExternalInvulnerability();
            UpdateBodyColor(true);
            RestartInvulnerableAmmoRefillTimer();
        }

        internal void NotifyInvulnerableHit(Vector3 hitPosition, Vector2 hitDirection)
        {
            if (!invulnerableAmmoRefillActive)
            {
                return;
            }

            if (Bullets != null)
            {
                Bullets.Restore(Bullets.MaxBullets, BulletChangeSource.Parry);
            }

            Vector3 clearCenter = Context.CombatCenterOrigin.position;
            PlayBlankVfx(invulnerableAmmoRefillBlankVfxPrefab, clearCenter);

            if (invulnerableAmmoRefillParryClearRadius > 0f)
            {
                ParryController.AutoParryProjectilesNear(clearCenter, invulnerableAmmoRefillParryClearRadius);
            }

            DamageReceiver.PlayHitStop();
            CameraFollow?.PlayImpact(hitDirection, 0.32f, 0.24f, 0.22f);

            // 패링은 한 번 성공하면 그걸로 끝 — 남은 무적 시간을 기다리지 않고 즉시 종료한다.
            if (invulnerableAmmoRefillRoutine != null)
            {
                StopCoroutine(invulnerableAmmoRefillRoutine);
            }

            float postParryInvulnerabilitySeconds = invulnerableAmmoRefillPostParryInvulnerabilitySeconds;
            float postParryClearRadius = invulnerableAmmoRefillParryClearRadius;
            CompleteInvulnerableAmmoRefill();

            // 스킬의 긴 무적 대신, 무적이 꺼지는 순간 몰린 다른 공격에 바로 맞지 않도록 짧은 무적을 이어서 부여한다.
            // 대쉬처럼 몸이 계속 겹쳐 있는 공격은 이 구간에도 매 프레임 다시 부딪히므로, 그동안 새로 들어온
            // 탄도 계속 쓸어주지 않으면 첫 히트 때만 지워지고 그 이후로 겹쳐 있는 동안 들어온 탄은 안 지워진다.
            if (postParryInvulnerabilitySeconds > 0f)
            {
                StartCoroutine(PostParryInvulnerabilityRoutine(postParryInvulnerabilitySeconds, postParryClearRadius));
            }
        }

        private IEnumerator PostParryInvulnerabilityRoutine(float seconds, float clearRadius)
        {
            PushExternalInvulnerability();
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (clearRadius > 0f)
                {
                    ParryController.AutoParryProjectilesNear(Context.CombatCenterOrigin.position, clearRadius);
                }

                yield return null;
                remaining -= Time.deltaTime;
            }

            PopExternalInvulnerability();
        }

        private void RestartInvulnerableAmmoRefillTimer()
        {
            if (invulnerableAmmoRefillRoutine != null)
            {
                StopCoroutine(invulnerableAmmoRefillRoutine);
                invulnerableAmmoRefillRoutine = null;
            }

            invulnerableAmmoRefillRoutine = StartCoroutine(InvulnerableAmmoRefillRoutine(invulnerableAmmoRefillDurationSeconds));
        }

        private IEnumerator InvulnerableAmmoRefillRoutine(float seconds)
        {
            yield return new WaitForSeconds(seconds);

            CompleteInvulnerableAmmoRefill();
        }

        private void CompleteInvulnerableAmmoRefill()
        {
            FinishInvulnerableAmmoRefill();
            Action onComplete = invulnerableAmmoRefillOnComplete;
            invulnerableAmmoRefillOnComplete = null;
            onComplete?.Invoke();
        }

        private void FinishInvulnerableAmmoRefill()
        {
            if (!invulnerableAmmoRefillActive)
            {
                return;
            }

            invulnerableAmmoRefillActive = false;
            invulnerableAmmoRefillRoutine = null;
            invulnerableAmmoRefillBlankVfxPrefab = null;
            invulnerableAmmoRefillDurationSeconds = 0f;
            invulnerableAmmoRefillPostParryInvulnerabilitySeconds = 0f;
            PopExternalInvulnerability();
            UpdateBodyColor(true);
        }

        private void ApplyInvulnerableAmmoRefillTint(bool force = false)
        {
            if (!invulnerableAmmoRefillActive)
            {
                return;
            }

            SpriteRenderer[] renderers = Context.BodyRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color targetColor = Color.Lerp(renderer.color, InvulnerableAmmoRefillTint, InvulnerableAmmoRefillTintAmount);
                targetColor.a = renderer.color.a;
                if (force || renderer.color != targetColor)
                {
                    renderer.color = targetColor;
                }
            }
        }

        private void PlayBlankVfx(GameObject blankVfxPrefab, Vector3 position)
        {
            if (blankVfxPrefab == null)
            {
                return;
            }

            GameObject instance = Instantiate(blankVfxPrefab, position, Quaternion.identity);
            ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sortingOrder = Mathf.Max(renderers[i].sortingOrder, 74);
                }
            }

            float lifetimeSeconds = 0.1f;
            ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                if (particle == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = particle.main;
                lifetimeSeconds = Mathf.Max(lifetimeSeconds, main.duration + main.startLifetime.constantMax);
                particle.Play(true);
            }

            Destroy(instance, lifetimeSeconds);
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
            if (GameModalState.BlocksGameplayInput || CursorController.IsForceVisible)
            {
                return;
            }

            bool gameplayMouseActive = config != null
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

            Vector2 autoParryCenter = Context.CombatCenterOrigin.position;
            ParryController.AutoParryProjectilesNear(
                autoParryCenter,
                DashController.AutoParryRadius,
                DashController.VfxSettings);
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
