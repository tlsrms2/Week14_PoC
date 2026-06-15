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
        private const float MagazinePreloadSeconds = 0.15f;
        private const float DirectionChangeInterval = 1.2f;
        private const float InnerRangeRatio = 0.45f;
        private const float OuterRangeRatio = 0.85f;

        private float cooldownTimer;
        private float cooldownDuration;
        private float directionChangeTimer;
        private int strafeDirection;

        public void Enter(EnemyAI enemy)
        {
            cooldownDuration = enemy.Data != null ? enemy.Data.InitialAttackDelaySeconds : 0f;
            cooldownTimer = cooldownDuration;
            strafeDirection = Random.value > 0.5f ? 1 : -1;
            directionChangeTimer = DirectionChangeInterval;
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

            MoveWhileEngaged(enemy);

            if (enemy.IsAttacking)
            {
                enemy.ShowCurrentAttackBullets();
                return;
            }

            if (cooldownTimer > 0f)
            {
                cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);
                int nextAttackCount = enemy.GetNextTimelineAttackCount();
                int loadedAttackCount = cooldownTimer <= MagazinePreloadSeconds ? nextAttackCount : 0;
                enemy.ShowAttackTiming(cooldownTimer, cooldownDuration, loadedAttackCount, nextAttackCount);
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

        private void MoveWhileEngaged(EnemyAI enemy)
        {
            if (enemy.Player == null || enemy.Data == null)
            {
                return;
            }

            directionChangeTimer -= Time.deltaTime;
            if (directionChangeTimer <= 0f)
            {
                directionChangeTimer = DirectionChangeInterval;
                strafeDirection *= -1;
            }

            Vector2 toPlayer = (Vector2)enemy.Player.position - (Vector2)enemy.transform.position;
            if (toPlayer.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float distance = toPlayer.magnitude;
            Vector2 toPlayerDirection = toPlayer / distance;
            Vector2 strafe = new Vector2(-toPlayerDirection.y, toPlayerDirection.x) * strafeDirection;
            Vector2 distanceCorrection = Vector2.zero;
            float attackRange = Mathf.Max(0.01f, enemy.Data.AttackRange);

            if (distance < attackRange * InnerRangeRatio)
            {
                distanceCorrection = -toPlayerDirection;
            }
            else if (distance > attackRange * OuterRangeRatio)
            {
                distanceCorrection = toPlayerDirection * 0.6f;
            }

            Vector2 moveDirection = (strafe + distanceCorrection).normalized;
            if (moveDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            enemy.MoveToward((Vector2)enemy.transform.position + moveDirection);
        }

        public void Exit(EnemyAI enemy)
        {
            enemy.HideAttackTiming();
            enemy.CancelAttack();
            enemy.Stop();
        }
    }
}
