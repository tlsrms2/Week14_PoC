using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 교전 상태.
    /// 사정거리 안이고 시야가 확보되면 공격 패턴을 실행한다.
    /// 공격 패턴이 끝난 뒤 대기 시간 동안 공격 타이밍 아웃라인을 표시한다.
    /// </summary>
    public sealed class EngageState : IEnemyState
    {
        private float cooldownTimer;
        private float cooldownDuration;

        public void Enter(EnemyAI enemy)
        {
            enemy.Stop();
            cooldownTimer = 0f;
            cooldownDuration = 0f;
            enemy.HideAttackTiming();
        }

        public void Tick(EnemyAI enemy)
        {
            if (!enemy.IsPlayerInAttackRange())
            {
                enemy.HideAttackTiming();
                enemy.CancelAttack();
                enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
                return;
            }

            if (!enemy.CanSeePlayer())
            {
                enemy.HideAttackTiming();
                enemy.CancelAttack();
                enemy.StateMachine.ChangeState(enemy.FlankState, enemy);
                return;
            }

            enemy.Stop();

            if (enemy.IsAttacking)
            {
                enemy.HideAttackTiming();
                return;
            }

            if (cooldownTimer > 0f)
            {
                cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
                enemy.ShowAttackTiming(cooldownTimer, cooldownDuration);
                return;
            }

            enemy.HideAttackTiming();

            AttackTimeline timeline = enemy.SelectNextTimeline();
            if (timeline == null)
            {
                return;
            }

            enemy.StartAttack(timeline);
            cooldownDuration = timeline.CooldownAfter;
            cooldownTimer = cooldownDuration;
        }

        public void Exit(EnemyAI enemy)
        {
            enemy.HideAttackTiming();
            enemy.CancelAttack();
            enemy.Stop();
        }
    }
}
