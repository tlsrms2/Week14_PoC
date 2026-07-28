using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

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
        [Tooltip("비워두면 BodyRoot 밑의 Animator를 전부 찾아 그로기 트리거를 동일하게 보냅니다(애니메이터가 여러 개인 보스도 자동 지원). 특정 애니메이터 하나에만 보내고 싶을 때만 직접 지정하세요.")]
        [SerializeField] private Animator patternGroggyAnimator;

        private static readonly int StunParameter = Animator.StringToHash("Stun");
        private static readonly int EndStunParameter = Animator.StringToHash("EndStun");
        private static readonly int IsStunParameter = Animator.StringToHash("isStun");

        private readonly BossGraphRunner graphRunner = new();
        private BossActionContext graphContext;
        private Coroutine patternRoutine;
        private Animator[] groggyAnimators;
        private bool isGroggy;
        private bool pendingGroggyEnter;
        private float pendingGroggySeconds;
        private float groggyRemainingSeconds;
        private bool isDebugPatternControlActive;
        private bool isCinematicPatternControlActive;
        private bool isQaBehaviorPaused;

        protected override BossGraphAsset GraphAsset => bossGraph;
        protected BossGraphAsset BossGraph => bossGraph;
        protected BossActionContext GraphContext => graphContext;
        public override bool IsDashing => graphContext != null && graphContext.IsDashing;
        internal override bool AllowsCinematicMovementForState =>
            isCinematicPatternControlActive;
        protected IReadOnlyList<BossGraphProjectileEntry> GraphProjectiles => graphProjectiles;
        protected override bool CanSpawnEnemyProjectileDuringCinematic =>
            isCinematicPatternControlActive;
        public BossGraphAsset ConfiguredGraphAsset => GraphAsset;
        public bool IsGroggy => isGroggy;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool IsQaBehaviorPaused => isQaBehaviorPaused;
        internal BossGraphAsset QaGraphAsset => GraphAsset;
        internal string QaCurrentPatternId => graphRunner.CurrentPatternId;
        internal string QaForcedPatternId => graphRunner.GetForcedPatternId(GraphAsset, CurrentPhaseIndex);
