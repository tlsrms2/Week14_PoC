using UnityEngine;

using UnityEngine.Serialization;

namespace Week14.Combat
{
    [CreateAssetMenu(menuName = "Week14/Combat/Player Combat Config", fileName = "PlayerCombatConfig")]
    public sealed class PlayerCombatConfig : ScriptableObject
    {
        [Header("Bullet")]
        [Tooltip("플레이어가 보유할 수 있는 최대 탄환 수입니다.")]
        [SerializeField, Min(1)] private int maxBullets = 100;
        [Tooltip("왼쪽 권총을 한 번 발사할 때 소비하는 탄환 수입니다.")]
        [SerializeField, Min(0)] private int leftAttackBulletCost = 1;
        [Tooltip("왼쪽 권총 공격이 적에게 적중했을 때 적 탄환을 감소시키는 양입니다. 아래 표를 사용할 수 없을 때의 기본값으로도 쓰입니다.")]
        [SerializeField, Min(0)] private int attackBulletDamage = 16;
        [Tooltip("남은 탄환 수에 따른 공격 데미지입니다. 인덱스 0은 탄환 1개 남았을 때, 마지막 인덱스는 탄환이 가장 많을 때입니다. 남은 탄환 수가 배열 길이를 넘으면 마지막 값을 사용합니다.")]
        [SerializeField] private int[] attackDamageByRemainingBullets = { 5, 4, 3, 2, 1 };
        [Tooltip("패링 성공 시 플레이어가 회복하는 탄환 수입니다.")]
        [SerializeField, Min(0)] private int parryBulletRecovery = 2;
        [Tooltip("적 몸체와 접촉했을 때 플레이어가 잃는 탄환 수입니다.")]
        [SerializeField, Min(1)] private int enemyBodyContactBulletDamage = 1;
        [Tooltip("적 몸체와 계속 닿아 있을 때 피격을 다시 받을 때까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float enemyBodyContactCooldownSeconds = 0.35f;
        [Tooltip("적 몸체와 부딪혔을 때 플레이어가 튕겨나는 속도입니다.")]
        [SerializeField, Min(0f)] private float enemyBodyContactKnockbackSpeed = 8f;
        [Tooltip("적 몸체와 부딪힌 뒤 입력 이동을 막는 경직 시간입니다.")]
        [SerializeField, Min(0f)] private float enemyBodyContactStaggerSeconds = 0.22f;
        [Tooltip("플레이어의 기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;

        [Header("Left Gun")]
        [Tooltip("플레이어 권총 발사에 사용할 투사체 프리팹입니다.")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [Tooltip("전투 이펙트와 UI 색상 설정입니다.")]
        [SerializeField] private CombatEffectData effectData;
        [Tooltip("플레이어 투사체 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float projectileSpeed = 12f;
        [Tooltip("플레이어 투사체가 자동으로 사라지기까지 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileLifetime = 1f;
        [Tooltip("플레이어 투사체 충돌 반지름입니다.")]
        [SerializeField, Min(0f)] private float projectileRadius = 0.08f;
        [Tooltip("발사 후 총구 방향을 고정해서 보여주는 시간입니다.")]
        [SerializeField, Min(0f)] private float gunAimHoldSeconds = 0.5f;

        [Header("Projectile Parry")]
        [Tooltip("유도탄 조준선이 플레이어에게 닿는 지점을 계산할 때 사용하는 플레이어 몸체 반경입니다.")]
        [FormerlySerializedAs("parryBodyRadius")]
        [SerializeField, Min(0f)] private float playerBodyAimRadius = 0.3f;
        [Header("Mouse Parry Miss Penalty")]
        [SerializeField, Range(0.1f, 1f)] private float mouseParryMinimumRangeScale = 0.5f;
        [SerializeField, Range(0f, 1f)] private float mouseParryMissRangeScaleLoss = 0.12f;
        [SerializeField, Min(0f)] private float mouseParryRangeRecoveryDelay = 0.8f;
        [SerializeField, Min(0f)] private float mouseParryRangeRecoveryPerSecond = 0.45f;
        [SerializeField, Min(0f)] private float mouseParryMissColorSeconds = 0.16f;
        [SerializeField, Min(0f)] private float mouseParryMissShakeSeconds = 0.18f;
        [SerializeField, Min(0f)] private float mouseParryMissShakeAmplitude = 0.035f;
        [SerializeField, Min(0f)] private float mouseParryMissShakeFrequency = 42f;

        [Header("Execution")]
        [Tooltip("처형을 시작할 수 있는 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float executionRange = 1.2f;
        [Tooltip("처형 성공 시 플레이어가 회복하는 탄환 수입니다.")]
        [SerializeField, Min(0)] private int executionBulletRecovery = 45;
        [Tooltip("처형 완료 후 대상을 파괴할지 여부입니다.")]
        [SerializeField] private bool destroyTargetOnExecute = true;
        [Tooltip("처형 시작 후 조준 자세를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionAimSeconds = 0.18f;
        [Tooltip("처형 조준 후 실제 발사까지의 지연 시간입니다.")]
        [SerializeField, Min(0f)] private float executionShotDelaySeconds = 0.12f;
        [Tooltip("처형 발사 후 대상 사망 처리까지의 지연 시간입니다.")]
        [SerializeField, Min(0f)] private float executionKillDelaySeconds = 0.08f;
        [Tooltip("처형 마무리 연출을 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionFinishSeconds = 0.12f;
        [Tooltip("처형 중 카메라 초점이 플레이어와 대상 사이를 따라가는 강도입니다.")]
        [SerializeField, Range(0f, 1f)] private float executionCameraFocusWeight = 1f;
        [Tooltip("처형 중 카메라 줌 배율입니다. 작을수록 더 가까이 보입니다.")]
        [SerializeField, Range(0.35f, 1f)] private float executionCameraZoomMultiplier = 0.62f;
        [Tooltip("처형 발사 순간 화면 어둡게 처리되는 시간입니다.")]
        [SerializeField, Min(0f)] private float executionShotDimSeconds = 0.065f;
        [Tooltip("처형 발사 순간 화면 어둡게 처리의 최대 알파값입니다.")]
        [SerializeField, Range(0f, 1f)] private float executionShotDimAlpha = 0.72f;
        [Tooltip("처형 시작 후 총 발사 연출이 시작되기 전 대기 시간입니다.")]
        [SerializeField, Min(0f)] private float executionFlourishDelaySeconds = 0.1f;
        [Tooltip("처형 사격 전, 총 발사 애니메이션을 반복할 횟수입니다.")]
        [SerializeField, Min(0)] private int executionFlourishShotCount = 3;
        [Tooltip("처형 사격 전 총 발사 애니메이션 반복 사이의 간격입니다.")]
        [SerializeField, Min(0.01f)] private float executionFlourishShotInterval = 0.08f;

        public int MaxBullets => maxBullets;
        public float MoveSpeed => moveSpeed;
        public PlayerProjectile ProjectilePrefab => projectilePrefab;
        public int LeftAttackBulletCost => leftAttackBulletCost;
        public int AttackBulletDamage => attackBulletDamage;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;

        public int GetAttackDamageForRemainingBullets(int remainingBullets)
        {
            if (attackDamageByRemainingBullets == null || attackDamageByRemainingBullets.Length == 0)
            {
                return attackBulletDamage;
            }

            int index = Mathf.Clamp(remainingBullets - 1, 0, attackDamageByRemainingBullets.Length - 1);
            return Mathf.Max(0, attackDamageByRemainingBullets[index]);
        }

        public float ProjectileTrailSeconds => effectData != null ? effectData.PlayerProjectileTrailSeconds : 0.08f;
        public float ProjectileTrailWidthMultiplier => effectData != null ? effectData.PlayerProjectileTrailWidthMultiplier : 2.8f;
        public float GunAimHoldSeconds => gunAimHoldSeconds;
        public Color AttackEffectColor => effectData != null ? effectData.AttackEffectColor : new Color(1f, 0.35f, 0.12f, 0.55f);
        public int ParryBulletRecovery => parryBulletRecovery;
        public int EnemyBodyContactBulletDamage => enemyBodyContactBulletDamage;
        public float EnemyBodyContactCooldownSeconds => enemyBodyContactCooldownSeconds;
        public float EnemyBodyContactKnockbackSpeed => enemyBodyContactKnockbackSpeed;
        public float EnemyBodyContactStaggerSeconds => enemyBodyContactStaggerSeconds;
        public float PlayerBodyAimRadius => playerBodyAimRadius;
        public float MouseParryMinimumRangeScale => mouseParryMinimumRangeScale;
        public float MouseParryMissRangeScaleLoss => mouseParryMissRangeScaleLoss;
        public float MouseParryRangeRecoveryDelay => mouseParryRangeRecoveryDelay;
        public float MouseParryRangeRecoveryPerSecond => mouseParryRangeRecoveryPerSecond;
        public float MouseParryMissColorSeconds => mouseParryMissColorSeconds;
        public float MouseParryMissShakeSeconds => mouseParryMissShakeSeconds;
        public float MouseParryMissShakeAmplitude => mouseParryMissShakeAmplitude;
        public float MouseParryMissShakeFrequency => mouseParryMissShakeFrequency;
        public Color ParryEffectColor => effectData != null ? effectData.ParryEffectColor : new Color(0.2f, 0.65f, 1f, 0.45f);
        public Color ParrySparkColor => effectData != null ? effectData.ParrySparkColor : new Color(1f, 0.88f, 0.35f, 1f);
        public Color ParryRingColor => effectData != null ? effectData.ParryRingColor : new Color(0.45f, 0.9f, 1f, 0.75f);
        public Color ParryRingGlitterColor => effectData != null ? effectData.ParryRingGlitterColor : new Color(1f, 0.96f, 0.68f, 1f);
        public float ParrySparkSeconds => effectData != null ? effectData.ParrySparkSeconds : 0.22f;
        public float ParryRingSeconds => effectData != null ? effectData.ParryRingSeconds : 0.32f;
        public float ParryRingGlitterSeconds => effectData != null ? effectData.ParryRingGlitterSeconds : 0.2f;
        public int ParrySparkCount => effectData != null ? effectData.ParrySparkCount : 34;
        public int ParryRingGlitterCount => effectData != null ? effectData.ParryRingGlitterCount : 18;
        public int ParryFlameCount => effectData != null ? effectData.ParryFlameCount : 20;
        public float ParryEffectScale => effectData != null ? effectData.ParryEffectScale : 1f;
        public int PlayerHitSparkCount => effectData != null ? effectData.PlayerHitSparkCount : 12;
        public int PlayerHitBackSparkCount => effectData != null ? effectData.PlayerHitBackSparkCount : 5;
        public int PlayerHitFlameCount => effectData != null ? effectData.PlayerHitFlameCount : 6;
        public float PlayerHitEffectScale => effectData != null ? effectData.PlayerHitEffectScale : 0.55f;
        public Color PlayerBodyBulletEmptyColor => effectData != null ? effectData.PlayerBodyBulletEmptyColor : new Color(1f, 0.2f, 0.12f, 1f);
        public Color PlayerBodyHitColor => effectData != null ? effectData.PlayerBodyHitColor : new Color(1f, 0.85f, 0.25f, 1f);
        public float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : 0.08f;
        public float ExecutionRange => executionRange;
        public int ExecutionBulletRecovery => executionBulletRecovery;
        public bool DestroyTargetOnExecute => destroyTargetOnExecute;
        public float ExecutionAimSeconds => executionAimSeconds;
        public float ExecutionShotDelaySeconds => executionShotDelaySeconds;
        public float ExecutionKillDelaySeconds => executionKillDelaySeconds;
        public float ExecutionFinishSeconds => executionFinishSeconds;
        public float ExecutionCameraFocusWeight => executionCameraFocusWeight;
        public float ExecutionCameraZoomMultiplier => executionCameraZoomMultiplier;
        public float ExecutionShotDimSeconds => executionShotDimSeconds;
        public float ExecutionShotDimAlpha => executionShotDimAlpha;
        public float ExecutionFlourishDelaySeconds => executionFlourishDelaySeconds;
        public int ExecutionFlourishShotCount => executionFlourishShotCount;
        public float ExecutionFlourishShotInterval => executionFlourishShotInterval;
        public Color ExecutionShotColor => effectData != null ? effectData.ExecutionShotColor : Color.white;
        public Color ExecutionImpactColor => effectData != null ? effectData.ExecutionImpactColor : new Color(0.9f, 0.02f, 0.04f, 1f);
        public float ExecutionImpactParticleSeconds => effectData != null ? effectData.ExecutionImpactParticleSeconds : 0.55f;
        public int ExecutionImpactParticleCount => effectData != null ? effectData.ExecutionImpactParticleCount : 28;
    }
}
