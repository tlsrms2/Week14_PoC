using UnityEngine;

namespace Week14.Combat
{
    [CreateAssetMenu(menuName = "Week14/Combat/Combat Effect Data", fileName = "CombatEffectData")]
    public sealed class CombatEffectData : ScriptableObject
    {
        [Header("Player Projectile")]
        [Tooltip("플레이어 왼쪽 권총 공격과 총구 화염에 사용할 기본 색입니다.")]
        [SerializeField] private Color attackEffectColor = new(1f, 0.35f, 0.12f, 0.55f);
        [Tooltip("오른쪽 권총 패링탄과 패링 성공 이펙트에 사용할 기본 색입니다.")]
        [SerializeField] private Color parryEffectColor = new(0.2f, 0.65f, 1f, 0.45f);
        [Tooltip("처형 중 발사되는 탄환의 색입니다.")]
        [SerializeField] private Color executionShotColor = Color.white;
        [Tooltip("플레이어 투사체 궤적이 남는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float playerProjectileTrailSeconds = 0.08f;
        [Tooltip("플레이어 투사체 궤적 두께 배율입니다.")]
        [SerializeField, Min(0.1f)] private float playerProjectileTrailWidthMultiplier = 2.8f;

        [Header("Parry")]
        [Tooltip("패링 성공 시 생성되는 스파크 색입니다.")]
        [SerializeField] private Color parrySparkColor = new(1f, 0.88f, 0.35f, 1f);
        [Tooltip("패링 성공 시 생성되는 링 색입니다.")]
        [SerializeField] private Color parryRingColor = new(0.45f, 0.9f, 1f, 0.75f);
        [Tooltip("패링 성공 링 주변 글리터 색입니다.")]
        [SerializeField] private Color parryRingGlitterColor = new(1f, 0.96f, 0.68f, 1f);
        [Tooltip("패링 스파크가 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float parrySparkSeconds = 0.22f;
        [Tooltip("패링 링이 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float parryRingSeconds = 0.32f;
        [Tooltip("패링 링 글리터가 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float parryRingGlitterSeconds = 0.2f;
        [Tooltip("패링 성공 시 생성되는 스파크 수입니다.")]
        [SerializeField, Min(0)] private int parrySparkCount = 34;
        [Tooltip("패링 성공 링 주변에 생성되는 글리터 수입니다.")]
        [SerializeField, Min(0)] private int parryRingGlitterCount = 18;
        [Tooltip("패링 성공 시 생성되는 화염 입자 수입니다.")]
        [SerializeField, Min(0)] private int parryFlameCount = 20;
        [Tooltip("패링 성공 이펙트 전체 크기 배율입니다.")]
        [SerializeField, Min(0f)] private float parryEffectScale = 1f;

        [Header("Hit Impact")]
        [Tooltip("플레이어 공격이 적에게 적중했을 때 전방 스파크 색입니다.")]
        [SerializeField] private Color attackImpactSparkColor = new(1f, 0.92f, 0.62f, 1f);
        [Tooltip("플레이어 공격이 적에게 적중했을 때 반대 방향 스파크 색입니다.")]
        [SerializeField] private Color attackImpactBackSparkColor = new(1f, 0.52f, 0.12f, 1f);
        [Tooltip("플레이어 공격이 적에게 적중했을 때 화염 입자 색입니다.")]
        [SerializeField] private Color attackImpactFlameColor = new(1f, 0.48f, 0.08f, 1f);
        [Tooltip("플레이어 공격이 적에게 적중했을 때 충격 링 색입니다.")]
        [SerializeField] private Color attackImpactRingColor = new(1f, 0.92f, 0.62f, 0.72f);
        [Tooltip("플레이어 공격 적중 시 생성되는 전방 스파크 수입니다.")]
        [SerializeField, Min(0)] private int attackImpactSparkCount = 14;
        [Tooltip("플레이어 공격 적중 시 반대 방향으로 튀는 스파크 수입니다.")]
        [SerializeField, Min(0)] private int attackImpactBackSparkCount = 6;
        [Tooltip("플레이어 공격 적중 시 생성되는 화염 입자 수입니다.")]
        [SerializeField, Min(0)] private int attackImpactFlameCount = 8;
        [Tooltip("플레이어 공격 적중 이펙트 전체 크기 배율입니다.")]
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
        [Tooltip("플레이어 탄환이 0일 때 몸체에 적용할 색입니다.")]
        [SerializeField] private Color playerBodyBulletEmptyColor = new(1f, 0.2f, 0.12f, 1f);
        [Tooltip("플레이어가 피격됐을 때 몸체에 잠시 적용할 색입니다.")]
        [SerializeField] private Color playerBodyHitColor = new(1f, 0.85f, 0.25f, 1f);
        [Tooltip("적이 피격됐을 때 몸체에 잠시 적용할 색입니다.")]
        [SerializeField] private Color enemyBodyHitColor = new(1f, 0.35f, 0.25f, 1f);
        [Tooltip("피격 색이 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float bodyHitColorSeconds = 0.08f;

        [Header("Execution")]
        [Tooltip("처형 타격 순간 사용하는 이펙트 색입니다.")]
        [SerializeField] private Color executionImpactColor = new(0.9f, 0.02f, 0.04f, 1f);
        [Tooltip("처형 타격 입자가 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionImpactParticleSeconds = 0.55f;
        [Tooltip("처형 타격 순간 생성되는 입자 수입니다.")]
        [SerializeField, Min(0)] private int executionImpactParticleCount = 28;

        public Color AttackEffectColor => attackEffectColor;
        public Color ParryEffectColor => parryEffectColor;
        public Color ExecutionShotColor => executionShotColor;
        public float PlayerProjectileTrailSeconds => playerProjectileTrailSeconds;
        public float PlayerProjectileTrailWidthMultiplier => playerProjectileTrailWidthMultiplier;

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
        public Color PlayerBodyBulletEmptyColor => playerBodyBulletEmptyColor;
        public Color PlayerBodyHitColor => playerBodyHitColor;
        public Color EnemyBodyHitColor => enemyBodyHitColor;
        public float BodyHitColorSeconds => bodyHitColorSeconds > 0f ? bodyHitColorSeconds : 0.08f;

        public Color ExecutionImpactColor => executionImpactColor;
        public float ExecutionImpactParticleSeconds => executionImpactParticleSeconds;
        public int ExecutionImpactParticleCount => executionImpactParticleCount;
    }
}
