using System.Collections;
using System.Collections.Generic;
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

        [Header("순찰 웨이포인트 (Patrol 모드 전용)")]
        [SerializeField] private List<Vector3> patrolWaypoints = new();

        // ── 캐시 ─────────────────────────────────────
        private Health health;
        private HeatGauge heat;
        private SpriteRenderer[] renderers;
        private Transform player;

        // ── FSM ──────────────────────────────────────
        private EnemyStateMachine stateMachine;
        private IdleState idleState;
        private PatrolState patrolState;
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
        private bool isBodyHitColorActive;
        private float bodyHitColorEndsAt;
        private bool isStaggered;
        private float staggerEndsAt;
        private Vector3 staggerBaseLocalPosition;

        // ── 공개 프로퍼티 ─────────────────────────────
        public EnemyData Data => data;
        public Transform Player => player;
        public Rigidbody2D Body => body;
        public Health Health => health;
        public HeatGauge Heat => heat;
        public Vector3 SpawnPosition { get; private set; }
        public IReadOnlyList<Vector3> PatrolWaypoints => patrolWaypoints;
        public bool IsAttacking => attackCoroutine != null;
        public bool IsOverheated => heat != null && heat.IsOverheated;
        public bool IsStaggered => isStaggered;
        public bool IsDurabilityDepleted => isDurabilityDepleted || (health != null && health.IsDurabilityDepleted);
        public bool IsExecutionLocked => isExecutionLocked;
        public LayerMask ObstacleMask => obstacleMask;

        // 상태 접근자 (상태 클래스에서 사용)
        public IdleState IdleState => idleState;
        public PatrolState PatrolState => patrolState;
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
            patrolState = new PatrolState();
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
            EnsureStatusView();
            ApplyHeatStateColor();

            // 플레이어 참조
            ResolvePlayer();

            // 초기 상태 결정
            IEnemyState initialState = data.PatrolMode == PatrolMode.Patrol && patrolWaypoints.Count > 0
                ? patrolState
                : idleState;
            stateMachine.Initialize(initialState, this);
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
            RotateToTarget();
            stateMachine.Tick(this);
        }

        // ── 외부 설정 ─────────────────────────────────
        public void SetData(EnemyData nextData)
        {
            data = nextData;
        }

        /// <summary>스폰 시 웨이포인트 설정</summary>
        public void SetPatrolWaypoints(List<Vector3> waypoints)
        {
            patrolWaypoints = waypoints ?? new List<Vector3>();
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
            BeginStagger(hitDirection);
            if (strongHit)
            {
                FlashBodyHitColor();
            }

            if (heat != null && heatAmount > 0f)
            {
                heat.AddHeat(heatAmount, HeatChangeSource.Hit);
                heat.SuppressCooling(heatCoolingSuppressSeconds);
                CancelAttack();
                Stop();
            }

            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                strongHit ? GetAttackImpactSparkColor(hitColor) : GetWeakAttackImpactSparkColor(),
                strongHit ? GetAttackImpactBackSparkColor(hitColor) : GetWeakAttackImpactBackSparkColor(),
                strongHit ? GetAttackImpactFlameColor(hitColor) : GetWeakAttackImpactFlameColor(),
                strongHit ? GetAttackImpactRingColor(hitColor) : GetWeakAttackImpactRingColor(),
                data != null ? (strongHit ? data.AttackImpactSparkCount : data.WeakAttackImpactSparkCount) : 14,
                data != null ? (strongHit ? data.AttackImpactBackSparkCount : data.WeakAttackImpactBackSparkCount) : 6,
                data != null ? (strongHit ? data.AttackImpactFlameCount : data.WeakAttackImpactFlameCount) : 8,
                data != null ? (strongHit ? data.AttackImpactEffectScale : data.WeakAttackImpactEffectScale) : 0.65f);

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
                Mathf.Max(8, data.WeakAttackImpactSparkCount + data.WeakAttackImpactBackSparkCount),
                0.18f,
                Mathf.Max(0.45f, data.WeakAttackImpactEffectScale));
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

        private Color GetWeakAttackImpactSparkColor()
        {
            return data != null ? data.WeakAttackImpactSparkColor : new Color(0.82f, 0.88f, 1f, 0.35f);
        }

        private Color GetWeakAttackImpactBackSparkColor()
        {
            return data != null ? data.WeakAttackImpactBackSparkColor : new Color(0.55f, 0.66f, 0.85f, 0.35f);
        }

        private Color GetWeakAttackImpactFlameColor()
        {
            return data != null ? data.WeakAttackImpactFlameColor : new Color(0.58f, 0.68f, 0.9f, 0.35f);
        }

        private Color GetWeakAttackImpactRingColor()
        {
            return data != null ? data.WeakAttackImpactRingColor : new Color(0.82f, 0.88f, 1f, 0.18f);
        }

        // ── 공격 타이밍 표시 ─────────────────────────
        public void ShowAttackTiming(float remainingSeconds, float durationSeconds)
        {
            if (remainingSeconds <= 0f || durationSeconds <= 0f)
            {
                HideAttackTiming();
                return;
            }

            EnsureAttackTimingOutline();
            attackTimingOutline.Show(remainingSeconds, durationSeconds);
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

        /// <summary>타임라인 기반 공격 코루틴 시작</summary>
        public void StartAttack(AttackTimeline timeline)
        {
            if (timeline == null || attackCoroutine != null) return;
            attackCoroutine = StartCoroutine(ExecuteTimeline(timeline));
        }

        public void CancelAttack()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
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
                Stop();
                yield return new WaitForSeconds(data.WindupSeconds);
            }

            // 이벤트 순차 실행
            float elapsed = 0f;
            int eventIndex = 0;

            while (eventIndex < events.Count)
            {
                float nextTime = events[eventIndex].FireTime;
                if (nextTime > elapsed)
                {
                    yield return new WaitForSeconds(nextTime - elapsed);
                    elapsed = nextTime;
                }

                FireProjectiles(events[eventIndex]);
                eventIndex++;
            }

            // Recovery
            if (data.RecoverySeconds > 0f)
            {
                Stop();
                yield return new WaitForSeconds(data.RecoverySeconds);
            }

            attackCoroutine = null;
        }

        /// <summary>단일 AttackEvent의 발사체 생성</summary>
        public void FireProjectiles(AttackEvent evt)
        {
            if (data.ProjectilePrefab == null || player == null) return;

            Transform origin = projectileOrigin != null ? projectileOrigin : fireOrigin;
            Vector2 baseDir = ((Vector2)player.position - (Vector2)origin.position).normalized;
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

                EnemyProjectile projectile = EnemyProjectile.Spawn(
                    data.ProjectilePrefab,
                    data,
                    heat,
                    origin.position,
                    dir);
                if (projectile != null)
                {
                    ProjectileVfx.PlayMuzzleFlash(origin.position, dir, data.ProjectileColor, 0.9f);
                    gunRecoil?.Play(dir);
                }
            }
        }

        // ── 시각 처리 ─────────────────────────────────
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
            CancelAttack();
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

            if (data.DurabilityDepletedSeconds <= 0f) health.Kill();
        }

        private void TickDurabilityDepleted()
        {
            if (!isDurabilityDepleted) BeginDurabilityDepleted();
            Stop();
            if (!health.IsDead && Time.time >= durabilityDepletedEndsAt) health.Kill();
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

            statusView.Configure(data);
            statusView.SetTargets(health, heat);
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

            // 웨이포인트
            if (patrolWaypoints is { Count: > 0 })
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolWaypoints.Count; i++)
                {
                    Gizmos.DrawWireSphere(patrolWaypoints[i], 0.2f);
                    int next = (i + 1) % patrolWaypoints.Count;
                    Gizmos.DrawLine(patrolWaypoints[i], patrolWaypoints[next]);
                }
            }
        }
    }
}
