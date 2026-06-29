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

        private readonly BossGraphRunner graphRunner = new();
        private BossActionContext graphContext;
        private Coroutine patternRoutine;

        protected override BossGraphAsset GraphAsset => bossGraph;
        protected BossGraphAsset BossGraph => bossGraph;
        protected IReadOnlyList<BossGraphProjectileEntry> GraphProjectiles => graphProjectiles;

        protected override BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            return ResolveProjectileSettings(graphProjectiles, projectileName);
        }

        protected override void OnBossTick()
        {
            if (patternRoutine != null || bossGraph == null || !CanStartGraphPattern())
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
            return IsCombatStartedForState || IsPlayerDetected();
        }

        protected virtual BossActionContext CreateGraphContext()
        {
            return new BossActionContext(
                this,
                Stop,
                () => IsExecutionPaused);
        }

        protected void StopGraphPattern()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            ResetGraphRuntime();
            graphContext?.ResetBodyRootLocalOffset();
            graphContext = null;
        }

        protected void ResetGraphRuntime()
        {
            BossGraphRuntimeState.Clear(bossGraph);
            graphRunner.Reset();
        }

        private IEnumerator RunGraphPatternLoop()
        {
            graphRunner.Reset();
            graphContext = CreateGraphContext();

            try
            {
                yield return graphRunner.RunLoop(bossGraph, graphContext);
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
