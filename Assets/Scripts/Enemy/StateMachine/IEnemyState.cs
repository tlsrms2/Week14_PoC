namespace Week14.Enemy
{
    /// <summary>
    /// 적 AI 상태 인터페이스.
    /// 각 상태 클래스가 이를 구현하여 FSM에서 교체된다.
    /// </summary>
    public interface IEnemyState
    {
        void Enter(EnemyAI enemy);
        void Tick(EnemyAI enemy);
        void Exit(EnemyAI enemy);
    }
}
