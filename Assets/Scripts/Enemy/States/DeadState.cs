namespace Week14.Enemy
{
    /// <summary>
    /// 사망 상태.
    /// 이동/공격 정지. Encounter 시스템의 Died 이벤트로 처리됨.
    /// </summary>
    public sealed class DeadState : IEnemyState
    {
        public void Enter(EnemyAI enemy)
        {
            enemy.CancelAttack();
            enemy.Stop();
        }

        public void Tick(EnemyAI enemy) { }

        public void Exit(EnemyAI enemy) { }
    }
}
