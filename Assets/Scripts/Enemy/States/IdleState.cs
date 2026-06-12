using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// Stationary 적의 기본 상태.
    /// 한 방향을 바라보며 정지. 플레이어 감지 시 전투 상태로 전환.
    /// </summary>
    public sealed class IdleState : IEnemyState
    {
        public void Enter(EnemyAI enemy)
        {
            enemy.Stop();
            enemy.CancelAttack();
        }

        public void Tick(EnemyAI enemy)
        {
            if (!enemy.IsPlayerDetected()) return;

            // 감지됨 → 상태 결정
            if (!enemy.IsPlayerInAttackRange())
            {
                enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
                return;
            }

            if (enemy.CanSeePlayer())
                enemy.StateMachine.ChangeState(enemy.EngageState, enemy);
            else
                enemy.StateMachine.ChangeState(enemy.FlankState, enemy);
        }

        public void Exit(EnemyAI enemy) { }
    }
}
