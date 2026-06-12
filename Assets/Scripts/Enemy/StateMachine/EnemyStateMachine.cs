namespace Week14.Enemy
{
    /// <summary>
    /// 단순 유한 상태 기계. 현재 상태의 Enter/Tick/Exit 생명주기를 관리한다.
    /// </summary>
    public sealed class EnemyStateMachine
    {
        public IEnemyState CurrentState { get; private set; }

        public void Initialize(IEnemyState initialState, EnemyAI enemy)
        {
            CurrentState = initialState;
            CurrentState?.Enter(enemy);
        }

        public void ChangeState(IEnemyState newState, EnemyAI enemy)
        {
            if (newState == null || newState == CurrentState) return;

            CurrentState?.Exit(enemy);
            CurrentState = newState;
            CurrentState.Enter(enemy);
        }

        public void Tick(EnemyAI enemy)
        {
            CurrentState?.Tick(enemy);
        }
    }
}
