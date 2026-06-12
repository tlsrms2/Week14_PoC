using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// Patrol 적의 기본 상태.
    /// 웨이포인트를 순차적으로 순회하며, 플레이어 감지 시 전투 상태로 전환.
    /// </summary>
    public sealed class PatrolState : IEnemyState
    {
        private int currentWaypointIndex;
        private float waitTimer;
        private bool isWaiting;

        public void Enter(EnemyAI enemy)
        {
            enemy.CancelAttack();
            isWaiting = false;
            waitTimer = 0f;

            // 가장 가까운 웨이포인트부터 시작
            if (enemy.PatrolWaypoints.Count > 0)
            {
                float bestDist = float.MaxValue;
                for (int i = 0; i < enemy.PatrolWaypoints.Count; i++)
                {
                    float d = Vector2.Distance(enemy.transform.position, enemy.PatrolWaypoints[i]);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        currentWaypointIndex = i;
                    }
                }
            }
        }

        public void Tick(EnemyAI enemy)
        {
            // 플레이어 감지 체크
            if (enemy.IsPlayerDetected())
            {
                if (!enemy.IsPlayerInAttackRange())
                {
                    enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
                    return;
                }

                if (enemy.CanSeePlayer())
                    enemy.StateMachine.ChangeState(enemy.EngageState, enemy);
                else
                    enemy.StateMachine.ChangeState(enemy.FlankState, enemy);
                return;
            }

            // 웨이포인트 없으면 정지
            if (enemy.PatrolWaypoints.Count == 0)
            {
                enemy.Stop();
                return;
            }

            // 대기 중
            if (isWaiting)
            {
                enemy.Stop();
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    isWaiting = false;
                    currentWaypointIndex = (currentWaypointIndex + 1) % enemy.PatrolWaypoints.Count;
                }
                return;
            }

            // 웨이포인트로 이동
            Vector3 target = enemy.PatrolWaypoints[currentWaypointIndex];
            float dist = Vector2.Distance(enemy.transform.position, target);

            if (dist < 0.3f)
            {
                // 도착 → 대기
                enemy.Stop();
                isWaiting = true;
                waitTimer = enemy.Data.PatrolWaitTime;
                return;
            }

            enemy.MoveToward(target);
        }

        public void Exit(EnemyAI enemy)
        {
            enemy.Stop();
        }
    }
}
