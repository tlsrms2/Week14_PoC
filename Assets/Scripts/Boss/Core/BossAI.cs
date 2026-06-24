using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public abstract class BossAI : MonoBehaviour
    {
        [Header("Boss Lives")]
        [Tooltip("보스의 총 목숨(페이즈) 수입니다. 처형될 때마다 1씩 깎입니다.")]
        [SerializeField, Min(1)] private int maxLives = 3;
        [Tooltip("처형 후 다음 페이즈 패턴을 시작하기 전, 보스가 제자리에서 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float phaseTransitionWaitSeconds = 1.2f;

        [Header("Enrage System")]
        [Tooltip("전투 시작 후 1단계 광폭화(최대 탄환 감소)까지 걸리는 시간입니다.")]
        [SerializeField] private float enragePhase1Seconds = 30f;
        [SerializeField] private int enragePhase1MaxBullets = 3;
        [Tooltip("1단계 광폭화 후 2단계 광폭화까지 추가로 걸리는 시간입니다.")]
        [SerializeField] private float enragePhase2Seconds = 30f;
        [SerializeField] private int enragePhase2MaxBullets = 1;

        [Header("Enrage Windup")]
        [Tooltip("광폭화 진입 연출이 재생되기 전, 보스가 부들부들 떠는 시간입니다. 0이면 떨림 없이 바로 진입 연출이 재생됩니다.")]
        [SerializeField, Min(0f)] private float enrageWindupSeconds = 0.6f;
        [Tooltip("떨림 중 좌우로 움직이는 거리입니다.")]
        [SerializeField, Min(0f)] private float enrageWindupShakeDistance = 0.05f;
        [Tooltip("떨림이 좌우로 진동하는 빈도입니다.")]
        [SerializeField, Min(0f)] private float enrageWindupShakeFrequency = 28f;

        [Header("Enrage Burst Effect")]
        [Tooltip("광폭화 단계로 넘어갈 때 스폰할 이미지입니다.")]
        [SerializeField] private Sprite enrageBurstSprite;
        [Tooltip("이펙트가 빠르게 커지면서 도달할 최종 스케일(N)입니다.")]
        [SerializeField, Min(0.01f)] private float enrageBurstTargetScale = 4f;
        [Tooltip("0에서 최종 스케일까지 커지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float enrageBurstGrowSeconds = 0.15f;
        [Tooltip("최종 스케일에 도달한 뒤 사라지기 전까지 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float enrageBurstHoldSeconds = 0.1f;
        [Tooltip("유지 시간이 끝난 뒤 페이드아웃되는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float enrageBurstFadeSeconds = 0.35f;
        [SerializeField] private Color enrageBurstColor = Color.white;
        [Tooltip("광폭화 진입 시 카메라 쉐이크 강도입니다.")]
        [SerializeField, Min(0f)] private float enrageShakeAmplitude = 0.25f;
        [SerializeField, Min(0f)] private float enrageShakeSeconds = 0.3f;
        [SerializeField, Min(0f)] private float enrageShakeZoom = 0.12f;

        [Header("Death Sequence")]
        [SerializeField] private Animator deathAnimator;
        [SerializeField] private string deathTriggerName = "Die";
        [SerializeField, Min(0f)] private float finalDeathExplosionSeconds = 1.4f;
        [SerializeField, Min(1)] private int finalDeathExplosionCount = 10;
        [SerializeField, Min(0.1f)] private float finalDeathExplosionScale = 1.25f;
        [SerializeField, Min(0)] private int finalDeathExplosionSparkCount = 24;
        [SerializeField] private Color finalDeathExplosionColor = new(1f, 0.55f, 0.12f, 1f);
        [SerializeField, Min(0f)] private float deathAnimationFallbackSeconds = 1f;

        [Header("Meta")]
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

        [Tooltip("기본 상태에서 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField, HideInInspector] private Color normalColor = Color.white;
        [Tooltip("보스 HP가 0이 되었을 때 보스 스프라이트에 적용할 색입니다.")]
        [FormerlySerializedAs("bulletEmptyColor")]
        [SerializeField, HideInInspector] private Color hpEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("보스가 경직 상태일 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField, HideInInspector] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        [Header("Detection")]
        [Tooltip("플레이어를 감지할 수 있는 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float detectionRange = 9f;

        [Header("Movement")]
        [Tooltip("보스의 기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;

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
        [SerializeField] private BossEnrageBarView bossEnrageBarView;

        [SerializeField, HideInInspector] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [FormerlySerializedAs("bulletBarColor")]
        [SerializeField, HideInInspector] private Color hpBarColor = new(1f, 0.55f, 0.1f, 1f);
        [FormerlySerializedAs("emptyBulletBarColor")]
        [SerializeField, HideInInspector] private Color emptyHpBarColor = Color.red;
        [SerializeField, HideInInspector] private Color lockOnIndicatorColor = Color.white;
        [SerializeField, HideInInspector] private Color executionIndicatorColor = Color.red;

        private readonly BossProjectileTracker projectileTracker = new();
        private Health health;
        private BulletGauge hpGauge;
        private SpriteRenderer[] renderers;
        private Transform player;
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
        private bool finalDeathSequencePlayed;
        private bool isFinalDeathSequencePlaying;
        private BossPhaseController phaseController;
        private BossStateMachine stateMachine;
        private static int finalDeathSequencePlayCount;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Health Health => health;
        public BulletGauge HpGauge => hpGauge;
        public BulletGauge Bullets => hpGauge;
        public Transform Player => player;
        public Rigidbody2D Body => body;
        public Transform BodyRoot => bodyRoot;
        public LayerMask ObstacleMask => obstacleMask;
        public Vector3 SpawnPosition { get; private set; }
        public bool IsHpEmpty => hpGauge != null && hpGauge.IsEmpty;
        public bool IsBulletEmpty => IsHpEmpty;
        public bool IsExecutionLocked => isExecutionLocked;
        public bool IsFinalDeathSequencePlaying => isFinalDeathSequencePlaying;
        public bool IsStaggered => isStaggered;
        public float DetectionRange => detectionRange;
        public float MoveSpeed => moveSpeed;
        public Color NormalColor => ActiveColorSettings != null ? ActiveColorSettings.NormalColor : normalColor;
        public Color HpEmptyColor => ActiveColorSettings != null ? ActiveColorSettings.HpEmptyColor : hpEmptyColor;
        public Color BulletEmptyColor => HpEmptyColor;
        public Color StaggeredColor => ActiveColorSettings != null ? ActiveColorSettings.StaggeredColor : staggeredColor;
        public Color BodyHitColor => ActiveEffectData != null ? ActiveEffectData.EnemyBodyHitColor : new Color(1f, 0.35f, 0.25f, 1f);
        public float BodyHitColorSeconds => ActiveEffectData != null ? ActiveEffectData.BodyHitColorSeconds : 0.08f;
        public Color StatusBarBackgroundColor => ActiveColorSettings != null ? ActiveColorSettings.StatusBarBackgroundColor : statusBarBackgroundColor;
        public Color HpBarColor => ActiveColorSettings != null ? ActiveColorSettings.HpBarColor : hpBarColor;
        public Color EmptyHpBarColor => ActiveColorSettings != null ? ActiveColorSettings.EmptyHpBarColor : emptyHpBarColor;
        public Color BulletBarColor => HpBarColor;
        public Color EmptyBulletBarColor => EmptyHpBarColor;
        public Color LockOnIndicatorColor => ActiveColorSettings != null ? ActiveColorSettings.LockOnIndicatorColor : lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => ActiveColorSettings != null ? ActiveColorSettings.ExecutionIndicatorColor : executionIndicatorColor;

        protected CombatEffectData EffectData => ActiveEffectData;
        protected virtual BossGraphAsset GraphAsset => null;
        protected virtual BossProjectileSettings ResolveGraphProjectileSettings(string projectileName) => null;
        public int MaxLives => Mathf.Max(1, maxLives);
        public int CurrentLives => PhaseController.CurrentLives;
        public int CurrentPhaseIndex => PhaseController.CurrentPhaseIndex;
        public int CurrentPhaseNumber => PhaseController.CurrentPhaseNumber;
        public int CurrentEnragePhase => PhaseController.CurrentEnragePhase;
        public float CurrentEnrageProgress => PhaseController.CurrentEnrageProgress;
        public float CurrentEnrageRemainingSeconds => PhaseController.CurrentEnrageRemainingSeconds;
        public bool IsCombatStarted => PhaseController.IsCombatStarted;
        public event Action<int, int> LivesChanged;
        public event Action<int, float, float> EnrageChanged;
        public static event Action<BossAI> CombatStarted;
        public static event Action<BossAI> Defeated;
        public static bool IsAnyFinalDeathSequencePlaying => finalDeathSequencePlayCount > 0;

        private BossPhaseController PhaseController => phaseController ??= new BossPhaseController(this);
        private CombatEffectData ActiveEffectData => GraphAsset != null && GraphAsset.EffectData != null ? GraphAsset.EffectData : effectData;
        private BossColorSettings ActiveColorSettings => GraphAsset != null && GraphAsset.ColorSettings != null ? GraphAsset.ColorSettings : colorSettings;
        internal BossProjectileSettings ResolveGraphProjectileSettingsForActions(string projectileName)
        {
            return ResolveGraphProjectileSettings(projectileName);
        }

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            hpGauge = GetComponent<BulletGauge>();

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            bodyRoot ??= FindChild("Visual") ?? transform;
            deathAnimator ??= bodyRoot != null
                ? bodyRoot.GetComponentInChildren<Animator>(true)
                : GetComponentInChildren<Animator>(true);

            lockOnIndicator ??= FindChild("LockOnIndicator")?.GetComponent<SpriteRenderer>();
            executionIndicator ??= FindChild("ExecutionIndicator")?.GetComponent<SpriteRenderer>();
            renderers = bodyRoot != null
                ? bodyRoot.GetComponentsInChildren<SpriteRenderer>(true)
                : GetComponentsInChildren<SpriteRenderer>(true);
            phaseController = new BossPhaseController(this);
            stateMachine = new BossStateMachine(this);
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

            SetFinalDeathSequencePlaying(false);
        }

        protected virtual void Start()
        {
            PhaseController.Initialize();
            
            SpawnPosition = transform.position;
            hpGauge.Configure(maxHp, true);

            PrepareStatusViews();
            ApplyBodyStateColor();
            ResolvePlayer();
            OnBossStarted();
        }

        protected virtual void Update()
        {
            stateMachine ??= new BossStateMachine(this);
            stateMachine.Tick();
        }

        public void PlayExecutionBarDrain()
        {
            bossHpBarView?.PlayExecutionDrain();
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

            if (IsHpEmpty)
            {
                // 그로기 상태에서는 총격으로 목숨이 깎이지 않고 이펙트만 재생됩니다.
                // 페이즈 전환은 오직 '처형 연출'이 끝났을 때 외부에서 TryConsumeLife()를 호출하여 처리합니다.
                PlayPlayerAttackImpact(hitPosition, hitDirection, hitColor);
                PlayEnemyHitCameraImpact(hitDirection);
                return true;
            }

            if (TryHandlePlayerHitBeforeDamage(bulletDamage, strongHit, hitPosition, hitDirection, hitColor))
            {
                return true;
            }

            hpGauge.TrySpend(bulletDamage, BulletChangeSource.Hit);

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

        internal bool IsDeadForState => health != null && health.IsDead;
        internal bool IsExecutionLockedForState => isExecutionLocked;
        internal bool IsPhaseTransitionWaitingForState => PhaseController.IsPhaseTransitionWaiting;
        internal bool IsCombatStartedForState => PhaseController.IsCombatStarted;
        internal static bool IsExecutionPausedForState => IsExecutionPaused;

        internal void TickVisualStateForState()
        {
            UpdateBodyHitColor();
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

        internal void TryStartCombatForState()
        {
            PhaseController.TryStartCombat(IsPlayerDetected());
        }

        internal void TickActiveBehaviorForState()
        {
            PhaseController.TickEnrage();

            TryActivateBossCombatUiOnCombatStart();
            if (RotatesBodyToPlayer)
            {
                RotateToTarget();
            }

            OnBossTick();
        }

        protected IEnumerator ApplyPendingEnrageIfAny()
        {
            yield return PhaseController.ApplyPendingEnrageIfAny();
        }

        internal IEnumerator ApplyPendingEnrageIfAnyForGraph()
        {
            yield return ApplyPendingEnrageIfAny();
        }

        private void PlayEnrageTransitionEffect()
        {
            Vector3 spawnPosition = bodyRoot != null ? bodyRoot.position : transform.position;

            SoundManager.PlaySfx("BossRoar");

            if (enrageBurstSprite != null)
            {
                GameObject burstObject = new GameObject("EnrageBurstVfx");
                burstObject.transform.position = spawnPosition;
                EnrageBurstVfx burst = burstObject.AddComponent<EnrageBurstVfx>();
                burst.Play(
                    enrageBurstSprite,
                    spawnPosition,
                    bodyRoot,
                    enrageBurstTargetScale,
                    enrageBurstGrowSeconds,
                    enrageBurstHoldSeconds,
                    enrageBurstFadeSeconds,
                    enrageBurstColor);
            }

            PlayEnemyHitCameraImpact(Vector2.zero, enrageShakeAmplitude, enrageShakeSeconds, enrageShakeZoom);
        }

        private IEnumerator PlayEnrageWindupTremble()
        {
            if (enrageWindupSeconds <= 0f || bodyRoot == null)
            {
                yield break;
            }

            Vector3 baseLocalPosition = bodyRoot.localPosition;
            float elapsed = 0f;

            while (elapsed < enrageWindupSeconds)
            {
                Stop();
                float sign = Mathf.Sin(Time.time * enrageWindupShakeFrequency * Mathf.PI * 2f) >= 0f ? 1f : -1f;
                bodyRoot.localPosition = baseLocalPosition + Vector3.right * (enrageWindupShakeDistance * sign);
                elapsed += Time.deltaTime;
                yield return null;
            }

            bodyRoot.localPosition = baseLocalPosition;
        }

        public bool TryConsumeLife()
        {
            return PhaseController.TryConsumeLife();
        }

        public IEnumerator PlayFinalDeathSequence()
        {
            if (finalDeathSequencePlayed)
            {
                yield break;
            }

            finalDeathSequencePlayed = true;
            SetFinalDeathSequencePlaying(true);

            try
            {
                CancelBossAction();
                Stop();
                projectileTracker.DestroyAll();

                yield return BossDeathSequencePlayer.Play(this);
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

        internal Animator DeathAnimatorForSequence => deathAnimator;
        internal string DeathTriggerNameForSequence => deathTriggerName;
        internal float FinalDeathExplosionSecondsForSequence => finalDeathExplosionSeconds;
        internal int FinalDeathExplosionCountForSequence => finalDeathExplosionCount;
        internal float FinalDeathExplosionScaleForSequence => finalDeathExplosionScale;
        internal int FinalDeathExplosionSparkCountForSequence => finalDeathExplosionSparkCount;
        internal Color FinalDeathExplosionColorForSequence => finalDeathExplosionColor;
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
        internal float EnragePhase1Seconds => enragePhase1Seconds;
        internal int EnragePhase1MaxBullets => enragePhase1MaxBullets;
        internal float EnragePhase2Seconds => enragePhase2Seconds;
        internal int EnragePhase2MaxBullets => enragePhase2MaxBullets;

        internal IEnumerator PlayEnrageWindupTrembleForController()
        {
            return PlayEnrageWindupTremble();
        }

        internal void PlayEnrageTransitionEffectForController()
        {
            PlayEnrageTransitionEffect();
        }

        internal void OnCombatStartedForController()
        {
            OnCombatStarted();
            CombatStarted?.Invoke(this);
        }

        internal void OnBossPhaseChangedForController(int phaseIndex, int phaseNumber)
        {
            OnBossPhaseChanged(phaseIndex, phaseNumber);
        }

        internal void CancelBossActionForController()
        {
            CancelBossAction();
        }

        internal void RefillHpForPhaseController()
        {
            hpGauge?.Configure(maxHp, true);
            ApplyBodyStateColor();
            bossHpBarView?.PlayPhaseRefill();
            OnHpEmptyRecovered();
        }

        internal float HpEmptyExecutionSeconds => hpEmptyExecutionSeconds;

        internal void BeginHpEmptyForState()
        {
            CancelBossAction();
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
            return !IsExecutionPaused && hpGauge != null && !hpGauge.IsEmpty;
        }

        public void RegisterActiveProjectile(EnemyProjectile projectile)
        {
            projectileTracker.Register(projectile);
        }

        public void UnregisterActiveProjectile(EnemyProjectile projectile)
        {
            projectileTracker.Unregister(projectile);
        }

        public Vector2 GetFacingDirection()
        {
            return bodyRoot != null ? (Vector2)bodyRoot.right : Vector2.right;
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
                ProjectileVfx.PlayMuzzleFlash(muzzleFlashPosition ?? position, direction, color, muzzleFlashScale);
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
                settings != null ? settings.ChargingColor : Color.white,
                settings != null ? settings.LaunchedColor : Color.white,
                aimAtPlayerWhileChargingOverride ?? (settings != null && settings.AimAtPlayerWhileCharging),
                aimAtPlayerOnLaunchOverride ?? (settings != null && settings.AimAtPlayerOnLaunch),
                suppressHoming,
                chargeSecondsOverride,
                radiusOverride,
                origin,
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

            bossEnrageBarView ??= bossCombatUiRoot != null
                ? bossCombatUiRoot.GetComponentInChildren<BossEnrageBarView>(true)
                : GetComponentInChildren<BossEnrageBarView>(true);

            bossEnrageBarView?.SetTarget(this);
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
            if (isBossCombatUiActive || !UsesBossCombatUi() || !IsPlayerDetected())
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

        internal void NotifyEnrageChanged()
        {
            EnrageChanged?.Invoke(CurrentEnragePhase, CurrentEnrageProgress, CurrentEnrageRemainingSeconds);
            bossEnrageBarView?.Refresh();
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
            PlayPlayerAttackImpact(hitPosition, hitDirection, hitColor);
            PlayEnemyHitCameraImpact(hitDirection);
        }

        private void PlayPlayerAttackImpact(Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            CombatEffectData activeEffectData = ActiveEffectData;
            Color sparkColor = activeEffectData != null ? activeEffectData.AttackImpactSparkColor : Color.Lerp(hitColor, Color.white, 0.35f);
            Color backSparkColor = activeEffectData != null ? activeEffectData.AttackImpactBackSparkColor : Color.Lerp(hitColor, new Color(1f, 0.72f, 0.12f, 1f), 0.55f);
            Color flameColor = activeEffectData != null ? activeEffectData.AttackImpactFlameColor : backSparkColor;
            Color ringColor = activeEffectData != null ? activeEffectData.AttackImpactRingColor : Color.Lerp(hitColor, Color.white, 0.35f);
            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                sparkColor,
                backSparkColor,
                flameColor,
                ringColor,
                activeEffectData != null ? activeEffectData.AttackImpactSparkCount : 14,
                activeEffectData != null ? activeEffectData.AttackImpactBackSparkCount : 6,
                activeEffectData != null ? activeEffectData.AttackImpactFlameCount : 8,
                activeEffectData != null ? activeEffectData.AttackImpactEffectScale : 0.65f);
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
            else if (isExecutionLocked || IsHpEmpty)
            {
                color = HpEmptyColor;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || IsStatusRenderer(renderers[i]) || ShouldIgnoreBodyStateRenderer(renderers[i]))
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

        protected virtual bool ShouldIgnoreBodyStateRenderer(SpriteRenderer renderer) => false;

        private void HandleDied(Health _)
        {
            projectileTracker.DestroyAll();
            SetBossCombatUiVisible(false);
            SoundManager.StopBgm();
            if (bossLivesView != null)
            {
                bossLivesView.gameObject.SetActive(false);
            }
            if (bossEnrageBarView != null)
            {
                bossEnrageBarView.gameObject.SetActive(false);
            }

            OnBossDied();
            Defeated?.Invoke(this);
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

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
