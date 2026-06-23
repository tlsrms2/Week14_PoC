using System.Collections;
using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI : BossAI
    {
        [Header("Boss Graph")]
        [SerializeField, Tooltip("할당하면 그래프 데이터로 패턴을 실행합니다.")]
        private BossGraphAsset bossGraph;

        private Coroutine patternRoutine;
        private BossActionContext graphContext;
        private readonly BossGraphRunner graphRunner = new();

        protected override BossGraphAsset GraphAsset => bossGraph;
        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("HogBgm");
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || !IsPlayerDetected() || bossGraph == null)
            {
                return;
            }

            patternRoutine = StartCoroutine(RunGraphPatternLoop());
        }

        protected override void CancelBossAction()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            graphContext?.ResetBodyRootLocalOffset();
            graphContext = null;
        }

        private IEnumerator RunGraphPatternLoop()
        {
            graphRunner.Reset();
            graphContext = CreateGraphContext();
            yield return graphRunner.RunLoop(bossGraph, graphContext);
            graphContext = null;
        }

        private BossActionContext CreateGraphContext()
        {
            return new BossActionContext(
                this,
                Stop,
                () => IsExecutionPaused,
                ApplyPendingEnrageIfAnyForGraph);
        }
    }
}
