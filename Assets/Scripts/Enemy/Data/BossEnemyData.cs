using UnityEngine;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Enemy/Boss Enemy Data", fileName = "BossEnemyData")]
    public sealed class BossEnemyData : EnemyData
    {
        [Header("Player Attack Response")]
        [Tooltip("보스가 플레이어의 왼쪽 권총 공격을 패링할 수 있는지 여부입니다.")]
        [SerializeField] private bool canParryPlayerAttacks = true;
        [Tooltip("보스가 플레이어 공격을 패링할 기본 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float playerAttackParryChance = 0.25f;
        [Tooltip("플레이어가 연속 공격할수록 패링 확률에 더해지는 값입니다.")]
        [SerializeField, Range(0f, 1f)] private float playerAttackParryChanceIncrease = 0.1f;
        [Tooltip("연속 공격으로 증가할 수 있는 패링 확률의 최대값입니다.")]
        [SerializeField, Range(0f, 1f)] private float maxPlayerAttackParryChance = 0.85f;
        [Tooltip("이 시간 동안 플레이어 공격이 없으면 누적 패링 압박을 초기화합니다.")]
        [SerializeField, Min(0f)] private float playerAttackPressureResetSeconds = 1.5f;
        [Tooltip("보스 정면 기준으로 플레이어 공격을 패링할 수 있는 각도입니다.")]
        [SerializeField, Range(1f, 360f)] private float playerAttackParryAngleDegrees = 130f;
        [Tooltip("보스가 플레이어 공격을 한 번 패링한 뒤 다시 패링할 수 있을 때까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float playerAttackParryCooldown = 0.35f;
        [Tooltip("보스가 플레이어 공격을 패링했을 때 플레이어 탄환을 감소시키는 양입니다.")]
        [SerializeField, Min(0)] private int playerBulletDamageOnBossParry = 8;

        public bool CanParryPlayerAttacks => canParryPlayerAttacks;
        public float PlayerAttackParryChance => Mathf.Clamp01(playerAttackParryChance);
        public float PlayerAttackParryChanceIncrease => Mathf.Clamp01(playerAttackParryChanceIncrease);
        public float MaxPlayerAttackParryChance => Mathf.Clamp01(maxPlayerAttackParryChance);
        public float PlayerAttackPressureResetSeconds => playerAttackPressureResetSeconds;
        public float PlayerAttackParryAngleDegrees => playerAttackParryAngleDegrees;
        public float PlayerAttackParryCooldown => playerAttackParryCooldown;
        public int PlayerBulletDamageOnBossParry => playerBulletDamageOnBossParry;
    }
}
