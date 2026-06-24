using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Save;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI : BossAI
    {
        [Header("Boss Graph")]
        [SerializeField, Tooltip("할당하면 그래프 데이터로 패턴을 실행합니다.")]
        private BossGraphAsset bossGraph;
        [SerializeField, Tooltip("Boss Graph 액션이 이름으로 참조할 투사체 설정 목록입니다.")]
        private List<BossGraphProjectileEntry> graphProjectiles = new()
        {
            new BossGraphProjectileEntry()
        };

        private Coroutine patternRoutine;
        private BossActionContext graphContext;
        private readonly BossGraphRunner graphRunner = new();

        protected override BossGraphAsset GraphAsset => bossGraph;
        protected override bool RotatesBodyToPlayer => false;

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

        protected override void OnBossDied()
        {
            GameSaveManager.ClearBoss("1");
            GameSaveManager.UnlockBoss("2");
        }

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

            BossGraphRuntimeState.Clear(bossGraph);
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
