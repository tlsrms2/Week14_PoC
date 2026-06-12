using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 측면 이동 상태.
    /// 사정거리 안이지만 시야 미확보 → 거리를 유지하며 좌/우로 이동해 시야 확보를 시도.
    /// 장애물 감지 시 반대 방향으로 전환.
    /// </summary>
    public sealed class FlankState : IEnemyState
    {
        private int flankDirection; // 1 = 시계, -1 = 반시계
        private float directionChangeTimer;
        private const float DirectionCheckInterval = 0.5f;
        private const float ObstacleCheckDistance = 1.2f;

        public void Enter(EnemyAI enemy)
        {
            enemy.CancelAttack();
            // 랜덤 방향 선택
            flankDirection = Random.value > 0.5f ? 1 : -1;
            directionChangeTimer = DirectionCheckInterval;
        }

        public void Tick(EnemyAI enemy)
        {
            if (enemy.Player == null) return;

            // 사정거리 이탈 → 추격
            if (!enemy.IsPlayerInAttackRange())
            {
                enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
                return;
            }

            // 시야 확보 → 교전
            if (enemy.CanSeePlayer())
            {
                enemy.StateMachine.ChangeState(enemy.EngageState, enemy);
                return;
            }

            // 플레이어와 수직 방향으로 이동 (거리 유지)
            Vector2 toPlayer = ((Vector2)enemy.Player.position - (Vector2)enemy.transform.position);
            Vector2 perpendicular = new Vector2(-toPlayer.y, toPlayer.x).normalized * flankDirection;

            // 장애물 감지 → 반대 방향
            directionChangeTimer -= Time.deltaTime;
            if (directionChangeTimer <= 0f)
            {
                directionChangeTimer = DirectionCheckInterval;

                Vector2 checkDir = perpendicular;
                RaycastHit2D hit = Physics2D.Raycast(
                    enemy.transform.position,
                    checkDir,
                    ObstacleCheckDistance,
                    enemy.ObstacleMask);

                if (hit.collider != null)
                {
                    flankDirection *= -1;
                    perpendicular = new Vector2(-toPlayer.y, toPlayer.x).normalized * flankDirection;
                }
            }

            enemy.MoveToward((Vector2)enemy.transform.position + perpendicular);
        }

        public void Exit(EnemyAI enemy)
        {
            enemy.Stop();
        }
    }
}
