using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        // ── 직렬화 필드 ───────────────────────────────
        [SerializeField] private EnemyData data;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform fireOrigin;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyStatusView statusView;
        [SerializeField] private AttackTimingOutline attackTimingOutline;
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
        private int nextTimelineIndex;

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
                data.HeatAfterOverheatRatio,
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

        /// <summary>시야각 + 장애물 레이캐스트 통과 여부</summary>
        public bool CanSeePlayer()
        {
            if (player == null) return false;
            float dist = Vector2.Distance(transform.position, player.position);
            if (dist > data.DetectionRange) return false;

            // 시야각 검사
            Vector2 dirToPlayer = (player.position - transform.position).normalized;
            Vector2 forward = GetFacingDirection();
            if (Vector2.Angle(forward, dirToPlayer) > data.FieldOfViewAngle * 0.5f)
                return false;

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

                EnemyProjectile.Spawn(
                    data.ProjectilePrefab,
                    data,
                    heat,
                    origin.position,
                    dir,
                    evt.Damage);
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
            if (isExecutionLocked || (health != null && health.IsDurabilityDepleted))
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
