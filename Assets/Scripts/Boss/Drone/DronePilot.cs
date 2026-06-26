using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed class DronePilot : BossAI
    {
        [Header("Boss Graph")]
        [SerializeField] private BossGraphAsset bossGraph;
        [SerializeField] private List<BossGraphProjectileEntry> graphProjectiles = new()
        {
            new BossGraphProjectileEntry()
        };

        private readonly BossGraphRunner graphRunner = new();
        private BossActionContext graphContext;
        private Coroutine patternRoutine;

        protected override BossGraphAsset GraphAsset => bossGraph;

        protected override BossProjectileSettings ResolveGraphProjectileSettings(string projectileName)
        {
            if (graphProjectiles == null || graphProjectiles.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(projectileName))
            {
                for (int i = 0; i < graphProjectiles.Count; i++)
                {
                    BossGraphProjectileEntry entry = graphProjectiles[i];
                    if (entry != null
                        && string.Equals(entry.ProjectileName, projectileName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Projectile;
                    }
                }

                return null;
            }

            return graphProjectiles[0]?.Projectile;
        }

        protected override void OnBossTick()
        {
            if (!IsPlayerDetected())
            {
                return;
            }

            if (bossGraph == null)
            {
                return;
            }

            if (patternRoutine == null)
            {
                patternRoutine = StartCoroutine(RunGraphPatternLoop());
            }
        }

        protected override void CancelBossAction()
        {
            if (patternRoutine != null)
            {
                StopCoroutine(patternRoutine);
                patternRoutine = null;
            }

            BossGraphRuntimeState.Clear(bossGraph);
            graphRunner.Reset();
            graphContext?.ResetBodyRootLocalOffset();
            graphContext = null;
        }

        protected override void OnBossDied()
        {
            CancelBossAction();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            BossGraphRuntimeState.Clear(bossGraph);
            graphRunner.Reset();
        }

        private IEnumerator RunGraphPatternLoop()
        {
            graphRunner.Reset();
            graphContext = CreateGraphContext();
            yield return graphRunner.RunLoop(bossGraph, graphContext);
            graphContext = null;
            patternRoutine = null;
        }

        private BossActionContext CreateGraphContext()
        {
            return new BossActionContext(
                this,
                Stop,
                () => IsExecutionPaused);
        }
    }
}