#endif

        protected override BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            return ResolveProjectileSettings(graphProjectiles, projectileName);
        }

        protected override void OnBossTick()
        {
            if (isQaBehaviorPaused
                || isDebugPatternControlActive
                || isCinematicPatternControlActive
                || patternRoutine != null
                || GraphAsset == null
                || !CanStartGraphPattern())
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
            isQaBehaviorPaused = false;
            isCinematicPatternControlActive = false;
            graphRunner.CancelForcedPattern();
            StopGraphPattern();
        }

        protected override void OnDisable()
        {
            isQaBehaviorPaused = false;
            isCinematicPatternControlActive = false;
            graphRunner.CancelForcedPattern();
            StopGraphPattern();
            base.OnDisable();
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
            if (isCinematicPatternControlActive)
            {
                return;
            }

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
            PlayGroggyStunVisual();
        }

        private void EndGroggy()
        {
            isGroggy = false;

            // 그로기 지속시간이 다 됐어도 그 사이 체력이 0이 되어 처형 가능 상태(HP Empty)에
            // 들어가 있다면 EndStun 비주얼을 쏘지 않는다 — 여기서 쏘면 보스가 여전히 처형
            // 대기 중인데 애니메이션만 Idle로 풀려버리는 시각 버그가 생긴다. 처형 창이 실제로
            // 끝나는 시점(성공/실패 불문)은 OnHpEmptyRecovered가 따로 책임진다.
            if (IsHpEmpty)
            {
                return;
            }

            PlayGroggyEndStunVisual();
        }

        // HP가 바닥나 처형 가능 상태(HP Empty)에 들어갈 때도 동일한 그로기 비주얼을 쓰고 싶은
        // 하위 클래스(예: AssassinBossAI)를 위해 공개한다. isGroggy 게임플레이 상태(패턴 억제,
        // 이동 정지 등)는 건드리지 않는다 — HP Empty 동안의 패턴/이동 정지는 BossAI의
        // BeginHpEmptyForState가 이미 별도로 처리한다.
        protected void PlayGroggyStunVisual()
        {
            SetGroggyAnimatorTrigger(StunParameter);
            SetGroggyAnimatorBool(IsStunParameter, true);
        }

        protected void PlayGroggyEndStunVisual()
        {
            SetGroggyAnimatorTrigger(EndStunParameter);
            SetGroggyAnimatorBool(IsStunParameter, false);
        }

        protected void SetPatternGroggyAnimator(Animator animator)
        {
            patternGroggyAnimator = animator;
            groggyAnimators = null;
        }

        private void SetGroggyAnimatorTrigger(int parameter)
        {
            Animator[] targets = GetGroggyAnimators();
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].SetTrigger(parameter);
            }
        }

        private void SetGroggyAnimatorBool(int parameter, bool value)
        {
            Animator[] targets = GetGroggyAnimators();
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].SetBool(parameter, value);
            }
        }

        // patternGroggyAnimator를 인스펙터에서 직접 지정했다면 그것만 쓰고(기존 동작 그대로 유지),
        // 비워뒀다면 BodyRoot 밑의 Animator를 전부 찾아 broadcast한다 — Assassin처럼 애니메이터를
        // 두 개 이상 나눠 쓰는 보스도 자동으로 전부 같은 그로기 트리거를 받게 하기 위함.
        private Animator[] GetGroggyAnimators()
        {
            if (groggyAnimators != null)
            {
                return groggyAnimators;
            }

            if (patternGroggyAnimator != null)
            {
                groggyAnimators = new[] { patternGroggyAnimator };
                return groggyAnimators;
            }

            groggyAnimators = BodyRoot != null
                ? BodyRoot.GetComponentsInChildren<Animator>(true)
                : GetComponentsInChildren<Animator>(true);
            return groggyAnimators;
        }

        protected virtual BossActionContext CreateGraphContext(
            BossGraphAsset graphAsset = null,
            bool skipApproachMovement = false,
            Func<bool> executionPausedOverride = null,
            Action<EnemyProjectile> projectileFired = null,
            bool allowExecutionCinematicMotion = false)
        {
            return new BossActionContext(
                this,
                Stop,
                executionPausedOverride ?? (() => IsExecutionPaused || isQaBehaviorPaused),
                graphAsset != null ? graphAsset : GraphAsset,
                skipApproachMovement,
                projectileFired,
                allowExecutionCinematicMotion);
        }

        public bool HasConfiguredGraphPattern(string patternId)
        {
            BossGraphPattern pattern = GraphAsset != null ? GraphAsset.GetPattern(patternId) : null;
            return pattern != null && pattern.NodeKeys != null && pattern.NodeKeys.Count > 0;
        }

        internal bool TryRunCinematicPatternOnce(
            string patternId,
            Action<EnemyProjectile> projectileFired)
        {
            BossGraphAsset graph = GraphAsset;
            if (!gameObject.activeInHierarchy || !HasConfiguredGraphPattern(patternId))
            {
                Debug.LogWarning(
                    $"{name}: Boss Graph에서 처형용 패턴 '{patternId}'을 실행할 수 없습니다.",
                    this);
                return false;
            }

            isCinematicPatternControlActive = true;
            StopGraphPattern();
            BossGraphPattern pattern = graph.GetPattern(patternId);
            graphRunner.RestartAfterInterruption();
            graphContext = CreateGraphContext(
                graph,
                skipApproachMovement: true,
                executionPausedOverride: () => isQaBehaviorPaused,
                projectileFired: projectileFired,
                allowExecutionCinematicMotion: true);
            patternRoutine = StartCoroutine(RunCinematicPatternOnce(graph, pattern, graphContext));
            return true;
        }

        internal void StopCinematicPattern()
        {
            if (!isCinematicPatternControlActive)
            {
                return;
            }

            StopGraphPattern();
            isCinematicPatternControlActive = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool TrySetQaBehaviorPaused(bool paused)
        {
            if (!isActiveAndEnabled || Health == null || Health.IsDead)
            {
                return false;
            }

            if (isQaBehaviorPaused == paused)
            {
                return true;
            }

            isQaBehaviorPaused = paused;
            if (paused)
            {
                StopGraphPattern();
                StopAllMinions();
                Stop();
            }
            else
            {
                ResumeAllMinions();
            }

            return true;
        }

        internal bool TrySetQaForcedPattern(string patternId)
        {
            BossGraphAsset graph = GraphAsset;
            return isActiveAndEnabled
                && Health != null
                && !Health.IsDead
                && graph != null
                && graphRunner.TrySetForcedPattern(graph, CurrentPhaseIndex, patternId);
        }

        internal void CancelQaForcedPattern()
        {
            graphRunner.CancelForcedPattern();
        }
#endif

        internal bool IsDebugPatternRunning => isDebugPatternControlActive && patternRoutine != null;

        internal void SetDebugPatternControlActive(bool active)
        {
            if (isDebugPatternControlActive == active)
            {
                return;
            }

            StopGraphPattern();
            isDebugPatternControlActive = active;
        }

        internal bool TryRunDebugPatternOnce(BossGraphAsset graph, string patternId)
        {
            if (graph == null || string.IsNullOrWhiteSpace(patternId))
            {
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"{name}: 비활성 상태에서는 디버그 패턴을 실행할 수 없습니다.", this);
                return false;
            }

            SetDebugPatternControlActive(true);
            BossGraphPattern pattern = graph.GetPattern(patternId);
            if (pattern == null || pattern.NodeKeys == null || pattern.NodeKeys.Count == 0)
            {
                Debug.LogWarning($"{name}: 그래프에서 실행 가능한 패턴 '{patternId}'을 찾지 못했습니다.", this);
                return false;
            }

            StopGraphPattern();
            graphRunner.RestartAfterInterruption();
            graphContext = CreateGraphContext(graph, skipApproachMovement: true);
            patternRoutine = StartCoroutine(RunDebugPatternOnce(graph, pattern, graphContext));
            return true;
        }

        internal void StopDebugPattern()
        {
            if (isDebugPatternControlActive)
            {
                StopGraphPattern();
            }
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

            // StopCoroutine이 액션 내부 finally를 보장하지 않으므로 대시 도중 중단돼도
            // 플레이어 충돌 무시 상태가 남지 않게 컨텍스트 폐기 전에 직접 복구한다.
            graphContext?.ClearAnimationBools();
            graphContext?.SetDashing(false);
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

        private IEnumerator RunDebugPatternOnce(
            BossGraphAsset graph,
            BossGraphPattern pattern,
            BossActionContext context)
        {
            try
            {
                yield return graphRunner.RunPatternOnce(graph, pattern, context);
            }
            finally
            {
                Stop();
                if (graphContext == context)
                {
                    graphContext = null;
                }

                patternRoutine = null;
            }
        }

        private IEnumerator RunCinematicPatternOnce(
            BossGraphAsset graph,
            BossGraphPattern pattern,
            BossActionContext context)
        {
            try
            {
                yield return graphRunner.RunPatternOnce(graph, pattern, context);
            }
            finally
            {
                Stop();
                if (graphContext == context)
                {
                    graphContext = null;
                }

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
