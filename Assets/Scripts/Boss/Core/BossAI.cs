using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Save;
using Week14.Skills;
using Week14.UI;

namespace Week14.Enemy
{
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public abstract partial class BossAI : MonoBehaviour, IMinionPatternHost
    {
        [Header("Boss Lives")]
        [Tooltip("보스의 총 목숨(페이즈) 수입니다. 처형될 때마다 1씩 깎입니다.")]
        [SerializeField, Min(1)] private int maxLives = 3;
        [Tooltip("처형 후 다음 페이즈 패턴을 시작하기 전, 보스가 제자리에서 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float phaseTransitionWaitSeconds = 1.2f;

        [Header("Death Sequence")]
        [Tooltip("비워두면 BodyRoot 밑의 Animator를 전부 찾아 사망 트리거를 동일하게 보냅니다(애니메이터가 여러 개인 보스도 자동 지원). 특정 애니메이터 하나에만 보내고 싶을 때만 직접 지정하세요.")]
        [SerializeField] private Animator deathAnimator;
        private Animator[] deathAnimators;
        [SerializeField] private string deathTriggerName = "Die";
        [SerializeField, Min(0f)] private float finalDeathExplosionSeconds = 1.4f;
        [SerializeField, Min(1)] private int finalDeathExplosionCount = 10;
        [Tooltip("지정하면 절차적 스파크/연기 이펙트 대신 이 프리팹을 각 폭발 위치에 생성합니다.")]
        [SerializeField] private GameObject finalDeathExplosionPrefab;
        [Tooltip("프리팹으로 생성된 폭발 오브젝트가 파괴되기까지의 시간(초)입니다.")]
        [SerializeField, Min(0.05f)] private float finalDeathExplosionPrefabLifetimeSeconds = 1.2f;
        [Tooltip("비워두면 BodyRoot를 기준으로 사용합니다. 폭발 스폰 범위의 중심을 직접 지정하고 싶을 때 사용합니다.")]
        [SerializeField] private Transform finalDeathExplosionAreaCenter;
        [Tooltip("0보다 크면 스프라이트 바운드 대신 이 반경의 원형 범위 안에서 폭발 위치를 랜덤으로 고릅니다.")]
        [SerializeField, Min(0f)] private float finalDeathExplosionAreaRadius = 0f;
        [Tooltip("폭발 연출이 모두 끝난 뒤 사망 애니메이션(Die 트리거)을 걸기까지 대기하는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float deathExplosionToAnimationDelaySeconds = 0f;
        [SerializeField, Min(0f)] private float deathAnimationFallbackSeconds = 1f;

        [Header("Meta")]
        [Tooltip("로비의 BossData 에셋입니다. 클리어 시 이 데이터의 ID로 ClearBoss를 기록하고, UnlocksBossIds/UnlocksSkillIds/UnlocksPassiveSkillIds/UnlocksWeaponIds에 등록된 ID들을 자동으로 해금합니다.")]
        [SerializeField] private BossData bossData;
        [Tooltip("상태 UI 등에 표시할 보스 이름입니다. 비워두면 오브젝트 이름을 사용합니다.")]
        [SerializeField] private string displayName;
        [Tooltip("이 보스가 사용할 공통 전투 이펙트 설정입니다.")]
        [SerializeField] private CombatEffectData effectData;
        [Tooltip("모든 보스가 공유할 색상과 상태 UI 색상 설정입니다.")]
        [SerializeField] private BossColorSettings colorSettings;

        [Header("HP")]
        [Tooltip("보스가 보유할 수 있는 최대 HP입니다.")]
        [FormerlySerializedAs("maxBullets")]
        [SerializeField, Min(1)] private int maxHp = 150;
        [Tooltip("보스 HP가 0이 되었을 때 처형 가능 상태를 유지하는 시간입니다.")]
        [FormerlySerializedAs("bulletEmptyExecutionSeconds")]
        [SerializeField, Min(0f)] private float hpEmptyExecutionSeconds = 3f;

        [Tooltip("보스가 피격당했을 때 흰색으로 깜빡이는 플래시 효과의 색입니다.")]
        [SerializeField] private Color hitFlashColor = Color.white;
        [Tooltip("보스가 피격당했을 때 플래시 효과가 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float hitFlashSeconds = 0.08f;
        [Tooltip("기본 상태에서 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField, HideInInspector] private Color normalColor = Color.white;
        [Tooltip("보스 HP가 0이 되었을 때 보스 스프라이트에 적용할 색입니다.")]
        [FormerlySerializedAs("bulletEmptyColor")]
        [SerializeField, HideInInspector] private Color hpEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("보스가 경직 상태일 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField, HideInInspector] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        [Header("Movement")]
        [Tooltip("보스의 기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [Tooltip("프레임 스파이크 중 직접 위치 이동을 여러 번 쪼갤 때 한 번에 처리할 최대 시간입니다.")]
        [SerializeField, Min(0.001f)] private float maxDirectMoveStepSeconds = 0.02f;

        [Header("Scene References")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyStatusView statusView;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private SpriteRenderer lockOnIndicator;
        [SerializeField] private SpriteRenderer executionIndicator;

        [Header("Boss Combat UI")]
        [SerializeField] private GameObject bossCombatUiRoot;
        [FormerlySerializedAs("bossBulletBarView")]
        [SerializeField] private BossBulletBarView bossHpBarView;
        [SerializeField] private BossLivesView bossLivesView;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private TMP_Text bossElapsedTimeText;

        [SerializeField, HideInInspector] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [FormerlySerializedAs("bulletBarColor")]
        [SerializeField, HideInInspector] private Color hpBarColor = new(1f, 0.55f, 0.1f, 1f);
        [FormerlySerializedAs("emptyBulletBarColor")]
        [SerializeField, HideInInspector] private Color emptyHpBarColor = Color.red;
        [SerializeField, HideInInspector] private Color lockOnIndicatorColor = Color.white;
        [SerializeField, HideInInspector] private Color executionIndicatorColor = Color.red;

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        private readonly BossProjectileTracker projectileTracker = new();
        private Health health;
        private BulletGauge hpGauge;
        private SpriteRenderer[] renderers;
        private Collider2D[] groundProbeColliders;
        private Collider2D[] physicsColliders;
        private Vector2 requestedMovementVelocity;
        private Vector2 lastAppliedMovementVelocity;
        private bool hasRequestedMovementVelocity;
        private Collider2D[] playerPhysicsColliders;
        private bool isIgnoringPlayerCollision;
        private bool isAutomaticDashContactDamageSuppressed;
        private MaterialPropertyBlock hitFlashPropertyBlock;
        private Transform player;
        private bool isExecutionLocked;
        private bool isBodyHitColorActive;
        private float bodyHitColorEndsAt;
        private bool isHitFlashActive;
        private float hitFlashEndsAt;
        private bool isStaggered;
        private float staggerEndsAt;
        private float staggerShakeDistance;
        private float staggerShakeFrequency;
        private Vector3 staggerBaseLocalPosition;
        private bool isBossCombatUiActive;
        private bool destroyAfterDeathQueued;
        private bool finalDeathSequencePlayed;
        private bool isFinalDeathSequencePlaying;
        private BossPhaseController phaseController;
        private BossStateMachine stateMachine;
        private static int finalDeathSequencePlayCount;
        private bool combatStartedCounted;
        private static int combatStartedCount;
        private float combatStartedAt;
        private float? frozenCombatElapsedSeconds;
        private int combatStartLockCount;
        private bool latestClearTimeWasNewRecord;
        private bool bossDeathSfxPlayed;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Health Health => health;
        public BulletGauge HpGauge => hpGauge;
        public BulletGauge Bullets => hpGauge;
        public Transform Player => player;
        public Rigidbody2D Body => body;
        public Transform BodyRoot => bodyRoot;
        public SpriteRenderer[] BodyRenderers => renderers;
        public LayerMask ObstacleMask => obstacleMask;
        public Vector3 SpawnPosition { get; private set; }
        public bool IsHpEmpty => hpGauge != null && hpGauge.IsEmpty;
        public bool IsBulletEmpty => IsHpEmpty;
        public bool IsExecutionLocked => isExecutionLocked;
        public bool IsFinalDeathSequencePlaying => isFinalDeathSequencePlaying;
        public virtual bool IsDashing => false;
        public virtual bool SuppressesBodyContactDamage => false;
        public bool IsStaggered => isStaggered;
        public float MoveSpeed => moveSpeed;
        public Color NormalColor => ActiveColorSettings != null ? ActiveColorSettings.NormalColor : normalColor;
        public Color HpEmptyColor => ActiveColorSettings != null ? ActiveColorSettings.HpEmptyColor : hpEmptyColor;
        public Color BulletEmptyColor => HpEmptyColor;
        public Color StaggeredColor => ActiveColorSettings != null ? ActiveColorSettings.StaggeredColor : staggeredColor;
        public Color BodyHitColor => ActiveEffectData != null ? ActiveEffectData.EnemyBodyHitColor : new Color(1f, 0.35f, 0.25f, 1f);
        public float BodyHitColorSeconds => ActiveEffectData != null ? ActiveEffectData.BodyHitColorSeconds : 0.08f;
        internal GameObject EnemyHitVfxPrefab => ActiveEffectData != null ? ActiveEffectData.EnemyHitVfxPrefab : null;
        internal GameObject EnemyMuzzleFlashVfxPrefab => BossMuzzleFlashVfxPrefab;
        public Color StatusBarBackgroundColor => ActiveColorSettings != null ? ActiveColorSettings.StatusBarBackgroundColor : statusBarBackgroundColor;
        public Color HpBarColor => ActiveColorSettings != null ? ActiveColorSettings.HpBarColor : hpBarColor;
        public Color EmptyHpBarColor => ActiveColorSettings != null ? ActiveColorSettings.EmptyHpBarColor : emptyHpBarColor;
        public Color BulletBarColor => HpBarColor;
        public Color EmptyBulletBarColor => EmptyHpBarColor;
        public Color LockOnIndicatorColor => ActiveColorSettings != null ? ActiveColorSettings.LockOnIndicatorColor : lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => ActiveColorSettings != null ? ActiveColorSettings.ExecutionIndicatorColor : executionIndicatorColor;

        protected CombatEffectData EffectData => ActiveEffectData;
        protected virtual GameObject BossMuzzleFlashVfxPrefab => null;
        protected virtual BossGraphAsset GraphAsset => null;
        protected virtual BossProjectileSettings ResolveGraphProjectileSettings(string projectileName) => null;
        // 페이즈에 ForcedPatternId가 지정돼 있을 때, 지금 그 패턴을 무조건 다음 패턴으로 강제 선택할지
        // 여부를 보스마다 다르게 판단하게 하는 훅이다(예: Assassin의 "단검이 일정 개수 이상 쌓였는지").
        protected virtual bool ShouldUseForcedGraphPattern() => false;
        public int MaxLives => Mathf.Max(1, maxLives);
        public int CurrentLives => PhaseController.CurrentLives;
        public int CurrentPhaseIndex => PhaseController.CurrentPhaseIndex;
        public int CurrentPhaseNumber => PhaseController.CurrentPhaseNumber;
        public bool IsCombatStarted => PhaseController.IsCombatStarted;
        public float CombatElapsedSeconds => frozenCombatElapsedSeconds
            ?? (combatStartedCounted ? Mathf.Max(0f, Time.time - combatStartedAt) : 0f);
        public bool LatestClearTimeWasNewRecord => latestClearTimeWasNewRecord;
        public event Action<int, int> LivesChanged;
        public static event Action<BossAI> CombatStarted;
        public static event Action<BossAI> Defeated;
        public static bool IsAnyFinalDeathSequencePlaying => finalDeathSequencePlayCount > 0;
        public static bool IsAnyCombatStarted => combatStartedCount > 0;
        public BossData BossData => bossData;
        public RectTransform BossCombatUiRect => bossCombatUiRoot != null
            ? bossCombatUiRoot.transform as RectTransform
            : bossHpBarView != null ? bossHpBarView.transform as RectTransform : null;

        private BossPhaseController PhaseController => phaseController ??= new BossPhaseController(this);
        private CombatEffectData ActiveEffectData => GraphAsset != null && GraphAsset.EffectData != null ? GraphAsset.EffectData : effectData;
        private BossColorSettings ActiveColorSettings => GraphAsset != null && GraphAsset.ColorSettings != null ? GraphAsset.ColorSettings : colorSettings;
        private BossGraphPhase ActiveGraphPhase => GraphAsset != null ? GraphAsset.GetPhase(CurrentPhaseIndex) : null;
        private bool BossCanFlyOverGround => ActiveGraphPhase != null && ActiveGraphPhase.BossCanFlyOverGround;
        public bool MinionsCanFlyOverGround => ActiveGraphPhase != null && ActiveGraphPhase.MinionsCanFlyOverGround;
        internal bool ShouldUseForcedGraphPatternForRunner()
        {
            return ShouldUseForcedGraphPattern();
        }

        internal BossProjectileSettings ResolveGraphProjectileSettingsForActions(string projectileName)
        {
            return ResolveGraphProjectileSettings(projectileName);
        }

        protected virtual void Awake()
        {
            ProjectilePool.EnsureScenePool();
            health = GetComponent<Health>();
            hpGauge = GetComponent<BulletGauge>();

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                // 질량을 무겁게 만드는 것만으로는 플레이어가 몸을 붙이고 있을 때 Box2D의 겹침
                // 위치 보정(velocity와 무관하게 body.position을 직접 밀어냄)을 막지 못한다.
                // 보스 이동은 전부 SetMovementVelocity/TryMovePatternTowards처럼 velocity·position을
                // 직접 대입하는 방식이라 Kinematic으로 바꿔도 그대로 동작하면서, 외부 충돌에 의한
                // 밀림 자체가 물리적으로 불가능해진다.
                body.bodyType = RigidbodyType2D.Kinematic;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            bodyRoot ??= FindChild("Visual") ?? transform;

            lockOnIndicator ??= FindChild("LockOnIndicator")?.GetComponent<SpriteRenderer>();
            executionIndicator ??= FindChild("ExecutionIndicator")?.GetComponent<SpriteRenderer>();
            renderers = bodyRoot != null
                ? bodyRoot.GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponentsInChildren<SpriteRenderer>(true);
            groundProbeColliders = GetComponentsInChildren<Collider2D>(true);
            phaseController = new BossPhaseController(this);
            stateMachine = new BossStateMachine(this);
        }

        protected virtual void OnEnable()
        {
            if (health != null)
            {
                health.Died += HandleDied;
            }

            if (bossData != null)
            {
                LoadoutSelectedSkillPanelLocalization.BindLocalizedString(bossData.LocalizedBossName, bossData.HasLocalizedBossName, SetBossNameText);
            }
        }

        protected virtual void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }

            if (bossData != null)
            {
                LoadoutSelectedSkillPanelLocalization.UnbindLocalizedString(bossData.LocalizedBossName, bossData.HasLocalizedBossName, SetBossNameText);
            }

            DisableMinionPatternHost();
            SetFinalDeathSequencePlaying(false);
            SetIgnorePlayerCollision(false);

            if (combatStartedCounted)
            {
                combatStartedCounted = false;
                combatStartedCount = Mathf.Max(0, combatStartedCount - 1);
            }
        }

        protected virtual void Start()
        {
            PhaseController.Initialize();
            
            SpawnPosition = transform.position;
            hpGauge.Configure(GetCurrentPhaseMaxHp(), true);

            PrepareStatusViews();
            ApplyBodyStateColor();
            ResolvePlayer();
            InitializeMinionPatternHost();
            OnBossStarted();
        }

        protected virtual void Update()
        {
            stateMachine ??= new BossStateMachine(this);
            stateMachine.Tick();
            TickDashContactForState();
            ApplyCombatTimerSlowCorrection();
            RefreshElapsedTimeText();
        }

        protected virtual void FixedUpdate()
        {
            ApplyRequestedMovement();
        }

        // CombatElapsedSeconds는 Time.time - combatStartedAt으로 계산되는 절대 시각 기반 타이머라
        // EnemyTimeScale의 영향을 받지 않는다. 슬로우 배율만큼 못 흐른 시간을 시작 시각에 계속
        // 더해 밀어내면(DeltaTimeDebt), 경과시간 자체가 슬로우 배율에 맞춰 천천히 늘어난다.
        private void ApplyCombatTimerSlowCorrection()
        {
            if (combatStartedCounted && frozenCombatElapsedSeconds == null)
            {
                combatStartedAt += EnemyTimeScale.DeltaTimeDebt;
            }
        }

        public static string FormatCombatTime(float seconds)
        {
            return TimeSpan.FromSeconds(Mathf.Max(0f, seconds)).ToString(@"mm\:ss\:ff");
        }

        private void RefreshElapsedTimeText()
        {
            if (bossElapsedTimeText == null)
            {
                return;
            }

            bossElapsedTimeText.text = combatStartedCounted ? FormatCombatTime(CombatElapsedSeconds) : "--:--:--";
        }

        public void PlayExecutionBarDrain()
        {
            bossHpBarView?.PlayExecutionDrain();
        }

        public void PushCombatStartLock()
        {
            combatStartLockCount++;
        }

        public void PopCombatStartLock()
        {
            combatStartLockCount = Mathf.Max(0, combatStartLockCount - 1);
        }

        public bool TryStartCombatFromIntro()
        {
            if (combatStartLockCount > 0 || health == null || health.IsDead)
            {
                return false;
            }

            ResolvePlayer();
            PhaseController.TryStartCombat(true);
            TryActivateBossCombatUiOnCombatStart();
            return IsCombatStarted;
        }

        public void ShowBossCombatUiForIntro()
        {
            if (!UsesBossCombatUi())
            {
                return;
            }

            PrepareBossCombatUi();
            SuppressEnemyStatusView();
            SetBossCombatUiVisible(true);
            bossHpBarView?.SetTarget(hpGauge);
        }

        public void HideBossCombatUiForFinalDeath()
        {
            SetBossCombatUiVisible(false);
            if (bossLivesView != null)
            {
                bossLivesView.gameObject.SetActive(false);
            }
        }

        public void SetExecutionLocked(bool locked)
        {
            isExecutionLocked = locked;
            ApplyBodyStateColor();

            if (locked)
            {
                CancelBossAction();
                CancelMinionPatternAction();
                Stop();
                EnemyProjectile.DestroyAllActive();
            }

            OnExecutionLockChanged(locked);
        }

        protected virtual void OnExecutionLockChanged(bool locked) { }

        public virtual bool ReceivePlayerHit(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (health == null || health.IsDead)
            {
                return false;
            }

            if (!IsCombatStarted)
            {
                return false;
            }

            if (IsHpEmpty)
            {
                // 그로기 상태에서는 총격으로 목숨이 깎이지 않고 이펙트만 재생됩니다.
                // 페이즈 전환은 오직 '처형 연출'이 끝났을 때 외부에서 TryConsumeLife()를 호출하여 처리합니다.
                PlayEnemyHitVfx(hitPosition, hitDirection);
                PlayEnemyHitCameraImpact(hitDirection);
                return true;
            }

            if (TryHandlePlayerHitBeforeDamage(bulletDamage, strongHit, hitPosition, hitDirection, hitColor))
            {
                return true;
            }

            hpGauge.TrySpend(bulletDamage, BulletChangeSource.Hit);

            FlashBodyHitColor();
            PlayEnemyHitVfx(hitPosition, hitDirection);
            PlayEnemyHitCameraImpact(hitDirection);
            OnPlayerHitAfterDamage(bulletDamage, strongHit, hitPosition, hitDirection, hitColor);
            return true;
        }

        public bool IsPlayerDetected()
        {
            return IsCombatStarted;
        }

        public bool CanSeePlayer()
        {
            if (!IsPlayerDetected() || player == null)
            {
                return false;
            }

            float distance = Vector2.Distance(transform.position, player.position);
            Vector2 direction = (player.position - transform.position).normalized;
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, obstacleMask);
            return hit.collider == null;
        }

        public virtual float DistanceToPlayer()
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
            SetMovementVelocity(direction * moveSpeed);
        }

        internal virtual void SetMovementVelocity(Vector2 velocity)
        {
            if (body == null)
            {
                return;
            }

            requestedMovementVelocity = velocity;
            hasRequestedMovementVelocity = velocity.sqrMagnitude > 0.000001f;
            if (!hasRequestedMovementVelocity)
            {
                Stop();
            }
        }

        internal bool TryMovePatternTowards(Vector2 target, float speed)
        {
            if (body == null)
            {
                return false;
            }

            float safeSpeed = Mathf.Max(0f, speed);
            float remainingDistance = safeSpeed * EnemyTimeScale.DeltaTime;
            if (remainingDistance <= 0f)
            {
                Stop();
                return false;
            }

            Vector2 current = body.position;
            Vector2 totalDisplacement = Vector2.zero;
            float maxStepDistance = safeSpeed * GetMaxDirectMoveStepSeconds();
            while (remainingDistance > 0f)
            {
                float stepDistance = maxStepDistance > 0f
                    ? Mathf.Min(remainingDistance, maxStepDistance)
                    : remainingDistance;
                Vector2 desired = Vector2.MoveTowards(current, target, stepDistance);
                Vector2 next = ResolveConstrainedBossPosition(current, desired);
                Vector2 displacement = next - current;
                if (displacement.sqrMagnitude <= 0.000001f)
                {
                    break;
                }

                totalDisplacement += displacement;
                current = next;
                remainingDistance -= stepDistance;

                if ((target - current).sqrMagnitude <= 0.000001f)
                {
                    break;
                }
            }

            if (totalDisplacement.sqrMagnitude <= 0.000001f)
            {
                Stop();
                return false;
            }

            ApplyImmediateBodyPosition(current, totalDisplacement / Mathf.Max(Time.deltaTime, 0.0001f));
            return true;
        }

        public void Stop()
        {
            if (body == null)
            {
                return;
            }

            hasRequestedMovementVelocity = false;
            requestedMovementVelocity = Vector2.zero;
            lastAppliedMovementVelocity = Vector2.zero;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        internal void SnapBodyPosition(Vector2 position)
        {
            if (body == null)
            {
                transform.position = new Vector3(position.x, position.y, transform.position.z);
                return;
            }

            ApplyImmediateBodyPosition(position, Vector2.zero);
        }

        private void ApplyRequestedMovement()
        {
            if (body == null)
            {
                return;
            }

            if (!hasRequestedMovementVelocity)
            {
                lastAppliedMovementVelocity = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                return;
            }

            float deltaTime = EnemyTimeScale.FixedDeltaTime;
            if (deltaTime <= 0f)
            {
                lastAppliedMovementVelocity = Vector2.zero;
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 current = body.position;
            Vector2 desired = current + requestedMovementVelocity * deltaTime;
            Vector2 next = ResolveConstrainedBossPosition(current, desired);
            Vector2 displacement = next - current;
            lastAppliedMovementVelocity = displacement / Time.fixedDeltaTime;
            body.linearVelocity = lastAppliedMovementVelocity;
            body.angularVelocity = 0f;

            if (displacement.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            body.MovePosition(next);
        }

        private Vector2 ResolveConstrainedBossPosition(Vector2 current, Vector2 desired)
        {
            return BossCanFlyOverGround
                ? desired
                : GroundMovementConstraint.ClampPointMovement(current, desired, groundProbeColliders);
        }

        private void ApplyImmediateBodyPosition(Vector2 position, Vector2 velocity)
        {
            hasRequestedMovementVelocity = false;
            requestedMovementVelocity = Vector2.zero;
            lastAppliedMovementVelocity = velocity;
            body.linearVelocity = velocity;
            body.angularVelocity = 0f;
            body.position = position;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Physics2D.SyncTransforms();
        }

        private float GetMaxDirectMoveStepSeconds()
        {
            return Mathf.Max(0.001f, maxDirectMoveStepSeconds, Time.fixedDeltaTime);
        }

        // 대쉬 중에는 벽 충돌은 유지한 채 일반 보스와 플레이어의 물리 충돌만 끈다.
        // 접촉 피해를 사용하지 않는 보스는 플레이어를 막는 물리 충돌을 계속 유지한다.
        internal void SetIgnorePlayerCollision(bool ignore)
        {
            OnPlayerCollisionIgnoreChanged(ignore);
            if (SuppressesBodyContactDamage || isIgnoringPlayerCollision == ignore)
            {
                return;
            }

            Collider2D[] bossColliders = GetPhysicsColliders();
            Collider2D[] targetPlayerColliders = GetPlayerPhysicsColliders();
            if (bossColliders.Length == 0 || targetPlayerColliders.Length == 0)
            {
                return;
            }

            for (int i = 0; i < bossColliders.Length; i++)
            {
                if (bossColliders[i] == null)
                {
                    continue;
                }

                for (int j = 0; j < targetPlayerColliders.Length; j++)
                {
                    if (targetPlayerColliders[j] == null)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(bossColliders[i], targetPlayerColliders[j], ignore);
                }
            }

            isIgnoringPlayerCollision = ignore;
        }

        protected virtual void OnPlayerCollisionIgnoreChanged(bool ignore)
        {
        }

        // 물리 충돌을 꺼둔 상태에서는 OnCollisionEnter2D가 발생하지 않으므로,
        // 대쉬 중에는 겹침 여부를 직접 검사해 기존 접촉 피해 로직을 그대로 호출해준다.
        private void TickDashContactForState()
        {
            if (!IsDashing
                || isAutomaticDashContactDamageSuppressed
                || player == null
                || health == null
                || health.IsDead)
            {
                return;
            }

            Collider2D[] bossColliders = GetPhysicsColliders();
            Collider2D[] targetPlayerColliders = GetPlayerPhysicsColliders();
            if (bossColliders.Length == 0 || targetPlayerColliders.Length == 0)
            {
                return;
            }

            PlayerCombatController playerController = player.GetComponent<PlayerCombatController>();
            if (playerController == null)
            {
                return;
            }

            for (int i = 0; i < bossColliders.Length; i++)
            {
                Collider2D bossCollider = bossColliders[i];
                if (bossCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < targetPlayerColliders.Length; j++)
                {
                    if (targetPlayerColliders[j] == null
                        || !Physics2D.Distance(bossCollider, targetPlayerColliders[j]).isOverlapped)
                    {
                        continue;
                    }

                    playerController.TryReceiveEnemyBodyContact(bossCollider, bossCollider.ClosestPoint(player.position));
                    return;
                }
            }
        }

        internal void SetAutomaticDashContactDamageSuppressed(bool suppressed)
        {
            isAutomaticDashContactDamageSuppressed = suppressed;
        }

        internal void ApplyDashContactDamageInBox(Vector2 center, Vector2 size, float angle)
        {
            if (!IsDashing
                || player == null
                || health == null
                || health.IsDead
                || size.x <= 0.0001f
                || size.y <= 0.0001f)
            {
                return;
            }

            Collider2D[] bossColliders = GetPhysicsColliders();
            if (bossColliders.Length == 0)
            {
                return;
            }

            Collider2D sourceCollider = null;
            for (int i = 0; i < bossColliders.Length; i++)
            {
                if (bossColliders[i] != null)
                {
                    sourceCollider = bossColliders[i];
                    break;
                }
            }

            if (sourceCollider == null)
            {
                return;
            }

            PlayerCombatController targetPlayer = player.GetComponent<PlayerCombatController>();
            if (targetPlayer == null)
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle);
            for (int i = 0; i < hits.Length; i++)
            {
                PlayerCombatController hitPlayer = hits[i] != null
                    ? hits[i].GetComponentInParent<PlayerCombatController>()
                    : null;
                if (hitPlayer != targetPlayer)
                {
                    continue;
                }

                targetPlayer.TryReceiveEnemyBodyContact(sourceCollider, center);
                return;
            }
        }

        private Collider2D[] GetPhysicsColliders()
        {
            physicsColliders ??= FilterNonTriggerColliders(GetComponentsInChildren<Collider2D>(true));
            return physicsColliders;
        }

        private Collider2D[] GetPlayerPhysicsColliders()
        {
            if (playerPhysicsColliders == null && player != null)
            {
                playerPhysicsColliders = FilterNonTriggerColliders(player.GetComponentsInChildren<Collider2D>(true));
            }

            return playerPhysicsColliders ?? Array.Empty<Collider2D>();
        }

        private static Collider2D[] FilterNonTriggerColliders(Collider2D[] source)
        {
            List<Collider2D> result = new();
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null && !source[i].isTrigger)
                {
                    result.Add(source[i]);
                }
            }

            return result.ToArray();
        }

        internal bool IsDeadForState => health != null && health.IsDead;
        internal bool IsExecutionLockedForState => isExecutionLocked;
        internal virtual bool AllowsCinematicMovementForState => false;
        internal bool IsPhaseTransitionWaitingForState => PhaseController.IsPhaseTransitionWaiting;
        internal bool IsCombatStartedForState => PhaseController.IsCombatStarted;
        internal static bool IsExecutionPausedForState => IsExecutionPaused;

        internal void TickVisualStateForState()
        {
            UpdateBodyHitColor();
            UpdateHitFlash();
            UpdateStagger();
        }

        internal void TickPhaseTransitionWaitForState()
        {
            PhaseController.TickPhaseTransitionWait();
        }

        internal void ResolvePlayerForState()
        {
            ResolvePlayer();
        }

        internal void TickActiveBehaviorForState()
        {
            TryActivateBossCombatUiOnCombatStart();
            if (RotatesBodyToPlayer)
            {
                RotateToTarget();
            }

            OnBossTick();
        }

        public bool TryConsumeLife()
        {
            return PhaseController.TryConsumeLife();
        }

        public void FreezeCombatTimer()
        {
            frozenCombatElapsedSeconds ??= CombatElapsedSeconds;
        }

        public void PlayCombatBgmForIntro()
        {
            PlayConfiguredBgm();
        }

        public IEnumerator PlayFinalDeathSequence(bool playFinalDeathExplosions)
        {
            if (finalDeathSequencePlayed)
            {
                yield break;
            }

            TimeSlowScreenFx.CancelImmediate();
            TimeSlowSkillSO.CancelActiveAfterimages();
            FreezeCombatTimer();
            finalDeathSequencePlayed = true;
            SetFinalDeathSequencePlaying(true);
            PlayBossDeathSfx();

            try
            {
                CancelBossAction();
                CancelMinionPatternAction();
                Stop();
                projectileTracker.DestroyAll();

                yield return BossDeathSequencePlayer.Play(this, playFinalDeathExplosions);
            }
            finally
            {
                SetFinalDeathSequencePlaying(false);
            }
        }

        private void SetFinalDeathSequencePlaying(bool playing)
        {
            if (isFinalDeathSequencePlaying == playing)
            {
                return;
            }

            isFinalDeathSequencePlaying = playing;
            finalDeathSequencePlayCount = Mathf.Max(0, finalDeathSequencePlayCount + (playing ? 1 : -1));
        }

        // 페이즈에 따라 활성 비주얼이 교체되는 보스가 있으므로, 직접 지정한 Animator와
        // 보스 계층의 Animator를 모두 후보로 제공한다. 실제 사망 상태를 가진 활성 Animator는
        // BossDeathSequencePlayer가 선택한다.
        internal Animator[] DeathAnimatorsForSequence
        {
            get
            {
                if (deathAnimators != null)
                {
                    return deathAnimators;
                }

                List<Animator> candidates = new();
                if (deathAnimator != null)
                {
                    candidates.Add(deathAnimator);
                }

                Animator[] hierarchyAnimators =
                    GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < hierarchyAnimators.Length; i++)
                {
                    Animator animator = hierarchyAnimators[i];
                    if (animator != null
                        && !candidates.Contains(animator))
                    {
                        candidates.Add(animator);
                    }
                }

                deathAnimators = candidates.ToArray();
                return deathAnimators;
            }
        }

        internal string DeathTriggerNameForSequence => deathTriggerName;
        internal float FinalDeathExplosionSecondsForSequence => finalDeathExplosionSeconds;
        internal int FinalDeathExplosionCountForSequence => finalDeathExplosionCount;
        internal GameObject FinalDeathExplosionPrefabForSequence => finalDeathExplosionPrefab;
        internal float FinalDeathExplosionPrefabLifetimeSecondsForSequence => finalDeathExplosionPrefabLifetimeSeconds;
        internal Transform FinalDeathExplosionAreaCenterForSequence =>
            finalDeathExplosionAreaCenter != null ? finalDeathExplosionAreaCenter : bodyRoot;
        internal float FinalDeathExplosionAreaRadiusForSequence => finalDeathExplosionAreaRadius;
        internal float DeathExplosionToAnimationDelaySecondsForSequence => deathExplosionToAnimationDelaySeconds;
        internal float DeathAnimationFallbackSecondsForSequence => deathAnimationFallbackSeconds;
        internal SpriteRenderer[] RenderersForSequence => renderers;

        internal bool CanUseDeathExplosionRendererForSequence(SpriteRenderer renderer)
        {
            return renderer != null
                && renderer.enabled
                && renderer.gameObject.activeInHierarchy
                && renderer != lockOnIndicator
                && renderer != executionIndicator
                && !IsStatusRenderer(renderer)
                && !ShouldIgnoreBodyStateRenderer(renderer)
                && renderer.bounds.size.sqrMagnitude > 0.0001f;
        }

        internal float PhaseTransitionWaitSeconds => phaseTransitionWaitSeconds;

        internal void OnCombatStartedForController()
        {
            if (!combatStartedCounted)
            {
                combatStartedCounted = true;
                combatStartedCount++;
            }

            combatStartedAt = Time.time;
            frozenCombatElapsedSeconds = null;
            latestClearTimeWasNewRecord = false;
            PlayConfiguredBgm();
            OnCombatStarted();
            CombatStarted?.Invoke(this);
        }

        private void PlayConfiguredBgm()
        {
            if (bossData != null && !string.IsNullOrWhiteSpace(bossData.BgmId))
            {
                SoundManager.PlayBgm(bossData.BgmId, bossData.BgmFadeSeconds);
            }
        }

        private void PlayBossDeathSfx()
        {
            if (bossDeathSfxPlayed
                || bossData == null
                || string.IsNullOrWhiteSpace(bossData.DeathSfxId))
            {
                return;
            }

            bossDeathSfxPlayed = true;
            SoundManager.PlaySfx(bossData.DeathSfxId);
        }

        internal void OnBossPhaseChangedForController(int phaseIndex, int phaseNumber)
        {
            OnBossPhaseChanged(phaseIndex, phaseNumber);
            HandleMinionBossPhaseChanged();
        }

        internal void CancelBossActionForController()
        {
            CancelBossAction();
            CancelMinionPatternAction();
        }

        internal void RefillHpForPhaseController()
        {
            hpGauge?.Configure(GetCurrentPhaseMaxHp(), true);
            ApplyBodyStateColor();
            bossHpBarView?.PlayPhaseRefill();
            OnHpEmptyRecovered();
        }

        private int GetCurrentPhaseMaxHp()
        {
            BossGraphPhase phase = ActiveGraphPhase;
            if (phase != null && phase.PhaseMaxHp > 0)
            {
                return phase.PhaseMaxHp;
            }

            return Mathf.Max(1, maxHp);
        }

        internal float HpEmptyExecutionSeconds => hpEmptyExecutionSeconds;

        internal void BeginHpEmptyForState()
        {
            CancelBossAction();
            CancelMinionPatternAction();
            Stop();
            ApplyBodyStateColor();
            bossHpBarView?.SetExecutionWindow(true, 1f);
            OnHpEmptyBegan();
        }

        internal void UpdateHpEmptyWindowForState(float remainingRatio)
        {
            bossHpBarView?.SetExecutionWindow(true, remainingRatio);
        }

        internal void RecoverFromHpEmptyForState()
        {
            if (health == null || health.IsDead || hpGauge == null)
            {
                return;
            }

            hpGauge.Restore(Mathf.Max(1, (hpGauge.MaxBullets + 2) / 3), BulletChangeSource.Generic);
            ApplyBodyStateColor();
            bossHpBarView?.ClearExecutionWindow();
            OnHpEmptyRecovered();
        }

        public bool CanSpawnEnemyProjectile()
        {
            if (hpGauge == null)
            {
                return false;
            }

            if (!IsExecutionPaused && !hpGauge.IsEmpty)
            {
                return true;
            }

            return CanSpawnEnemyProjectileDuringCinematic;
        }

        public void RegisterActiveProjectile(EnemyProjectile projectile)
        {
            projectileTracker.Register(projectile);
        }

        public void UnregisterActiveProjectile(EnemyProjectile projectile)
        {
            projectileTracker.Unregister(projectile);
        }

        protected void DestroyActiveProjectiles()
        {
            projectileTracker.DestroyAll();
        }

        public Vector2 GetFacingDirection()
        {
            return bodyRoot != null ? (Vector2)bodyRoot.right : Vector2.right;
        }

        public void FaceTowards(Vector2 worldPosition)
        {
            ApplyExecutionFacing(worldPosition);
        }

        protected virtual void ApplyExecutionFacing(Vector2 worldPosition)
        {
            if (!RotatesBodyToPlayer)
            {
                return;
            }

            Transform facingRoot = bodyRoot != null ? bodyRoot : transform;
            Vector2 horizontalDirection = new(worldPosition.x - transform.position.x, 0f);
            RotateRight(facingRoot, horizontalDirection);
        }

        protected virtual void OnBossStarted() { }
        protected virtual void OnCombatStarted() { }
        protected virtual void OnBossPhaseChanged(int phaseIndex, int phaseNumber) { }
        protected abstract void OnBossTick();
        protected virtual void CancelBossAction() { }
        protected virtual void OnBossDied() { }
        protected virtual void OnHpEmptyBegan() { }
        protected virtual void OnHpEmptyRecovered() { }
        protected virtual bool TryHandlePlayerHitBeforeDamage(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor) => false;
        protected virtual void OnPlayerHitAfterDamage(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor) { }
        protected virtual bool RotatesBodyToPlayer => true;
        protected virtual bool CanSpawnEnemyProjectileDuringCinematic => false;
        protected static bool IsExecutionPaused => PlayerCombatController.IsExecutionCinematicActive;

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
            bool homingEnabled,
            float homingSeconds,
            float homingTurnDegrees,
            Vector3? muzzleFlashPosition = null,
            float muzzleFlashScale = 0.9f)
        {
            if (!CanSpawnEnemyProjectile())
            {
                return null;
            }

            EnemyProjectile projectile = EnemyProjectile.Spawn(
                prefab,
                hpGauge,
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
                homingEnabled,
                homingSeconds,
                homingTurnDegrees);

            if (projectile == null)
            {
                return null;
            }

            if (muzzleFlashScale > 0f)
            {
                ProjectileVfx.PlayPrefab(
                    EnemyMuzzleFlashVfxPrefab,
                    muzzleFlashPosition ?? projectile.transform.position,
                    direction,
                    bodyRoot != null ? bodyRoot : transform,
                    muzzleFlashScale);
            }

            return projectile;
        }

        internal EnemyProjectile FireGraphProjectile(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            float muzzleFlashScale,
            bool? aimAtPlayerWhileChargingOverride = null,
            bool? aimAtPlayerOnLaunchOverride = null,
            float chargeSecondsOverride = -1f,
            float radiusOverride = -1f,
            bool suppressHoming = false)
        {
            return BossProjectileEmitter.Fire(
                SpawnBossProjectile,
                settings,
                origin,
                direction,
                aimAtPlayerWhileChargingOverride ?? (settings != null && settings.AimAtPlayerWhileCharging),
                aimAtPlayerOnLaunchOverride ?? (settings != null && settings.AimAtPlayerOnLaunch),
                suppressHoming,
                chargeSecondsOverride,
                radiusOverride,
                null,
                muzzleFlashScale,
                null);
        }

        protected void BeginStagger(float seconds, float shakeDistance, float shakeFrequency)
        {
            if (seconds <= 0f || isExecutionLocked || IsHpEmpty)
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
            statusView.SetTarget(health);
        }

        private bool UsesBossCombatUi()
        {
            return bossCombatUiRoot != null || bossHpBarView != null;
        }

        private void PrepareBossCombatUi()
        {
            if (bossCombatUiRoot != null)
            {
                bossHpBarView ??= bossCombatUiRoot.GetComponentInChildren<BossBulletBarView>(true);
            }

            bossLivesView ??= bossCombatUiRoot != null
                ? bossCombatUiRoot.GetComponentInChildren<BossLivesView>(true)
                : GetComponentInChildren<BossLivesView>(true);

            bossLivesView?.SetTarget(this);

            // 로컬라이징된 이름은 OnEnable에서 건 StringChanged 구독이 채워주므로 여기서는 건드리지 않는다
            // (언어 로드가 끝나기 전에 여기서 덮어쓰면 빈 텍스트로 되돌아가버린다).
            bool usesLocalizedBossName = bossData != null && bossData.HasLocalizedBossName;
            if (bossNameText != null && !usesLocalizedBossName)
            {
                bossNameText.text = ResolveBossNameFallback();
            }
        }

        private string ResolveBossNameFallback()
        {
            if (bossData != null && !string.IsNullOrWhiteSpace(bossData.BossName))
            {
                return bossData.BossName;
            }

            return DisplayName;
        }

        private void SetBossNameText(string value)
        {
            if (bossNameText != null)
            {
                bossNameText.text = value;
            }
        }

        private void BindBossCombatUiTargetsIfVisible()
        {
            if (IsBossCombatUiVisible())
            {
                bossHpBarView?.SetTarget(hpGauge);
            }
        }

        private void TryActivateBossCombatUiOnCombatStart()
        {
            if (isBossCombatUiActive || !UsesBossCombatUi() || !IsCombatStarted)
            {
                return;
            }

            PrepareBossCombatUi();
            SuppressEnemyStatusView();
            SetBossCombatUiVisible(true);
            bossHpBarView?.SetTarget(hpGauge);
        }

        private bool IsBossCombatUiVisible()
        {
            if (bossCombatUiRoot != null)
            {
                return bossCombatUiRoot.activeInHierarchy;
            }

            return bossHpBarView != null && bossHpBarView.gameObject.activeInHierarchy;
        }

        private void SetBossCombatUiVisible(bool visible)
        {
            if (bossCombatUiRoot != null)
            {
                bossCombatUiRoot.SetActive(visible);
            }
            else if (bossHpBarView != null)
            {
                bossHpBarView.gameObject.SetActive(visible);
            }

            isBossCombatUiActive = visible;
        }

        internal void NotifyLivesChanged()
        {
            LivesChanged?.Invoke(CurrentLives, MaxLives);
            bossLivesView?.Refresh();
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
            statusView.SetTarget(health);
            statusView.SetSuppressed(true);
        }

        public void PlayExecutionHitReaction(Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            FlashBodyHitColor();
            PlayEnemyHitVfx(hitPosition, hitDirection);
            PlayEnemyHitCameraImpact(hitDirection);
        }

        private void PlayEnemyHitVfx(Vector3 hitPosition, Vector2 hitDirection)
        {
            ProjectileVfx.PlayPrefab(
                EnemyHitVfxPrefab,
                hitPosition,
                hitDirection,
                bodyRoot != null ? bodyRoot : transform,
                followRotation: false);
        }

        private void FlashBodyHitColor()
        {
            isBodyHitColorActive = true;
            bodyHitColorEndsAt = Time.time + BodyHitColorSeconds;
            isHitFlashActive = true;
            hitFlashEndsAt = Time.time + hitFlashSeconds;
            ApplyBodyStateColor();
        }

        private void UpdateHitFlash()
        {
            if (!isHitFlashActive || Time.time < hitFlashEndsAt)
            {
                return;
            }

            isHitFlashActive = false;
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

        private void ApplyBodyStateColor()
        {
            if (renderers == null)
            {
                return;
            }

            Color color = NormalColor;
            if (isStaggered)
            {
                color = StaggeredColor;
            }
            else if (isBodyHitColorActive)
            {
                color = BodyHitColor;
            }
            else if ((isExecutionLocked || IsHpEmpty) && ShouldUseExecutionAvailableBodyColor)
            {
                color = HpEmptyColor;
            }

            float flashAmount = isHitFlashActive ? 1f : 0f;
            hitFlashPropertyBlock ??= new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || IsStatusRenderer(renderers[i]) || ShouldIgnoreBodyStateRenderer(renderers[i]))
                {
                    continue;
                }

                renderers[i].color = color;

                renderers[i].GetPropertyBlock(hitFlashPropertyBlock);
                hitFlashPropertyBlock.SetColor(FlashColorId, hitFlashColor);
                hitFlashPropertyBlock.SetFloat(FlashAmountId, flashAmount);
                renderers[i].SetPropertyBlock(hitFlashPropertyBlock);
            }
        }

        private bool IsStatusRenderer(SpriteRenderer renderer)
        {
            return statusView != null && statusView.OwnsRenderer(renderer);
        }

        protected virtual bool ShouldIgnoreBodyStateRenderer(SpriteRenderer renderer) => false;
        protected virtual bool ShouldUseExecutionAvailableBodyColor => true;

        private void HandleDied(Health _)
        {
            FreezeCombatTimer();
            projectileTracker.DestroyAll();
            SetBossCombatUiVisible(false);
            SoundManager.StopBgm();
            PlayBossDeathSfx();
            if (bossLivesView != null)
            {
                bossLivesView.gameObject.SetActive(false);
            }

            ApplyBossClearRewards();
            OnBossDied();
            HandleMinionBossDied();
            Defeated?.Invoke(this);
        }

        private void ApplyBossClearRewards()
        {
            if (bossData == null)
            {
                return;
            }

            GameSaveManager.ClearBoss(bossData.Id);
            latestClearTimeWasNewRecord = GameSaveManager.TrySetBestClearTime(bossData.Id, CombatElapsedSeconds);

            IReadOnlyList<string> unlocksBossIds = bossData.UnlocksBossIds;
            for (int i = 0; i < unlocksBossIds.Count; i++)
            {
                GameSaveManager.UnlockBoss(unlocksBossIds[i]);
            }

            IReadOnlyList<string> unlocksSkillIds = bossData.UnlocksSkillIds;
            for (int i = 0; i < unlocksSkillIds.Count; i++)
            {
                GameSaveManager.UnlockSkill(unlocksSkillIds[i]);
            }

            IReadOnlyList<string> unlocksPassiveSkillIds = bossData.UnlocksPassiveSkillIds;
            for (int i = 0; i < unlocksPassiveSkillIds.Count; i++)
            {
                GameSaveManager.UnlockPassiveSkill(unlocksPassiveSkillIds[i]);
            }

            IReadOnlyList<string> unlocksWeaponIds = bossData.UnlocksWeaponIds;
            for (int i = 0; i < unlocksWeaponIds.Count; i++)
            {
                GameSaveManager.UnlockWeapon(unlocksWeaponIds[i]);
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

        private static void PlayEnemyHitCameraImpact(Vector2 direction)
        {
            PlayEnemyHitCameraImpact(direction, 0.08f, 0.12f, 0.05f);
        }

        private static void PlayEnemyHitCameraImpact(Vector2 direction, float amplitude, float seconds, float zoomAmount)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, amplitude, seconds, zoomAmount);
        }

        internal static void PlayEnemyHitCameraImpactForSequence(Vector2 direction, float amplitude, float seconds, float zoomAmount)
        {
            PlayEnemyHitCameraImpact(direction, amplitude, seconds, zoomAmount);
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

    }
}
