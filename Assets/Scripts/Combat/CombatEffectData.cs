using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Combat
{
    [CreateAssetMenu(menuName = "Week14/Combat/Combat Effect Data", fileName = "CombatEffectData")]
    public sealed class CombatEffectData : ScriptableObject
    {
        [Header("Player Projectile")]
        [Tooltip("플레이어 일반 공격 효과와 총구 화염에 사용하는 기본 색입니다.")]
        [SerializeField] private Color attackEffectColor = new(1f, 0.35f, 0.12f, 0.55f);
        [Tooltip("패링탄과 패링 성공 이펙트에 사용하는 기본 색입니다.")]
        [SerializeField] private Color parryEffectColor = new(0.2f, 0.65f, 1f, 0.45f);
        [Tooltip("처형 중 발사하는 탄의 색입니다.")]
        [SerializeField] private Color executionShotColor = Color.white;
        [Tooltip("플레이어 탄 궤적이 남는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float playerProjectileTrailSeconds = 0.08f;
        [Tooltip("플레이어 탄 궤적 두께 배율입니다.")]
        [SerializeField, Min(0.1f)] private float playerProjectileTrailWidthMultiplier = 2.8f;

        [Header("Enemy Projectile")]
        [Tooltip("적 탄과 적 총구 화염에 사용하는 기본 색입니다.")]
        [SerializeField] private Color enemyProjectileColor = new(1f, 0.95f, 0.25f, 1f);
        [Tooltip("적 탄 궤적이 남는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float enemyProjectileTrailSeconds = 0.1f;
        [Tooltip("적 탄 궤적 두께 배율입니다.")]
        [SerializeField, Min(0.1f)] private float enemyProjectileTrailWidthMultiplier = 3f;

        [Header("Parry")]
        [Tooltip("패링 성공 시 생성되는 스파크 색입니다.")]
        [SerializeField] private Color parrySparkColor = new(1f, 0.88f, 0.35f, 1f);
        [Tooltip("패링 또는 방어 성공 시 링 이펙트 색입니다.")]
        [SerializeField] private Color parryRingColor = new(0.45f, 0.9f, 1f, 0.75f);
        [Tooltip("패링 성공 링 주변 글리터 색입니다.")]
        [SerializeField] private Color parryRingGlitterColor = new(1f, 0.96f, 0.68f, 1f);
        [Tooltip("패링 스파크 지속 시간입니다.")]
        [SerializeField, Min(0f)] private float parrySparkSeconds = 0.22f;
        [Tooltip("패링 링 지속 시간입니다.")]
        [SerializeField, Min(0f)] private float parryRingSeconds = 0.32f;
        [Tooltip("패링 링 글리터 지속 시간입니다.")]
        [SerializeField, Min(0f)] private float parryRingGlitterSeconds = 0.2f;
        [Tooltip("패링 성공 시 생성되는 스파크 수입니다.")]
        [SerializeField, Min(0)] private int parrySparkCount = 34;
        [Tooltip("패링 성공 링 주변 글리터 수입니다.")]
        [SerializeField, Min(0)] private int parryRingGlitterCount = 18;
        [Tooltip("패링 성공 시 생성되는 화염 입자 수입니다.")]
        [SerializeField, Min(0)] private int parryFlameCount = 20;
        [Tooltip("패링 성공 이펙트 전체 크기 배율입니다.")]
        [SerializeField, Min(0f)] private float parryEffectScale = 1f;
        [Tooltip("방어 성공 시 생성되는 스파크 수입니다.")]
        [SerializeField, FormerlySerializedAs("failedParrySparkCount"), Min(0)] private int defenseSparkCount = 18;
        [Tooltip("방어 성공 시 생성되는 스파크 색입니다.")]
        [SerializeField] private Color defenseSparkColor = new(0.55f, 0.75f, 1f, 1f);
        [Tooltip("방어 성공 시 링 이펙트 색입니다.")]
        [SerializeField] private Color defenseRingColor = new(0.35f, 0.65f, 1f, 0.75f);
        [Tooltip("방어 성공 이펙트 전체 크기 배율입니다.")]
        [SerializeField, FormerlySerializedAs("failedParryEffectScale"), Min(0f)] private float defenseEffectScale = 1f;

        [Header("Boss Response")]
        [Tooltip("보스가 플레이어 탄을 패링해서 되돌릴 때 쓰는 스파크 색입니다.")]
        [SerializeField] private Color bossParrySparkColor = new(1f, 0.78f, 0.18f, 1f);
        [Tooltip("보스가 플레이어 탄을 패링해서 되돌릴 때 쓰는 링 색입니다.")]
        [SerializeField] private Color bossParryRingColor = new(1f, 0.42f, 0.08f, 0.85f);
        [Tooltip("보스가 플레이어 탄을 패링해서 되돌릴 때 쓰는 글리터 색입니다.")]
        [SerializeField] private Color bossParryGlitterColor = new(1f, 0.95f, 0.52f, 1f);
        [Tooltip("보스가 플레이어 탄을 방어할 때 쓰는 스파크 색입니다.")]
        [SerializeField] private Color bossDefenseSparkColor = new(0.52f, 0.92f, 1f, 1f);
        [Tooltip("보스가 플레이어 탄을 방어할 때 쓰는 링 색입니다.")]
        [SerializeField] private Color bossDefenseRingColor = new(0.28f, 0.72f, 1f, 0.9f);

        [Header("Hit Impact")]
        [Tooltip("플레이어 공격이 적에게 맞았을 때 전방 스파크 색입니다.")]
        [SerializeField] private Color attackImpactSparkColor = new(1f, 0.92f, 0.62f, 1f);
        [Tooltip("플레이어 공격이 적에게 맞았을 때 반대 방향 스파크 색입니다.")]
        [SerializeField] private Color attackImpactBackSparkColor = new(1f, 0.52f, 0.12f, 1f);
        [Tooltip("플레이어 공격이 적에게 맞았을 때 화염 입자 색입니다.")]
        [SerializeField] private Color attackImpactFlameColor = new(1f, 0.48f, 0.08f, 1f);
        [Tooltip("플레이어 공격이 적에게 맞았을 때 충격 링 색입니다.")]
        [SerializeField] private Color attackImpactRingColor = new(1f, 0.92f, 0.62f, 0.72f);
        [Tooltip("플레이어 공격이 적에게 맞았을 때 생성되는 전방 스파크 수입니다.")]
        [SerializeField, Min(0)] private int attackImpactSparkCount = 14;
        [Tooltip("플레이어 공격이 적에게 맞았을 때 반대 방향으로 튀는 스파크 수입니다.")]
        [SerializeField, Min(0)] private int attackImpactBackSparkCount = 6;
        [Tooltip("플레이어 공격이 적에게 맞았을 때 생성되는 화염 입자 수입니다.")]
        [SerializeField, Min(0)] private int attackImpactFlameCount = 8;
        [Tooltip("플레이어 공격 피격 이펙트 전체 크기 배율입니다.")]
        [SerializeField, Min(0f)] private float attackImpactEffectScale = 0.65f;
        [Tooltip("플레이어가 적 공격에 맞았을 때 생성되는 전방 스파크 수입니다.")]
        [SerializeField, Min(0)] private int playerHitSparkCount = 12;
        [Tooltip("플레이어가 적 공격에 맞았을 때 반대 방향으로 튀는 스파크 수입니다.")]
        [SerializeField, Min(0)] private int playerHitBackSparkCount = 5;
        [Tooltip("플레이어가 적 공격에 맞았을 때 생성되는 화염 입자 수입니다.")]
        [SerializeField, Min(0)] private int playerHitFlameCount = 6;
        [Tooltip("플레이어 피격 이펙트 전체 크기 배율입니다.")]
        [SerializeField, Min(0f)] private float playerHitEffectScale = 0.55f;

        [Header("Body Color")]
        [Tooltip("플레이어가 과열 상태일 때 BodyRoot에 적용하는 색입니다.")]
        [SerializeField] private Color playerBodyOverheatedColor = new(1f, 0.2f, 0.12f, 1f);
        [Tooltip("플레이어가 피격됐을 때 BodyRoot에 잠깐 적용하는 색입니다.")]
        [SerializeField] private Color playerBodyHitColor = new(1f, 0.85f, 0.25f, 1f);
        [Tooltip("적이 피격됐을 때 BodyRoot에 잠깐 적용하는 색입니다.")]
        [SerializeField] private Color enemyBodyHitColor = new(1f, 0.35f, 0.25f, 1f);
        [Tooltip("피격 색이 지속되는 시간입니다.")]
        [SerializeField, Min(0f)] private float bodyHitColorSeconds = 0.08f;

        [Header("Heat UI")]
        [Tooltip("패링으로 플레이어 열기가 증가했을 때 열기 UI 외곽선 색입니다.")]
        [SerializeField] private Color heatParryOutlineColor = Color.white;
        [Tooltip("방어로 플레이어 열기가 증가했을 때 열기 UI 외곽선 색입니다.")]
        [SerializeField] private Color heatDefenseOutlineColor = new(0.55f, 0.55f, 0.55f, 1f);
        [Tooltip("피격으로 플레이어 열기가 증가했을 때 열기 UI 외곽선 색입니다.")]
        [SerializeField] private Color heatHitOutlineColor = Color.yellow;
        [Tooltip("플레이어 과열 중 열기 UI가 깜빡일 때 사용하는 외곽선 색입니다.")]
        [SerializeField] private Color heatOverheatBlinkOutlineColor = Color.yellow;
        [Tooltip("플레이어 과열 중 열기 게이지 바가 깜빡일 때 쓰는 색입니다.")]
        [SerializeField] private Color heatOverheatBlinkFillColor = Color.yellow;
        [Tooltip("플레이어 과열 중 열기 UI 깜빡임 속도입니다.")]
        [SerializeField, Min(0.1f)] private float heatOverheatBlinkFrequency = 5f;
        [Tooltip("플레이어 과열 중 열기 UI가 가장 어두울 때의 알파 배율입니다.")]
        [SerializeField, Range(0f, 1f)] private float heatOverheatBlinkMinAlpha = 0.25f;

        [Header("Execution")]
        [Tooltip("처형 타격 순간 사용하는 이펙트 색입니다.")]
        [SerializeField] private Color executionImpactColor = new(0.9f, 0.02f, 0.04f, 1f);
        [Tooltip("처형 흡수 연출에 사용하는 이펙트 색입니다.")]
        [SerializeField] private Color executionAbsorbColor = new(0.35f, 0.85f, 1f, 1f);
        [Tooltip("처형 타격 입자가 지속되는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionImpactParticleSeconds = 0.55f;
        [Tooltip("처형 타격 순간 생성되는 입자 수입니다.")]
        [SerializeField, Min(0)] private int executionImpactParticleCount = 28;
        [Tooltip("처형 흡수 연출에 생성되는 입자 수입니다.")]
        [SerializeField, Min(0)] private int executionAbsorbParticleCount = 24;

        public Color AttackEffectColor => attackEffectColor;
        public Color ParryEffectColor => parryEffectColor;
        public Color ExecutionShotColor => executionShotColor;
        public float PlayerProjectileTrailSeconds => playerProjectileTrailSeconds;
        public float PlayerProjectileTrailWidthMultiplier => playerProjectileTrailWidthMultiplier;

        public Color EnemyProjectileColor => enemyProjectileColor;
        public float EnemyProjectileTrailSeconds => enemyProjectileTrailSeconds;
        public float EnemyProjectileTrailWidthMultiplier => enemyProjectileTrailWidthMultiplier;

        public Color ParrySparkColor => parrySparkColor;
        public Color ParryRingColor => parryRingColor;
        public Color ParryRingGlitterColor => parryRingGlitterColor;
        public float ParrySparkSeconds => parrySparkSeconds > 0f ? parrySparkSeconds : 0.22f;
        public float ParryRingSeconds => parryRingSeconds > 0f ? parryRingSeconds : 0.32f;
        public float ParryRingGlitterSeconds => parryRingGlitterSeconds > 0f ? parryRingGlitterSeconds : 0.2f;
        public int ParrySparkCount => parrySparkCount > 0 ? parrySparkCount : 34;
        public int ParryRingGlitterCount => parryRingGlitterCount > 0 ? parryRingGlitterCount : 18;
        public int ParryFlameCount => parryFlameCount > 0 ? parryFlameCount : 20;
        public float ParryEffectScale => parryEffectScale > 0f ? parryEffectScale : 1f;
        public int DefenseSparkCount => defenseSparkCount > 0 ? defenseSparkCount : 18;
        public Color DefenseSparkColor => defenseSparkColor;
        public Color DefenseRingColor => defenseRingColor;
        public float DefenseEffectScale => defenseEffectScale > 0f ? defenseEffectScale : 1f;
        public Color BossParrySparkColor => bossParrySparkColor;
        public Color BossParryRingColor => bossParryRingColor;
        public Color BossParryGlitterColor => bossParryGlitterColor;
        public Color BossDefenseSparkColor => bossDefenseSparkColor;
        public Color BossDefenseRingColor => bossDefenseRingColor;

        public int AttackImpactSparkCount => attackImpactSparkCount;
        public int AttackImpactBackSparkCount => attackImpactBackSparkCount;
        public int AttackImpactFlameCount => attackImpactFlameCount;
        public float AttackImpactEffectScale => attackImpactEffectScale > 0f ? attackImpactEffectScale : 0.65f;
        public Color AttackImpactSparkColor => attackImpactSparkColor;
        public Color AttackImpactBackSparkColor => attackImpactBackSparkColor;
        public Color AttackImpactFlameColor => attackImpactFlameColor;
        public Color AttackImpactRingColor => attackImpactRingColor;
        public int PlayerHitSparkCount => playerHitSparkCount;
        public int PlayerHitBackSparkCount => playerHitBackSparkCount;
        public int PlayerHitFlameCount => playerHitFlameCount;
        public float PlayerHitEffectScale => playerHitEffectScale > 0f ? playerHitEffectScale : 0.55f;
        public Color PlayerBodyOverheatedColor => playerBodyOverheatedColor;
        public Color PlayerBodyHitColor => playerBodyHitColor;
        public Color EnemyBodyHitColor => enemyBodyHitColor;
        public float BodyHitColorSeconds => bodyHitColorSeconds > 0f ? bodyHitColorSeconds : 0.08f;
        public Color HeatParryOutlineColor => heatParryOutlineColor;
        public Color HeatDefenseOutlineColor => heatDefenseOutlineColor;
        public Color HeatHitOutlineColor => heatHitOutlineColor;
        public Color HeatOverheatBlinkOutlineColor => heatOverheatBlinkOutlineColor;
        public Color HeatOverheatBlinkFillColor => heatOverheatBlinkFillColor;
        public float HeatOverheatBlinkFrequency => heatOverheatBlinkFrequency > 0f ? heatOverheatBlinkFrequency : 5f;
        public float HeatOverheatBlinkMinAlpha => Mathf.Clamp01(heatOverheatBlinkMinAlpha);

        public Color ExecutionImpactColor => executionImpactColor;
        public Color ExecutionAbsorbColor => executionAbsorbColor;
        public float ExecutionImpactParticleSeconds => executionImpactParticleSeconds;
        public int ExecutionImpactParticleCount => executionImpactParticleCount;
        public int ExecutionAbsorbParticleCount => executionAbsorbParticleCount;
    }
}
