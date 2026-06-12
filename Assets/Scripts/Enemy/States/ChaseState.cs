using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 추격 상태.
    /// 플레이어가 사정거리 밖일 때 접근한다.
    /// 사정거리 진입 시 Engage/Flank으로, 놓치면 Idle/Patrol로 전환.
    /// </summary>
    public sealed class ChaseState : IEnemyState
    {
        private float loseTargetTimer;
        private const float LoseTargetDelay = 2f;

        public void Enter(EnemyAI enemy)
        {
            enemy.CancelAttack();
            loseTargetTimer = 0f;
        }

        public void Tick(EnemyAI enemy)
        {
            // 감지 범위 이탈 → 타이머 후 복귀
            if (!enemy.IsPlayerDetected())
            {
                enemy.Stop();
                loseTargetTimer += Time.deltaTime;
                if (loseTargetTimer >= LoseTargetDelay)
                {
                    ReturnToDefault(enemy);
                }
                return;
            }

            loseTargetTimer = 0f;

            // 사정거리 진입
            if (enemy.IsPlayerInAttackRange())
            {
                if (enemy.CanSeePlayer())
                    enemy.StateMachine.ChangeState(enemy.EngageState, enemy);
                else
                    enemy.StateMachine.ChangeState(enemy.FlankState, enemy);
                return;
            }

            // 접근
            if (enemy.Player != null)
                enemy.MoveToward(enemy.Player.position);
        }

        public void Exit(EnemyAI enemy)
        {
            enemy.Stop();
        }

        private static void ReturnToDefault(EnemyAI enemy)
        {
            if (enemy.Data.PatrolMode == PatrolMode.Patrol && enemy.PatrolWaypoints.Count > 0)
                enemy.StateMachine.ChangeState(enemy.PatrolState, enemy);
            else
                enemy.StateMachine.ChangeState(enemy.IdleState, enemy);
        }
    }
}
