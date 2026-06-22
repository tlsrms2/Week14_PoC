using UnityEngine;

namespace Week14.Enemy
{
    internal abstract class BossState
    {
        protected BossState(BossAI boss)
        {
            Boss = boss;
        }

        internal abstract BossStateId Id { get; }
        protected BossAI Boss { get; }

        internal virtual void Enter() { }
        internal abstract void Tick();
        internal virtual void Exit() { }
    }

    internal sealed class BossWaitingForPlayerState : BossState
    {
        public BossWaitingForPlayerState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.WaitingForPlayer;

        internal override void Tick()
        {
            Boss.ResolvePlayerForState();
            Boss.TryStartCombatForState();
            Boss.TickActiveBehaviorForState();
        }
    }

    internal sealed class BossCombatState : BossState
    {
        public BossCombatState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.Combat;

        internal override void Tick()
        {
            Boss.ResolvePlayerForState();
            Boss.TickActiveBehaviorForState();
        }
    }

    internal sealed class BossHpEmptyState : BossState
    {
        private float hpEmptyEndsAt;

        public BossHpEmptyState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.HpEmpty;

        internal override void Enter()
        {
            hpEmptyEndsAt = Time.time + Boss.HpEmptyExecutionSeconds;
            Boss.BeginHpEmptyForState();
        }

        internal override void Tick()
        {
            if (Time.time >= hpEmptyEndsAt)
            {
                Boss.RecoverFromHpEmptyForState();
                return;
            }

            float remainingSeconds = hpEmptyEndsAt - Time.time;
            float remainingRatio = Boss.HpEmptyExecutionSeconds > 0f
                ? Mathf.Clamp01(remainingSeconds / Boss.HpEmptyExecutionSeconds)
                : 0f;
            Boss.UpdateHpEmptyWindowForState(remainingRatio);
            Boss.Stop();
        }
    }

    internal sealed class BossPhaseTransitionState : BossState
    {
        public BossPhaseTransitionState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.PhaseTransition;

        internal override void Tick()
        {
            Boss.TickPhaseTransitionWaitForState();
        }
    }

    internal sealed class BossExecutionPausedState : BossState
    {
        public BossExecutionPausedState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.ExecutionPaused;

        internal override void Tick()
        {
            Boss.Stop();
        }
    }

    internal sealed class BossExecutionLockedState : BossState
    {
        public BossExecutionLockedState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.ExecutionLocked;

        internal override void Tick()
        {
            Boss.Stop();
        }
    }

    internal sealed class BossDeadState : BossState
    {
        public BossDeadState(BossAI boss) : base(boss) { }

        internal override BossStateId Id => BossStateId.Dead;

        internal override void Tick()
        {
            Boss.Stop();
        }
    }
}
