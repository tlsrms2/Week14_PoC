using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    /// <summary>
    /// 일반 적과 보스가 공통으로 사용하는 AI 컨트롤러입니다.
    /// EnemyData 기반으로 상태와 전투 동작을 관리합니다.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public sealed class EnemyAI : MonoBehaviour
    {
        [Header("기본 설정")]
        [SerializeField] private EnemyData data;
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

        [Header("보스 전투 UI")]
        [SerializeField] private GameObject bossCombatUiRoot;
        [SerializeField] private BulletBarView bossBulletBarView;

        // 런타임 캐시
        private Health health;
        private BulletGauge bullets;
        private SpriteRenderer[] renderers;
        private Transform player;

        // 상태 머신
        private EnemyStateMachine stateMachine;
        private IdleState idleState;
        private ChaseState chaseState;
        private EngageState engageState;
        private FlankState flankState;
        private DeadState deadState;

        // 전투 상태
        private Coroutine attackCoroutine;
        private bool isBulletEmpty;
        private float bulletEmptyEndsAt;
        private bool isExecutionLocked;
        private int nextTimelineIndex;
        private int currentAttackBulletTotal;
        private int currentAttackBulletRemaining;
        private readonly List<EnemyProjectile> activeProjectiles = new();
        private bool isBodyHitColorActive;
        private float bodyHitColorEndsAt;
        private bool isStaggered;
        private float staggerEndsAt;
        private Vector3 staggerBaseLocalPosition;
        private bool isBossCombatUiActive;
        private bool destroyAfterDeathQueued;

        // 공개 프로퍼티
        public EnemyData Data => data;
        public Transform Player => player;
        public Rigidbody2D Body => body;
        public Health Health => health;
        public BulletGauge Bullets => bullets;
        public Vector3 SpawnPosition { get; private set; }
        public bool IsAttacking => attackCoroutine != null;
        public bool IsStaggered => isStaggered;
        public bool IsBulletEmpty => isBulletEmpty || (bullets != null && bullets.IsEmpty);
        public bool IsExecutionLocked => isExecutionLocked;
        public LayerMask ObstacleMask => obstacleMask;

        // 상태 클래스 접근용 프로퍼티
        public IdleState IdleState => idleState;
        public ChaseState ChaseState => chaseState;
        public EngageState EngageState => engageState;
        public FlankState FlankState => flankState;
        public DeadState DeadState => deadState;
        public EnemyStateMachine StateMachine => stateMachine;

        // 생명 주기
        private void Awake()
        {
            health = GetComponent<Health>();
            bullets = GetComponent<BulletGauge>();

            if (body == null) body = GetComponent<Rigidbody2D>();
            if (body != null) body.constraints = RigidbodyConstraints2D.FreezeRotation;

            if (bodyRoot == null) bodyRoot = FindChild("Visual") ?? transform;
            if (fireOrigin == null) fireOrigin = FindChild("Gun") ?? bodyRoot;
            if (projectileOrigin == null)
                projectileOrigin = FindChild("FireOrigin") ?? FindChild("Muzzle") ?? fireOrigin;
            if (gunRecoil == null && fireOrigin != null)
                gunRecoil = fireOrigin.GetComponentInChildren<GunRecoilMotion>();
            if (lockOnIndicator == null)
                lockOnIndicator = FindChild("LockOnIndicator")?.GetComponent<SpriteRenderer>();
            if (executionIndicator == null)
                executionIndicator = FindChild("ExecutionIndicator")?.GetComponent<SpriteRenderer>();

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
            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
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

            // 탄환 초기화
            bullets.Configure(data.MaxBullets, true);

            // 상태 UI 준비
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
            ApplyBulletStateColor();

            // 플레이어 참조
            ResolvePlayer();

            stateMachine.Initialize(idleState, this);
        }

        private void Update()
        {
            if (data == null) return;

            UpdateBodyHitColor();
            UpdateStagger();

            // 사망 처리
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

            if (IsExecutionPaused)
            {
                Stop();
                return;
            }

            // 탄환 고갈 처리
            if (IsBulletEmpty)
            {
                HideAttackTiming();
                TickBulletEmpty();
                return;
            }

            ResolvePlayer();
            TryActivateBossCombatUiOnCombatStart();
            RotateToTarget();
            stateMachine.Tick(this);
        }

        // 데이터 설정
        public void SetData(EnemyData nextData)
        {
            data = nextData;
        }

        public void SetExecutionLocked(bool locked)
        {
            isExecutionLocked = locked;
            ApplyBulletStateColor();

            if (locked)
            {
                CancelAttack();
                Stop();
            }
        }

        public bool ReceivePlayerHit(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
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

            bullets.TrySpend(bulletDamage, BulletChangeSource.Hit);
            if (bullets.IsEmpty)
            {
                BeginBulletEmpty();
            }

            if (strongHit)
            {
                BeginStagger(hitDirection);
                FlashBodyHitColor();
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

        // 공격 타이밍 표시
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

        // 감지
        /// <summary>감지 범위 안에 플레이어가 있는지 확인합니다.</summary>
        public bool IsPlayerDetected()
        {
            if (player == null) return false;
            return Vector2.Distance(transform.position, player.position) <= data.DetectionRange;
        }

        /// <summary>감지 거리와 장애물 레이캐스트를 함께 확인합니다.</summary>
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

        /// <summary>공격 사거리 안에 플레이어가 있는지 확인합니다.</summary>
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

        // 이동
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

        // 공격
        /// <summary>다음 AttackTimeline을 라운드 로빈으로 선택합니다.</summary>
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

        /// <summary>타임라인 기반 공격 코루틴을 시작합니다.</summary>
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
                yield return WaitAttackSeconds(data.WindupSeconds);
            }

            // 이벤트 시각 실행
            float timelineElapsed = 0f;
            int eventIndex = 0;

            while (eventIndex < events.Count)
            {
                AttackEvent evt = events[eventIndex];
                float targetTime = Mathf.Max(0f, evt.FireTime);
                while (timelineElapsed < targetTime)
                {
                    if (IsExecutionPaused)
                    {
                        Stop();
                        yield return null;
                        continue;
                    }

                    timelineElapsed += Time.deltaTime;
                    yield return null;
                }

                yield return WaitWhileExecutionPaused();
                yield return ExecuteAttackEvent(evt);
                currentAttackBulletRemaining = Mathf.Max(0, currentAttackBulletRemaining - 1);
                ShowCurrentAttackBullets();
                eventIndex++;
            }

            // Recovery
            if (data.RecoverySeconds > 0f)
            {
                yield return WaitAttackSeconds(data.RecoverySeconds);
            }

            attackCoroutine = null;
            currentAttackBulletTotal = 0;
            currentAttackBulletRemaining = 0;
        }

        private static int CountTimelineAttacks(AttackTimeline timeline)
        {
            return timeline?.Events != null ? timeline.Events.Count : 0;
        }

        /// <summary>단일 AttackEvent의 투사체를 생성합니다.</summary>
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

        // 공격 패턴 처리
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
                    yield return WaitAttackSeconds(interval);
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
                if (IsExecutionPaused)
                {
                    Stop();
                    yield return null;
                    continue;
                }

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
            if (!CanSpawnEnemyProjectile())
            {
                return null;
            }

            EnemyProjectile projectile = EnemyProjectile.Spawn(
                data.ProjectilePrefab,
                data,
                bullets,
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

        public bool CanSpawnEnemyProjectile()
        {
            if (IsExecutionPaused || bullets == null || bullets.IsEmpty)
            {
                return false;
            }

            PruneInactiveProjectiles();
            int capacity = bullets.CurrentBullets;
            return activeProjectiles.Count < capacity;
        }

        private static bool IsExecutionPaused => PlayerCombatController.IsExecutionCinematicActive;

        private IEnumerator WaitAttackSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitWhileExecutionPaused()
        {
            while (IsExecutionPaused)
            {
                yield return null;
            }
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
            if (projectile == null)
            {
                return;
            }

            activeProjectiles.Remove(projectile);
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

        // 조준 처리
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

        public void ApplyBulletStateColor()
        {
            if (data == null || renderers == null) return;

            Color color = data.NormalColor;
            if (isStaggered)
                color = data.StaggeredColor;
            else if (isBodyHitColorActive)
                color = data.BodyHitColor;
            else if (isExecutionLocked || IsBulletEmpty)
                color = data.BulletEmptyColor;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                // 상태 UI 렌더러는 제외
                if (statusView != null && statusView.OwnsRenderer(renderers[i])) continue;

                // 총구 하위 렌더러는 몸체 색상 변경에서 제외
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
            ApplyBulletStateColor();
        }

        private void UpdateBodyHitColor()
        {
            if (!isBodyHitColorActive || Time.time < bodyHitColorEndsAt)
            {
                return;
            }

            isBodyHitColorActive = false;
            ApplyBulletStateColor();
        }

        private void BeginStagger(Vector2 hitDirection)
        {
            if (data == null || data.StaggerSeconds <= 0f || isExecutionLocked || IsBulletEmpty)
            {
                return;
            }

            if (!isStaggered)
            {
                staggerBaseLocalPosition = bodyRoot != null ? bodyRoot.localPosition : Vector3.zero;
            }

            isStaggered = true;
            staggerEndsAt = Time.time + data.StaggerSeconds;
            ApplyBulletStateColor();
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

                ApplyBulletStateColor();
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

        // 이벤트 핸들러
        private void HandleDied(Health _)
        {
            DestroyActiveProjectiles();
            if (stateMachine.CurrentState != deadState)
                stateMachine.ChangeState(deadState, this);
        }

        private void DestroyActiveProjectiles()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                EnemyProjectile projectile = activeProjectiles[i];
                if (projectile != null)
                {
                    projectile.DestroyFromOwner();
                }
            }

            activeProjectiles.Clear();
        }

        private void BeginBulletEmpty()
        {
            if (data == null || isBulletEmpty || health.IsDead) return;

            isBulletEmpty = true;
            bulletEmptyEndsAt = Time.time + data.BulletEmptyExecutionSeconds;
            CancelAttack();
            Stop();
            ApplyBulletStateColor();
        }

        private void TickBulletEmpty()
        {
            if (!isBulletEmpty) BeginBulletEmpty();
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
            ApplyBulletStateColor();
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

        // 유틸리티
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
            statusView.SetIndicators(lockOnIndicator, executionIndicator);
            statusView.Configure(data);
            statusView.SetTargets(health, bullets);
        }

        private bool UsesBossCombatUi()
        {
            return data != null && (bossCombatUiRoot != null || bossBulletBarView != null);
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
                bossBulletBarView.SetColors(data.BulletBarColor, data.EmptyBulletBarColor);
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
            bossBulletBarView?.SetTarget(bullets);
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

            return bossBulletBarView != null && bossBulletBarView.gameObject.activeInHierarchy;
        }

        private void SetBossCombatUiVisible(bool visible)
        {
            if (bossCombatUiRoot != null)
            {
                bossCombatUiRoot.SetActive(visible);
            }
            else
            {
                if (bossBulletBarView != null) bossBulletBarView.gameObject.SetActive(visible);
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
            statusView.SetIndicators(lockOnIndicator, executionIndicator);
            statusView.Configure(data);
            statusView.SetTargets(health, bullets);
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
