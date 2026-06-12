using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 교전 상태.
    /// 사정거리 안 + 시야 확보 → 정지 후 AttackTimeline 실행.
    /// 시야 차단 시 Flank, 사정거리 이탈 시 Chase로 전환.
    /// </summary>
    public sealed class EngageState : IEnemyState
    {
        private float cooldownTimer;

        public void Enter(EnemyAI enemy)
        {
            enemy.Stop();
            cooldownTimer = 0f;
        }

        public void Tick(EnemyAI enemy)
        {
            // 사정거리 이탈
            if (!enemy.IsPlayerInAttackRange())
            {
                enemy.CancelAttack();
                enemy.StateMachine.ChangeState(enemy.ChaseState, enemy);
                return;
            }

            // 시야 차단
            if (!enemy.CanSeePlayer())
            {
                enemy.CancelAttack();
                enemy.StateMachine.ChangeState(enemy.FlankState, enemy);
                return;
            }

            enemy.Stop();

            // 공격 중이면 대기
            if (enemy.IsAttacking) return;

            // 쿨다운 진행
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                return;
            }

            // 타임라인 선택 및 실행
            AttackTimeline timeline = enemy.SelectNextTimeline();
            if (timeline == null) return;

            enemy.StartAttack(timeline);
            cooldownTimer = timeline.CooldownAfter;
        }

        public void Exit(EnemyAI enemy)
        {
            enemy.CancelAttack();
            enemy.Stop();
        }
    }
}
