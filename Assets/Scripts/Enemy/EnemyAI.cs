using System.Collections;
using UnityEngine;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    /// <summary>
    /// 모든 적(일반/보스)이 사용하는 단일 AI 컨트롤러.
    /// EnemyData SO 기반으로 동작하며 FSM으로 상태를 관리한다.
    /// 기존 EnemyCombatController를 완전 대체.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(HeatGauge))]
    public sealed class EnemyAI : MonoBehaviour
    {
        private const float BossParryInterceptDelaySeconds = 0.035f;

        // ── 직렬화 필드 ───────────────────────────────
        [SerializeField] private EnemyData data;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform fireOrigin;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyStatusView statusView;
        [SerializeField] private AttackTimingOutline attackTimingOutline;
        [SerializeField] private GunRecoilMotion gunRecoil;
        [SerializeField] private LayerMask obstacleMask;

        [Header("보스 전투 UI")]
        [SerializeField] private GameObject bossCombatUiRoot;
        [SerializeField] private DurabilityBarView bossDurabilityBarView;
        [SerializeField] private HeatBarView bossHeatBarView;

        // ── 캐시 ─────────────────────────────────────
        private Health health;
        private HeatGauge heat;
        private SpriteRenderer[] renderers;
        private Transform player;

        // ── FSM ──────────────────────────────────────
        private EnemyStateMachine stateMachine;
        private IdleState idleState;
        private ChaseState chaseState;
        private EngageState engageState;
        private FlankState flankState;
        private DeadState deadState;

        // ── 공격 ─────────────────────────────────────
        private Coroutine attackCoroutine;
        private bool isDurabilityDepleted;
        private bool isExecutionLocked;
        private float durabilityDepletedEndsAt;
        private float nextBossParryTime;
        private float nextBossDefenseTime;
        private float lastBossPlayerAttackTime;
        private int bossPlayerAttackPressureCount;
        private int nextTimelineIndex;
        private int currentAttackBulletTotal;
        private int currentAttackBulletRemaining;
        private bool isBodyHitColorActive;
        private float bodyHitColorEndsAt;
        private bool isStaggered;
        private float staggerEndsAt;
        private Vector3 staggerBaseLocalPosition;
        private bool isBossCombatUiActive;
        private bool destroyAfterDeathQueued;

        // ── 공개 프로퍼티 ─────────────────────────────
        public EnemyData Data => data;
        public Transform Player => player;
        public Rigidbody2D Body => body;
        public Health Health => health;
        public HeatGauge Heat => heat;
        public Vector3 SpawnPosition { get; private set; }
        public bool IsAttacking => attackCoroutine != null;
        public bool IsOverheated => heat != null && heat.IsOverheated;
        public bool IsStaggered => isStaggered;
        public bool IsDurabilityDepleted => isDurabilityDepleted || (health != null && health.IsDurabilityDepleted);
        public bool IsExecutionLocked => isExecutionLocked;
        public LayerMask ObstacleMask => obstacleMask;

        // 상태 접근자 (상태 클래스에서 사용)
        public IdleState IdleState => idleState;
        public ChaseState ChaseState => chaseState;
        public EngageState EngageState => engageState;
        public FlankState FlankState => flankState;
        public DeadState DeadState => deadState;
        public EnemyStateMachine StateMachine => stateMachine;

        // ── 생명주기 ─────────────────────────────────
        private void Awake()
        {
            health = GetComponent<Health>();
            heat = GetComponent<HeatGauge>();

            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body != null) body.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (bodyRoot == null) bodyRoot = FindChild("Visual") ?? transform;
            if (fireOrigin == null) fireOrigin = FindChild("Gun") ?? bodyRoot;
            if (projectileOrigin == null)
                projectileOrigin = FindChild("FireOrigin") ?? FindChild("Muzzle") ?? fireOrigin;
            if (gunRecoil == null && fireOrigin != null)
                gunRecoil = fireOrigin.GetComponentInChildren<GunRecoilMotion>();

            renderers = GetComponentsInChildren<SpriteRenderer>(true);

            // 상태 인스턴스 생성
            idleState = new IdleState();
            chaseState = new ChaseState();
            engageState = new EngageState();
            flankState = new FlankState();
            deadState = new DeadState();
            stateMachine = new EnemyStateMachine();
        }

        private void OnEnable()
        {
            if (heat != null)
            {
                heat.Overheated += HandleOverheated;
                heat.Recovered += HandleRecovered;
            }
            if (health != null)
            {
                health.DurabilityDepleted += HandleDurabilityDepleted;
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (heat != null)
            {
                heat.Overheated -= HandleOverheated;
                heat.Recovered -= HandleRecovered;
            }
            if (health != null)
            {
                health.DurabilityDepleted -= HandleDurabilityDepleted;
                health.Died -= HandleDied;
            }

        }

        private void Start()
        {
            if (data == null)
            {
                Debug.LogWarning($"{nameof(EnemyAI)} requires {nameof(EnemyData)}.", this);
                return;
            }

            SpawnPosition = transform.position;

            // 체력/열 초기화
            health.SetDeferDeathAtZero(true);
            health.SetMaxDurability(data.MaxDurability, true);
            heat.Configure(
                data.MaxHeat,
                data.HeatCoolingPerSecond,
                data.OverheatSeconds,
                true);

            // 상태바 UI
            if (UsesBossCombatUi())
            {
                SuppressEnemyStatusView();
                PrepareBossCombatUi();
                BindBossCombatUiTargetsIfVisible();
                SetBossCombatUiVisible(false);
            }
            else
            {
                EnsureStatusView();
            }
            ApplyHeatStateColor();

            // 플레이어 참조
            ResolvePlayer();

            stateMachine.Initialize(idleState, this);
        }

        private void Update()
        {
            if (data == null) return;

            UpdateBodyHitColor();
            UpdateStagger();

            // 사망 체크
            if (health.IsDead)
            {
                HideAttackTiming();
                if (stateMachine.CurrentState != deadState)
                    stateMachine.ChangeState(deadState, this);
                return;
            }

            // 처형 잠금
            if (isExecutionLocked)
            {
                HideAttackTiming();
                Stop();
                return;
            }

            // 내구도 고갈 처리
            if (isDurabilityDepleted || health.IsDurabilityDepleted)
            {
                HideAttackTiming();
                TickDurabilityDepleted();
                return;
            }

            // 과열 시 행동 정지
            if (heat.IsOverheated)
            {
                HideAttackTiming();
                Stop();
                CancelAttack();
                return;
            }

            if (isStaggered)
            {
                HideAttackTiming();
                Stop();
                return;
            }

            ResolvePlayer();
            TryActivateBossCombatUiOnCombatStart();
            RotateToTarget();
            stateMachine.Tick(this);
        }

        // ── 외부 설정 ─────────────────────────────────
        public void SetData(EnemyData nextData)
        {
            data = nextData;
        }

        public void SetExecutionLocked(bool locked)
        {
            isExecutionLocked = locked;
            ApplyHeatStateColor();

            if (locked)
            {
                CancelAttack();
                Stop();
            }
        }

        public bool CanHandleBossParryOnShotFired(Vector3 shotPosition, Vector2 shotDirection)
        {
            BossEnemyData bossData = data as BossEnemyData;
            return bossData != null
                && bossData.CanParryPlayerAttacks
                && heat != null
                && !heat.IsOverheated
                && !isExecutionLocked
                && !IsDurabilityDepleted
                && Time.time >= nextBossParryTime
                && IsPlayerAttackInFront(shotDirection, bossData.PlayerAttackParryAngleDegrees)
                && IsShotAimedAtBoss(shotPosition, shotDirection);
        }

        public bool HandleBossParryOnShotFired(Vector3 shotPosition, Vector2 shotDirection)
        {
            BossEnemyData bossData = data as BossEnemyData;
            if (!CanHandleBossParryOnShotFired(shotPosition, shotDirection))
            {
                return false;
            }

            int pressureCount = GetBossPlayerAttackPressureCount(bossData);
            RegisterBossPlayerAttackPressure();
            nextBossParryTime = Time.time + bossData.PlayerAttackParryCooldown;

            if (Random.value > GetBossParryChance(bossData, pressureCount))
            {
                return false;
            }

            HandleBossParryPlayerAttack(bossData, transform.position, shotDirection);
            return true;
        }

        public bool ReceivePlayerHit(float damage, float heatAmount, float heatCoolingSuppressSeconds, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (health == null || health.IsDead || health.IsDurabilityDepleted)
            {
                return false;
            }

            if (TryHandleBossPlayerAttackResponse(hitPosition, hitDirection))
            {
                return true;
            }

            health.TakeDamage(damage);
            if (strongHit)
            {
                BeginStagger(hitDirection);
                FlashBodyHitColor();
            }

            if (heat != null && heatAmount > 0f)
            {
                heat.AddHeat(heatAmount, HeatChangeSource.Hit);
                heat.SuppressCooling(heatCoolingSuppressSeconds);
                Stop();
            }

            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                GetAttackImpactSparkColor(hitColor),
                GetAttackImpactBackSparkColor(hitColor),
                GetAttackImpactFlameColor(hitColor),
                GetAttackImpactRingColor(hitColor),
                data != null ? data.AttackImpactSparkCount : 14,
                data != null ? data.AttackImpactBackSparkCount : 6,
                data != null ? data.AttackImpactFlameCount : 8,
                data != null ? data.AttackImpactEffectScale : 0.65f);

            if (strongHit)
            {
                PlayEnemyHitCameraImpact(hitDirection);
            }

            return true;
        }

        // ── 피격 이펙트 색상 ─────────────────────────
        private bool TryHandleBossPlayerAttackResponse(Vector3 hitPosition, Vector2 hitDirection)
        {
            BossEnemyData bossData = data as BossEnemyData;
            if (bossData == null || heat == null || heat.IsOverheated || isExecutionLocked || IsDurabilityDepleted)
            {
                return false;
            }

            int pressureCount = GetBossPlayerAttackPressureCount(bossData);
            bool canParry = bossData.CanParryPlayerAttacks
                && Time.time >= nextBossParryTime
                && IsPlayerAttackInFront(hitDirection, bossData.PlayerAttackParryAngleDegrees)
                && Random.value <= GetBossParryChance(bossData, pressureCount);
            bool canDefend = bossData.CanDefendPlayerAttacks
                && Time.time >= nextBossDefenseTime
                && IsPlayerAttackInFront(hitDirection, bossData.PlayerAttackDefenseAngleDegrees)
                && Random.value <= bossData.PlayerAttackDefenseChance;

            RegisterBossPlayerAttackPressure();

            if (canParry)
            {
                nextBossParryTime = Time.time + bossData.PlayerAttackParryCooldown;
                HandleBossParryPlayerAttack(bossData, hitPosition, hitDirection);
                return true;
            }

            if (canDefend)
            {
                nextBossDefenseTime = Time.time + bossData.PlayerAttackDefenseCooldown;
                HandleBossDefendPlayerAttack(bossData, hitPosition, hitDirection);
                return true;
            }

            return false;
        }

        private bool IsPlayerAttackInFront(Vector2 hitDirection, float angleDegrees)
        {
            if (angleDegrees >= 359.5f)
            {
                return true;
            }

            Vector2 toAttacker = hitDirection.sqrMagnitude > 0.0001f ? -hitDirection.normalized : -GetFacingDirection();
            return Vector2.Angle(GetFacingDirection(), toAttacker) <= angleDegrees * 0.5f;
        }

        private bool IsShotAimedAtBoss(Vector3 shotPosition, Vector2 shotDirection)
        {
            if (shotDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            Vector2 direction = shotDirection.normalized;
            Vector2 toBoss = (Vector2)transform.position - (Vector2)shotPosition;
            float forwardDistance = Vector2.Dot(direction, toBoss);
            if (forwardDistance <= 0f)
            {
                return false;
            }

            float sideDistance = Mathf.Abs(Vector2.Dot(new Vector2(-direction.y, direction.x), toBoss));
            return sideDistance <= GetBossDefenseRadius();
        }

        private int GetBossPlayerAttackPressureCount(BossEnemyData bossData)
        {
            if (bossData.PlayerAttackPressureResetSeconds > 0f
                && Time.time - lastBossPlayerAttackTime > bossData.PlayerAttackPressureResetSeconds)
            {
                bossPlayerAttackPressureCount = 0;
            }

            return bossPlayerAttackPressureCount;
        }

        private void RegisterBossPlayerAttackPressure()
        {
            lastBossPlayerAttackTime = Time.time;
            bossPlayerAttackPressureCount++;
        }

        private void ResetBossPlayerAttackPressure()
        {
            bossPlayerAttackPressureCount = 0;
            lastBossPlayerAttackTime = Time.time;
        }

        private static float GetBossParryChance(BossEnemyData bossData, int pressureCount)
        {
            float maxChance = Mathf.Max(bossData.PlayerAttackParryChance, bossData.MaxPlayerAttackParryChance);
            float chance = bossData.PlayerAttackParryChance
                + Mathf.Max(0, pressureCount) * bossData.PlayerAttackParryChanceIncrease;
            return Mathf.Clamp(chance, 0f, maxChance);
        }

        private void HandleBossParryPlayerAttack(BossEnemyData bossData, Vector3 hitPosition, Vector2 hitDirection)
        {
            ResetBossPlayerAttackPressure();
            Vector2 responseDirection = hitDirection.sqrMagnitude > 0.0001f ? -hitDirection.normalized : GetFacingDirection();
            ProjectileVfx.PlayParry(
                hitPosition,
                responseDirection,
                data.BossParrySparkColor,
                data.BossParryRingColor,
                data.BossParryGlitterColor,
                Mathf.Max(18, data.AttackImpactSparkCount + data.AttackImpactBackSparkCount),
                10,
                0.16f,
                0.24f,
                0.14f,
                Mathf.Max(6, data.AttackImpactFlameCount),
                Mathf.Max(0.7f, data.AttackImpactEffectScale));
            StartCoroutine(DelayedBossParryIntercept(responseDirection));
            AddHeatToPlayerWithoutOverheat(bossData.PlayerHeatOnBossParry, bossData.PlayerHeatCoolingSuppressSeconds);
            PlayEnemyHitCameraImpact(responseDirection);
        }

        private void HandleBossDefendPlayerAttack(BossEnemyData bossData, Vector3 hitPosition, Vector2 hitDirection)
        {
            Vector2 responseDirection = hitDirection.sqrMagnitude > 0.0001f ? -hitDirection.normalized : GetFacingDirection();
            PlayBossDefenseArc(bossData, responseDirection, hitPosition);
            ProjectileVfx.PlayDefense(
                hitPosition,
                responseDirection,
                data.BossDefenseSparkColor,
                data.BossDefenseRingColor,
                Mathf.Max(8, data.AttackImpactSparkCount + data.AttackImpactBackSparkCount),
                0.18f,
                Mathf.Max(0.45f, data.AttackImpactEffectScale));
            AddHeatToBoss(bossData.BossHeatOnDefense);
            PlayEnemyHitCameraImpact(responseDirection);
        }

        private void DestroyAllPlayerProjectilesByBossParry(Vector2 responseDirection)
        {
            PlayerProjectile[] projectiles = FindObjectsByType<PlayerProjectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projectiles.Length; i++)
            {
                PlayerProjectile projectile = projectiles[i];
                if (projectile == null)
                {
                    continue;
                }

                FireBossParryInterceptShot(projectile.transform.position);
                ProjectileVfx.PlayParry(
                    projectile.transform.position,
                    responseDirection,
                    data.BossParrySparkColor,
                    data.BossParryRingColor,
                    Mathf.Max(8, data.AttackImpactSparkCount),
                    0.12f);
                projectile.DestroyAfterParryResolved();
            }
        }

        private IEnumerator DelayedBossParryIntercept(Vector2 responseDirection)
        {
            yield return new WaitForSeconds(BossParryInterceptDelaySeconds);

            if (data == null || heat == null || heat.IsOverheated || isExecutionLocked || IsDurabilityDepleted)
            {
                yield break;
            }

            DestroyAllPlayerProjectilesByBossParry(responseDirection);
        }

        private void FireBossParryInterceptShot(Vector3 targetPosition)
        {
            Transform origin = projectileOrigin != null ? projectileOrigin : fireOrigin != null ? fireOrigin : transform;
            Vector2 direction = targetPosition - origin.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = GetFacingDirection();
            }

            Vector2 normalizedDirection = direction.normalized;
            RotateRight(fireOrigin, normalizedDirection);
            ProjectileVfx.PlayMuzzleFlash(origin.position, normalizedDirection, data.BossParrySparkColor, 0.85f);
            ProjectileVfx.PlayShotLine(origin.position, targetPosition, data.BossParryRingColor, 0.09f, 0.035f);
            gunRecoil?.Play(normalizedDirection);
        }

        private void PlayBossDefenseArc(BossEnemyData bossData, Vector2 responseDirection, Vector3 hitPosition)
        {
            Vector3 center = transform.position;
            float radius = GetBossDefenseRadius();
            Color color = data.BossDefenseRingColor;
            color.a = Mathf.Max(color.a, 0.85f);
            ProjectileVfx.PlayDefenseArc(
                center,
                responseDirection,
                360f,
                radius,
                color,
                0.65f,
                0.035f);
        }

        private float GetBossDefenseRadius()
        {
            Bounds bounds = new(transform.position, Vector3.zero);
            bool hasBounds = false;

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer spriteRenderer = renderers[i];
                    if (spriteRenderer == null || !spriteRenderer.enabled)
                    {
                        continue;
                    }

                    if (statusView != null && statusView.OwnsRenderer(spriteRenderer))
                    {
                        continue;
                    }

                    if (fireOrigin != null && fireOrigin != bodyRoot
                        && (spriteRenderer.transform == fireOrigin || spriteRenderer.transform.IsChildOf(fireOrigin)))
                    {
                        continue;
                    }

                    if (!hasBounds)
                    {
                        bounds = spriteRenderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(spriteRenderer.bounds);
                    }
                }
            }

            float radius = hasBounds
                ? Mathf.Max(bounds.extents.x, bounds.extents.y)
                : 0.45f;
            return Mathf.Max(0.55f, radius + 0.18f);
        }

        private void AddHeatToPlayerWithoutOverheat(float amount, float suppressSeconds)
        {
            PlayerCombatController playerCombat = PlayerCombatController.Active;
            if (playerCombat == null && player != null)
            {
                playerCombat = player.GetComponent<PlayerCombatController>();
            }

            if (playerCombat == null)
            {
                return;
            }

            if (amount > 0f)
            {
                playerCombat.Heat?.AddHeatWithoutOverheat(amount, HeatChangeSource.Parry);
            }

            if (suppressSeconds > 0f)
            {
                playerCombat.SuppressHeatCooling(suppressSeconds);
            }
        }

        private void AddHeatToBoss(float amount)
        {
            if (heat == null || amount <= 0f)
            {
                return;
            }

            heat.AddHeat(amount, HeatChangeSource.Defense);
        }

        private Color GetAttackImpactSparkColor(Color hitColor)
        {
            if (data != null)
            {
                return data.AttackImpactSparkColor;
            }

            Color color = Color.Lerp(hitColor, Color.white, 0.35f);
            color.a = hitColor.a;
            return color;
        }

        private Color GetAttackImpactBackSparkColor(Color hitColor)
        {
            if (data != null)
            {
                return data.AttackImpactBackSparkColor;
            }

            Color color = Color.Lerp(hitColor, new Color(1f, 0.72f, 0.12f, 1f), 0.55f);
            color.a = hitColor.a;
            return color;
        }

        private Color GetAttackImpactFlameColor(Color hitColor)
        {
            if (data != null)
            {
                return data.AttackImpactFlameColor;
            }

            Color color = Color.Lerp(hitColor, new Color(1f, 0.72f, 0.12f, 1f), 0.55f);
            color.a = hitColor.a;
            return color;
        }

        private Color GetAttackImpactRingColor(Color hitColor)
        {
            if (data != null)
            {
                return data.AttackImpactRingColor;
            }

            Color color = Color.Lerp(hitColor, Color.white, 0.35f);
            color.a = hitColor.a * 0.72f;
            return color;
        }

        // ── 공격 타이밍 표시 ─────────────────────────
        public void ShowAttackTiming(float remainingSeconds, float durationSeconds)
        {
            ShowAttackTiming(remainingSeconds, durationSeconds, 0, 0);
        }

        public void ShowAttackTiming(float remainingSeconds, float durationSeconds, int loadedBulletCount, int totalBulletCount)
        {
            if (remainingSeconds <= 0f || durationSeconds <= 0f)
            {
                if (totalBulletCount > 0)
                {
                    EnsureAttackTimingOutline();
                    attackTimingOutline.ShowBullets(loadedBulletCount, totalBulletCount);
                }
                else
                {
                    HideAttackTiming();
                }

                return;
            }

            EnsureAttackTimingOutline();
            attackTimingOutline.Show(remainingSeconds, durationSeconds, loadedBulletCount, totalBulletCount);
        }

        public void ShowCurrentAttackBullets()
        {
            if (currentAttackBulletTotal <= 0)
            {
                HideAttackTiming();
                return;
            }

            EnsureAttackTimingOutline();
            attackTimingOutline.ShowBullets(currentAttackBulletRemaining, currentAttackBulletTotal);
        }

        public void HideAttackTiming()
        {
            if (attackTimingOutline != null)
            {
                attackTimingOutline.Hide();
            }
        }

        // ── 감지 ─────────────────────────────────────
        /// <summary>감지 범위 안에 플레이어가 있는지</summary>
        public bool IsPlayerDetected()
        {
            if (player == null) return false;
            return Vector2.Distance(transform.position, player.position) <= data.DetectionRange;
        }

        /// <summary>감지 거리 + 장애물 레이캐스트 통과 여부</summary>
        public bool CanSeePlayer()
        {
            if (player == null) return false;
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist > data.DetectionRange) return false;

            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            // 장애물 레이캐스트
            RaycastHit2D hit = Physics2D.Raycast(transform.position, dirToPlayer, dist, obstacleMask);
            return hit.collider == null;
        }

        /// <summary>사정거리 안에 플레이어가 있는지</summary>
        public bool IsPlayerInAttackRange()
        {
            if (player == null) return false;
            return Vector2.Distance(transform.position, player.position) <= data.AttackRange;
        }

        public float DistanceToPlayer()
        {
            if (player == null) return float.MaxValue;
            return Vector2.Distance(transform.position, player.position);
        }

        // ── 이동 ─────────────────────────────────────
        public void MoveToward(Vector2 target)
        {
            if (body == null) return;
            Vector2 dir = (target - (Vector2)transform.position).normalized;
            body.linearVelocity = dir * data.MoveSpeed;
        }

        public void Stop()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        // ── 공격 ─────────────────────────────────────
        /// <summary>다음 AttackTimeline을 선택하여 실행 (Round-Robin)</summary>
        public AttackTimeline SelectNextTimeline()
        {
            var timelines = data.AttackTimelines;
            if (timelines == null || timelines.Count == 0) return null;

            var timeline = timelines[nextTimelineIndex];
            nextTimelineIndex = (nextTimelineIndex + 1) % timelines.Count;
            return timeline;
        }

        public int GetNextTimelineAttackCount()
        {
            var timelines = data.AttackTimelines;
            if (timelines == null || timelines.Count == 0)
            {
                return 0;
            }

            return CountTimelineAttacks(timelines[nextTimelineIndex]);
        }

        /// <summary>타임라인 기반 공격 코루틴 시작</summary>
        public void StartAttack(AttackTimeline timeline)
        {
            if (timeline == null || attackCoroutine != null) return;
            currentAttackBulletTotal = CountTimelineAttacks(timeline);
            currentAttackBulletRemaining = currentAttackBulletTotal;
            ShowCurrentAttackBullets();
            attackCoroutine = StartCoroutine(ExecuteTimeline(timeline));
        }

        public void CancelAttack()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }

            currentAttackBulletTotal = 0;
            currentAttackBulletRemaining = 0;
        }

        private IEnumerator ExecuteTimeline(AttackTimeline timeline)
        {
            var events = timeline.Events;
            if (events == null || events.Count == 0)
            {
                attackCoroutine = null;
                yield break;
            }

            // Windup
            if (data.WindupSeconds > 0f)
            {
                yield return new WaitForSeconds(data.WindupSeconds);
            }

            // 이벤트 순차 실행
            float timelineStartedAt = Time.time;
            int eventIndex = 0;

            while (eventIndex < events.Count)
            {
                AttackEvent evt = events[eventIndex];
                float waitSeconds = timelineStartedAt + evt.FireTime - Time.time;
                if (waitSeconds > 0f)
                {
                    yield return new WaitForSeconds(waitSeconds);
                }

                yield return ExecuteAttackEvent(evt);
                currentAttackBulletRemaining = Mathf.Max(0, currentAttackBulletRemaining - 1);
                ShowCurrentAttackBullets();
                eventIndex++;
            }

            // Recovery
            if (data.RecoverySeconds > 0f)
            {
                yield return new WaitForSeconds(data.RecoverySeconds);
            }

            attackCoroutine = null;
            currentAttackBulletTotal = 0;
            currentAttackBulletRemaining = 0;
        }

        private static int CountTimelineAttacks(AttackTimeline timeline)
        {
            return timeline?.Events != null ? timeline.Events.Count : 0;
        }

        /// <summary>단일 AttackEvent의 발사체 생성</summary>
        public void FireProjectiles(AttackEvent evt)
        {
            FireDirectSpread(evt);
        }

        private IEnumerator ExecuteAttackEvent(AttackEvent evt)
        {
            if (evt == null)
            {
                yield break;
            }

            switch (evt.PatternKind)
            {
                case EnemyAttackPatternKind.LeftCircleSweep:
                    yield return FireLeftCircleSweep(evt);
                    break;
                case EnemyAttackPatternKind.DashTrail:
                    yield return FireDashTrail(evt);
                    break;
                default:
                    FireDirectSpread(evt);
                    break;
            }
        }

        private void FireDirectSpread(AttackEvent evt)
        {
            if (data.ProjectilePrefab == null || player == null) return;

            Transform origin = projectileOrigin != null ? projectileOrigin : fireOrigin;
            Vector2 targetPosition = GetPredictedProjectileTargetPosition(origin.position);
            Vector2 baseDir = (targetPosition - (Vector2)origin.position).normalized;
            float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

            int count = evt.BulletCount;
            float spread = evt.SpreadAngle;
            float startAngle = baseAngle - spread * 0.5f;
            float step = count > 1 ? spread / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? startAngle + step * i : baseAngle;
                Vector2 dir = new(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));

                SpawnEnemyProjectile(origin.position, dir, true);
            }
        }

        // ── 공격 패턴 처리 ─────────────────────────────
        private IEnumerator FireLeftCircleSweep(AttackEvent evt)
        {
            if (data.ProjectilePrefab == null || player == null)
            {
                yield break;
            }

            int count = Mathf.Max(1, evt.BulletCount);
            float duration = Mathf.Max(0f, evt.PatternDuration);
            float interval = count > 1 && duration > 0f ? duration / (count - 1) : 0f;
            Vector2 center = transform.position;
            Vector2 toPlayer = (Vector2)player.position - center;
            if (toPlayer.sqrMagnitude <= 0.0001f)
            {
                toPlayer = Vector2.left;
            }

            float radius = Mathf.Max(data.ProjectileRadius * 3f, toPlayer.magnitude);
            float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            float arcAngle = evt.SpreadAngle > 0f ? evt.SpreadAngle : 180f;
            float startAngle = baseAngle + 90f;
            float step = count > 1 ? arcAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle - step * i;
                Vector2 radialDirection = AngleToDirection(angle);
                Vector3 spawnPosition = center + radialDirection * radius;
                Vector2 direction = GetProjectileDirection(spawnPosition);
                SpawnEnemyProjectile(spawnPosition, direction, false);

                if (interval > 0f && i < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private IEnumerator FireDashTrail(AttackEvent evt)
        {
            if (data.ProjectilePrefab == null || player == null)
            {
                yield break;
            }

            if (body == null)
            {
                FireDirectSpread(evt);
                yield break;
            }

            int maxBulletCount = Mathf.Max(1, evt.BulletCount);
            float duration = Mathf.Max(0.05f, evt.PatternDuration);
            float dashSpeed = data.MoveSpeed * Mathf.Max(1f, evt.DashSpeedMultiplier);
            float spacing = Mathf.Max(0.05f, evt.TrailBulletSpacing);
            Vector2 dashDirection = (Vector2)player.position - (Vector2)transform.position;
            if (dashDirection.sqrMagnitude <= 0.0001f)
            {
                yield break;
            }

            dashDirection.Normalize();

            float elapsed = 0f;
            float distanceSinceLastBullet = spacing;
            int spawnedBulletCount = 0;
            Vector2 previousPosition = transform.position;

            while (elapsed < duration)
            {
                Vector2 currentPosition = transform.position;
                distanceSinceLastBullet += Vector2.Distance(previousPosition, currentPosition);

                while (spawnedBulletCount < maxBulletCount && distanceSinceLastBullet >= spacing)
                {
                    Vector2 direction = GetProjectileDirection(currentPosition);
                    SpawnEnemyProjectile(currentPosition, direction, false);
                    spawnedBulletCount++;
                    distanceSinceLastBullet -= spacing;
                }

                body.linearVelocity = dashDirection * dashSpeed;
                previousPosition = currentPosition;
                elapsed += Time.deltaTime;
                yield return null;
            }

            body.linearVelocity = Vector2.zero;
        }

        private EnemyProjectile SpawnEnemyProjectile(Vector3 position, Vector2 direction, bool playRecoil)
        {
            EnemyProjectile projectile = EnemyProjectile.Spawn(
                data.ProjectilePrefab,
                data,
                heat,
                position,
                direction);

            if (projectile != null)
            {
                ProjectileVfx.PlayMuzzleFlash(position, direction, data.ProjectileColor, 0.9f);
                if (playRecoil)
                {
                    gunRecoil?.Play(direction);
                }
            }

            return projectile;
        }

        private Vector2 GetProjectileDirection(Vector3 originPosition)
        {
            Vector2 direction = GetPredictedProjectileTargetPosition(originPosition) - (Vector2)originPosition;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            if (player != null)
            {
                direction = (Vector2)player.position - (Vector2)transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    return direction.normalized;
                }
            }

            return Vector2.left;
        }

        private static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        // ── 조준 처리 ─────────────────────────────────
        private Vector2 GetPredictedProjectileTargetPosition(Vector3 originPosition)
        {
            if (player == null)
            {
                return originPosition;
            }

            Vector2 targetPosition = player.position;
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            if (playerBody == null || data.ProjectileLeadPredictionSeconds <= 0f || data.ProjectileSpeed <= 0f)
            {
                return targetPosition;
            }

            float distance = Vector2.Distance(originPosition, targetPosition);
            float travelSeconds = distance / data.ProjectileSpeed;
            float leadSeconds = Mathf.Min(data.ProjectileLeadPredictionSeconds, travelSeconds);
            return targetPosition + playerBody.linearVelocity * leadSeconds;
        }

        private void RotateToTarget()
        {
            if (player == null) return;

            Vector2 direction = (Vector2)(player.position - bodyRoot.position);
            RotateRight(bodyRoot, direction);

            if (fireOrigin != null && fireOrigin != bodyRoot)
            {
                Transform origin = projectileOrigin != null ? projectileOrigin : fireOrigin;
                Vector2 fireDirection = (Vector2)(player.position - origin.position);
                RotateRight(fireOrigin, fireDirection);
            }
        }

        public void ApplyHeatStateColor()
        {
            if (data == null || renderers == null) return;

            Color color = data.NormalColor;
            if (isStaggered)
                color = data.StaggeredColor;
            else if (isBodyHitColorActive)
                color = data.BodyHitColor;
            else if (isExecutionLocked || (health != null && health.IsDurabilityDepleted))
                color = data.DurabilityDepletedColor;
            else if (heat != null && heat.IsOverheated)
                color = data.OverheatedColor;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                // UI 상태바 렌더러 제외
                if (statusView != null && statusView.OwnsRenderer(renderers[i])) continue;

                // 총(fireOrigin) 하위 렌더러 제외 (총과 몸체가 다를 때만)
                if (fireOrigin != null && fireOrigin != bodyRoot)
                {
                    if (renderers[i].transform == fireOrigin || renderers[i].transform.IsChildOf(fireOrigin))
                    {
                        continue;
                    }
                }

                renderers[i].color = color;
            }
        }

        private void FlashBodyHitColor()
        {
            if (data == null)
            {
                return;
            }

            isBodyHitColorActive = true;
            bodyHitColorEndsAt = Time.time + data.BodyHitColorSeconds;
            ApplyHeatStateColor();
        }

        private void UpdateBodyHitColor()
        {
            if (!isBodyHitColorActive || Time.time < bodyHitColorEndsAt)
            {
                return;
            }

            isBodyHitColorActive = false;
            ApplyHeatStateColor();
        }

        private void BeginStagger(Vector2 hitDirection)
        {
            if (data == null || data.StaggerSeconds <= 0f || isExecutionLocked || IsDurabilityDepleted)
            {
                return;
            }

            if (!isStaggered)
            {
                staggerBaseLocalPosition = bodyRoot != null ? bodyRoot.localPosition : Vector3.zero;
            }

            isStaggered = true;
            staggerEndsAt = Time.time + data.StaggerSeconds;
            Stop();
            ApplyHeatStateColor();
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

                ApplyHeatStateColor();
                return;
            }

            float distance = data != null ? data.StaggerShakeDistance : 0f;
            float frequency = data != null ? data.StaggerShakeFrequency : 0f;
            if (target == null || distance <= 0f || frequency <= 0f)
            {
                return;
            }

            float sign = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) >= 0f ? 1f : -1f;
            Vector3 offset = Vector3.right * (distance * sign);
            if (bodyRoot != null)
            {
                bodyRoot.localPosition = staggerBaseLocalPosition + offset;
            }
            else
            {
                target.position += offset * Time.deltaTime;
            }
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

        // ── 이벤트 핸들러 ─────────────────────────────
        private void HandleOverheated(HeatGauge _) => ApplyHeatStateColor();
        private void HandleRecovered(HeatGauge _) => ApplyHeatStateColor();

        private void HandleDurabilityDepleted(Health _)
        {
            BeginDurabilityDepleted();
        }

        private void HandleDied(Health _)
        {
            if (stateMachine.CurrentState != deadState)
                stateMachine.ChangeState(deadState, this);
        }

        private void BeginDurabilityDepleted()
        {
            if (data == null || isDurabilityDepleted || health.IsDead) return;

            isDurabilityDepleted = true;
            CancelAttack();
            Stop();
            durabilityDepletedEndsAt = Time.time + data.DurabilityDepletedSeconds;
            ApplyHeatStateColor();

            if (data.DurabilityDepletedSeconds <= 0f)
            {
                health.Kill();
                QueueDestroyAfterDeath();
            }
        }

        private void TickDurabilityDepleted()
        {
            if (!isDurabilityDepleted) BeginDurabilityDepleted();
            Stop();
            if (!health.IsDead && Time.time >= durabilityDepletedEndsAt)
            {
                health.Kill();
                QueueDestroyAfterDeath();
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

        // ── 유틸리티 ─────────────────────────────────
        public Vector2 GetFacingDirection()
        {
            return bodyRoot != null ? (Vector2)bodyRoot.right : Vector2.right;
        }

        private void ResolvePlayer()
        {
            if (player == null && PlayerCombatController.Active != null)
                player = PlayerCombatController.Active.transform;
        }

        private void EnsureStatusView()
        {
            if (statusView == null) statusView = GetComponentInChildren<EnemyStatusView>();
            if (statusView == null) statusView = gameObject.AddComponent<EnemyStatusView>();

            statusView.SetSuppressed(false);
            statusView.Configure(data);
            statusView.SetTargets(health, heat);
        }

        private bool UsesBossCombatUi()
        {
            return data != null
                && data is BossEnemyData
                && (bossCombatUiRoot != null || bossDurabilityBarView != null || bossHeatBarView != null);
        }

        private void PrepareBossCombatUi()
        {
            if (bossCombatUiRoot != null)
            {
                bossDurabilityBarView ??= bossCombatUiRoot.GetComponentInChildren<DurabilityBarView>(true);
                bossHeatBarView ??= bossCombatUiRoot.GetComponentInChildren<HeatBarView>(true);
            }

            if (bossDurabilityBarView != null)
            {
                bossDurabilityBarView.SetBindPlayerOnStart(false);
            }

            if (bossHeatBarView != null)
            {
                bossHeatBarView.SetBindPlayerOnStart(false);
                bossHeatBarView.SetColors(data.HeatBarColor, data.OverheatedBarColor);
            }
        }

        private void BindBossCombatUiTargetsIfVisible()
        {
            if (!IsBossCombatUiVisible())
            {
                return;
            }

            BindBossCombatUiTargets();
        }

        private void BindBossCombatUiTargets()
        {
            bossDurabilityBarView?.SetTarget(health);
            bossHeatBarView?.SetTarget(heat);
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
            BindBossCombatUiTargets();
        }

        private bool IsBossCombatUiVisible()
        {
            if (bossCombatUiRoot != null)
            {
                return bossCombatUiRoot.activeInHierarchy;
            }

            return (bossDurabilityBarView != null && bossDurabilityBarView.gameObject.activeInHierarchy)
                || (bossHeatBarView != null && bossHeatBarView.gameObject.activeInHierarchy);
        }

        private void SetBossCombatUiVisible(bool visible)
        {
            if (bossCombatUiRoot != null)
            {
                bossCombatUiRoot.SetActive(visible);
            }
            else
            {
                if (bossDurabilityBarView != null) bossDurabilityBarView.gameObject.SetActive(visible);
                if (bossHeatBarView != null) bossHeatBarView.gameObject.SetActive(visible);
            }

            isBossCombatUiActive = visible;
        }

        private void SuppressEnemyStatusView()
        {
            EnemyStatusView rootStatusView = GetComponent<EnemyStatusView>();
            if (rootStatusView == null)
            {
                rootStatusView = gameObject.AddComponent<EnemyStatusView>();
            }

            EnemyStatusView[] statusViews = GetComponentsInChildren<EnemyStatusView>(true);
            for (int i = 0; i < statusViews.Length; i++)
            {
                if (statusViews[i] != null && statusViews[i] != rootStatusView)
                {
                    statusViews[i].SetSuppressed(true);
                }
            }

            statusView = rootStatusView;
            statusView.Configure(data);
            statusView.SetTargets(health, heat);
            statusView.SetSuppressed(true);
        }

        private void EnsureAttackTimingOutline()
        {
            if (attackTimingOutline == null)
            {
                attackTimingOutline = GetComponent<AttackTimingOutline>();
            }

            if (attackTimingOutline == null)
            {
                attackTimingOutline = gameObject.AddComponent<AttackTimingOutline>();
            }

            attackTimingOutline.SetTarget(bodyRoot);
        }

        private static void RotateRight(Transform target, Vector2 direction)
        {
            if (target == null || direction.sqrMagnitude <= 0.0001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private Transform FindChild(string childName)
        {
            Transform found = transform.Find(childName);
            return found ?? FindChildRecursive(transform, childName);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName) return child;
                Transform nested = FindChildRecursive(child, childName);
                if (nested != null) return nested;
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            // 사정거리
            Transform origin = projectileOrigin != null ? projectileOrigin : fireOrigin != null ? fireOrigin : transform;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(origin.position, data.AttackRange);

            // 감지 범위
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, data.DetectionRange);

        }
    }
}
