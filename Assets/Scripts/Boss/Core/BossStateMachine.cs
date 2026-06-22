namespace Week14.Enemy
{
    internal enum BossStateId
    {
        None,
        WaitingForPlayer,
        Combat,
        HpEmpty,
        PhaseTransition,
        ExecutionPaused,
        ExecutionLocked,
        Dead
    }

    internal sealed class BossStateMachine
    {
        private readonly BossAI boss;
        private readonly BossState waitingForPlayerState;
        private readonly BossState combatState;
        private readonly BossState hpEmptyState;
        private readonly BossState phaseTransitionState;
        private readonly BossState executionPausedState;
        private readonly BossState executionLockedState;
        private readonly BossState deadState;
        private BossState currentState;

        public BossStateMachine(BossAI boss)
        {
            this.boss = boss;
            waitingForPlayerState = new BossWaitingForPlayerState(boss);
            combatState = new BossCombatState(boss);
            hpEmptyState = new BossHpEmptyState(boss);
            phaseTransitionState = new BossPhaseTransitionState(boss);
            executionPausedState = new BossExecutionPausedState(boss);
            executionLockedState = new BossExecutionLockedState(boss);
            deadState = new BossDeadState(boss);
        }

        public void Tick()
        {
            boss.TickVisualStateForState();
            ChangeState(ResolveState());
            currentState.Tick();
            ChangeState(ResolveState());
        }

        private BossStateId ResolveState()
        {
            if (boss.IsDeadForState)
            {
                return BossStateId.Dead;
            }

            if (boss.IsExecutionLockedForState)
            {
                return BossStateId.ExecutionLocked;
            }

            if (BossAI.IsExecutionPausedForState)
            {
                return BossStateId.ExecutionPaused;
            }

            if (boss.IsPhaseTransitionWaitingForState)
            {
                return BossStateId.PhaseTransition;
            }

            if (boss.IsHpEmpty)
            {
                return BossStateId.HpEmpty;
            }

            return boss.IsCombatStartedForState
                ? BossStateId.Combat
                : BossStateId.WaitingForPlayer;
        }

        private void ChangeState(BossStateId stateId)
        {
            if (currentState != null && currentState.Id == stateId)
            {
                return;
            }

            currentState?.Exit();
            currentState = GetState(stateId);
            currentState.Enter();
        }

        private BossState GetState(BossStateId stateId)
        {
            return stateId switch
            {
                BossStateId.WaitingForPlayer => waitingForPlayerState,
                BossStateId.Combat => combatState,
                BossStateId.HpEmpty => hpEmptyState,
                BossStateId.PhaseTransition => phaseTransitionState,
                BossStateId.ExecutionPaused => executionPausedState,
                BossStateId.ExecutionLocked => executionLockedState,
                BossStateId.Dead => deadState,
                _ => waitingForPlayerState
            };
        }
    }
}
