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
        [Tooltip("그로기 상태 진입/해제 시 켜고 끌 Animator Bool 이름입니다.")]
        [SerializeField] private string groggyAnimatorBoolName = "isGroggy";

        private readonly BossGraphRunner graphRunner = new();
        private BossActionContext graphContext;
        private Coroutine patternRoutine;
        private Animator groggyAnimator;
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

            return IsCombatStartedForState || IsPlayerDetected();
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
            SetGroggyAnimatorBool(true);
        }

        private void EndGroggy()
        {
            isGroggy = false;
            SetGroggyAnimatorBool(false);
        }

        private void SetGroggyAnimatorBool(bool value)
        {
            if (string.IsNullOrWhiteSpace(groggyAnimatorBoolName))
            {
                return;
            }

            if (groggyAnimator == null)
            {
                groggyAnimator = BodyRoot != null
                    ? BodyRoot.GetComponentInChildren<Animator>(true)
                    : GetComponentInChildren<Animator>(true);
            }

            if (groggyAnimator != null)
            {
                groggyAnimator.SetBool(groggyAnimatorBoolName, value);
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

        protected void StopGraphPattern()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            ResetGraphRuntime();
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
            graphRunner.Reset();
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
