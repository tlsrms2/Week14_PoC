namespace Week14.Enemy
{
    /// <summary>적 카테고리</summary>
    public enum EnemyCategory
    {
        Normal = 0,
        Boss = 1
    }

    /// <summary>순찰 모드</summary>
    public enum PatrolMode
    {
        /// <summary>한 자리에서 한 방향을 보며 가만히 있음</summary>
        Stationary = 0,
        /// <summary>웨이포인트를 따라 배회</summary>
        Patrol = 1
    }
}
