using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public class GraphBossAI : BossAI
    {
        [Header("Boss Graph")]
        [SerializeField, Tooltip("Boss Graph 패턴 데이터입니다. 비어 있으면 패턴을 실행하지 않습니다.")]
        private BossGraphAsset bossGraph;
        [SerializeField, Tooltip("Boss Graph 액션에서 이름으로 참조할 투사체 설정 목록입니다. 첫 항목은 기본 투사체로 사용됩니다.")]
        private List<BossGraphProjectileEntry> graphProjectiles = new()
        {
            new BossGraphProjectileEntry()
        };

        [Header("Groggy (패링 억제)")]
        [Tooltip("그로기 상태 진입/해제 시 애니메이터를 못 찾으면 사용할 대체 검색용입니다. 보통은 자동으로 BodyRoot 밑에서 찾습니다.")]
        [SerializeField] private Animator patternGroggyAnimator;

        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");

        private readonly BossGraphRunner graphRunner = new();
        private BossActionContext graphContext;
        private Coroutine patternRoutine;
        private bool isGroggy;
        private bool pendingGroggyEnter;
        private float pendingGroggySeconds;
        private float groggyRemainingSeconds;

        protected override BossGraphAsset GraphAsset => bossGraph;
        protected BossGraphAsset BossGraph => bossGraph;
        protected BossActionContext GraphContext => graphContext;
        public override bool IsDashing => graphContext != null && graphContext.IsDashing;
        protected IReadOnlyList<BossGraphProjectileEntry> GraphProjectiles => graphProjectiles;
        public bool IsGroggy => isGroggy;

        protected override BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            return ResolveProjectileSettings(graphProjectiles, projectileName);
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || GraphAsset == null || !CanStartGraphPattern())
            {
                return;
            }

            patternRoutine = StartCoroutine(RunGraphPatternLoop());
        }

        protected override void CancelBossAction()
        {
            StopGraphPattern();
        }

        protected override void OnBossDied()
        {
            StopGraphPattern();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            ResetGraphRuntime();
        }

        protected virtual bool CanStartGraphPattern()
        {
            if (isGroggy || pendingGroggyEnter)
            {
                return false;
            }

            return IsCombatStartedForState;
        }

        // 패링으로 패턴을 억제하는 액션(예: FireParrySuppressionBaitAction)이 패링 성공을 감지하면 호출한다.
        // 실제 그로기 진입(StopGraphPattern 포함)은 다음 프레임의 Update로 미룬다 — 이 호출 자체가 그래프
        // 액션의 Execute() 코루틴 안에서 일어나므로, 그 자리에서 바로 StopGraphPattern을 부르면 자기
        // 자신을 끊는 재진입 문제가 생길 수 있기 때문이다(AssassinBossAI의 은신 전환과 같은 이유).
        public void RequestGroggy(float seconds)
        {
            if (isGroggy || pendingGroggyEnter)
            {
                return;
            }

            pendingGroggyEnter = true;
            pendingGroggySeconds = Mathf.Max(0f, seconds);
        }

        // LateUpdate를 새로 선언하는 대신 BossAI의 기존 virtual Update를 정식으로 오버라이드한다 —
        // 서로 다른 클래스가 각자 private void LateUpdate()를 따로 선언해도 Unity가 전부 호출해줄
        // 거라는 가정에 기대지 않기 위함이다(하위 클래스가 override 없이 자기만의 LateUpdate를
        // 가지고 있으면 그 가정이 깨질 수 있다). 반드시 base.Update()를 호출해 기존 틱 로직을 유지한다.
        protected override void Update()
        {
            base.Update();
            TickGroggy();
        }

        private void TickGroggy()
        {
            if (isGroggy)
            {
                groggyRemainingSeconds -= EnemyTimeScale.DeltaTime;
                SetMovementVelocity(Vector2.zero);
                if (groggyRemainingSeconds <= 0f)
                {
                    EndGroggy();
                }

                return;
            }

            if (!pendingGroggyEnter)
            {
                return;
            }

            pendingGroggyEnter = false;
            StopGraphPattern();
            BeginGroggy(pendingGroggySeconds);
        }

        private void BeginGroggy(float seconds)
        {
            isGroggy = true;
            groggyRemainingSeconds = seconds;
            SetMovementVelocity(Vector2.zero);
            SetGroggyAnimatorTrigger(StunParameter);
        }

        private void EndGroggy()
        {
            isGroggy = false;
            SetGroggyAnimatorTrigger(EndStunParameter);
        }

        private void SetGroggyAnimatorTrigger(int parameter)
        {
            if (patternGroggyAnimator == null)
            {
                patternGroggyAnimator = BodyRoot != null
                    ? BodyRoot.GetComponentInChildren<Animator>(true)
                    : GetComponentInChildren<Animator>(true);
            }

            if (patternGroggyAnimator != null)
            {
                patternGroggyAnimator.SetTrigger(parameter);
            }
        }

        protected virtual BossActionContext CreateGraphContext()
        {
            return new BossActionContext(
                this,
                Stop,
                () => IsExecutionPaused,
                GraphAsset);
        }

        // 그로기/실행 연출 등 "같은 그래프 안에서" 패턴 코루틴이 잠깐 끊기는 경우 쓴다. 쿨다운/Min
        // Patterns Played 같은 페이즈별 히스토리는 유지한 채 그래프 순회 위치만 지운다.
        protected void StopGraphPattern()
        {
            StopGraphPattern(false);
        }

        // resetPatternHistory=true는 GraphAsset 자체가 바뀌는 경우(예: AssassinBossAI의 은신↔일반
        // 그래프 전환)에 쓴다. 서로 다른 BossGraphAsset은 phase.PhaseIndex가 각자 0부터 다시 매겨지므로,
        // 쿨다운/Min Patterns Played 히스토리를 그대로 들고 넘어가면 전혀 다른 그래프의 같은 인덱스
        // 페이즈끼리 기록이 섞인다. 그래서 이 경우는 페이즈 전환(OnBossPhaseChanged)과 동일하게
        // 전체 초기화를 쓴다.
        protected void StopGraphPattern(bool resetPatternHistory)
        {
            if (patternRoutine != null)
            {
                // StopCoroutine은 이렇게 깊이 중첩된 코루틴 체인에서 try/finally를 안정적으로 안
                // 돌려주므로(실측 확인됨), 코루틴을 끊기 전에 "지금 실행 중이던 패턴"을 직접 등록한다.
                graphRunner.RegisterInFlightPatternIfNeeded();
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            if (resetPatternHistory)
            {
                ResetGraphRuntime();
            }
            else
            {
                graphRunner.RestartAfterInterruption();
            }

            graphContext?.ClearPatternScopedBossChildAims();
            graphContext?.ResetBodyRootLocalOffset();
            graphContext?.ClearConductorMinionOutlineHoldRequests();
            if (graphContext?.Boss is Conductor conductor)
            {
                conductor.ClearMinionOutlinePatternVisibility();
            }

            graphContext = null;
        }

        protected void ResetGraphRuntime()
        {
            BossGraphRuntimeState.Clear(GraphAsset);
            graphRunner.Reset();
        }

        private IEnumerator RunGraphPatternLoop()
        {
            graphRunner.RestartAfterInterruption();
            graphContext = CreateGraphContext();

            try
            {
                yield return graphRunner.RunLoop(GraphAsset, graphContext);
            }
            finally
            {
                graphContext = null;
                patternRoutine = null;
            }
        }

        private static BossProjectileSettings ResolveProjectileSettings(
            IReadOnlyList<BossGraphProjectileEntry> projectiles,
            string projectileName)
        {
            if (projectiles == null || projectiles.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(projectileName))
            {
                for (int i = 0; i < projectiles.Count; i++)
                {
                    BossGraphProjectileEntry entry = projectiles[i];
                    if (entry != null
                        && string.Equals(entry.ProjectileName, projectileName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Projectile;
                    }
                }

                return null;
            }

            return projectiles[0]?.Projectile;
        }
    }
}
