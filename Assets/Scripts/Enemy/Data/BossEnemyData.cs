using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Enemy/Boss Enemy Data", fileName = "BossEnemyData")]
    public sealed class BossEnemyData : EnemyData
    {
        [Header("플레이어 공격 대응")]
        [Tooltip("보스가 플레이어 공격을 패링할 수 있는지 여부입니다.")]
        [SerializeField] private bool canParryPlayerAttacks = true;
        [Tooltip("플레이어 공격을 패링할 기본 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float playerAttackParryChance = 0.25f;
        [Tooltip("플레이어 공격이 이어질 때마다 추가되는 패링 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float playerAttackParryChanceIncrease = 0.1f;
        [Tooltip("누적 증가를 포함한 패링 확률 상한입니다.")]
        [SerializeField, Range(0f, 1f)] private float maxPlayerAttackParryChance = 0.85f;
        [Tooltip("이 시간 동안 플레이어 공격이 없으면 패링 확률 누적을 초기화합니다.")]
        [SerializeField, Min(0f)] private float playerAttackPressureResetSeconds = 1.5f;
        [Tooltip("보스가 플레이어 공격을 패링할 수 있는 정면 각도입니다.")]
        [SerializeField, Range(1f, 360f)] private float playerAttackParryAngleDegrees = 130f;
        [Tooltip("보스가 플레이어 공격을 한 번 패링한 뒤 다시 패링할 수 있을 때까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float playerAttackParryCooldown = 0.35f;
        [Tooltip("보스가 플레이어 공격을 패링했을 때 플레이어에게 추가되는 열기입니다. 이 열기로는 과열되지 않습니다.")]
        [SerializeField, Min(0f)] private float playerHeatOnBossParry = 8f;

        [Tooltip("보스가 패링하지 못한 플레이어 공격을 방어할 수 있는지 여부입니다.")]
        [SerializeField] private bool canDefendPlayerAttacks = true;
        [Tooltip("플레이어 공격을 방어할 고정 확률입니다.")]
        [SerializeField, Range(0f, 1f)] private float playerAttackDefenseChance = 0.25f;
        [Tooltip("보스가 플레이어 공격을 방어할 수 있는 정면 각도입니다.")]
        [SerializeField, Range(1f, 360f)] private float playerAttackDefenseAngleDegrees = 220f;
        [Tooltip("보스가 플레이어 공격을 한 번 방어한 뒤 다시 방어할 수 있을 때까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float playerAttackDefenseCooldown = 0.12f;
        [Tooltip("보스가 플레이어 공격을 방어했을 때 보스에게 추가되는 열기입니다.")]
        [FormerlySerializedAs("playerHeatOnBossDefense")]
        [SerializeField, Min(0f)] private float bossHeatOnDefense = 4f;
        [Tooltip("보스가 플레이어 공격을 패링했을 때 플레이어 열기 자연 감소를 멈추는 시간입니다.")]
        [SerializeField, Min(0f)] private float playerHeatCoolingSuppressSeconds = 0.35f;

        public bool CanParryPlayerAttacks => canParryPlayerAttacks;
        public float PlayerAttackParryChance => Mathf.Clamp01(playerAttackParryChance);
        public float PlayerAttackParryChanceIncrease => Mathf.Clamp01(playerAttackParryChanceIncrease);
        public float MaxPlayerAttackParryChance => Mathf.Clamp01(maxPlayerAttackParryChance);
        public float PlayerAttackPressureResetSeconds => playerAttackPressureResetSeconds;
        public float PlayerAttackParryAngleDegrees => playerAttackParryAngleDegrees;
        public float PlayerAttackParryCooldown => playerAttackParryCooldown;
        public float PlayerHeatOnBossParry => playerHeatOnBossParry;

        public bool CanDefendPlayerAttacks => canDefendPlayerAttacks;
        public float PlayerAttackDefenseChance => Mathf.Clamp01(playerAttackDefenseChance);
        public float PlayerAttackDefenseAngleDegrees => playerAttackDefenseAngleDegrees;
        public float PlayerAttackDefenseCooldown => playerAttackDefenseCooldown;
        public float BossHeatOnDefense => bossHeatOnDefense;
        public float PlayerHeatCoolingSuppressSeconds => playerHeatCoolingSuppressSeconds;
    }
}
