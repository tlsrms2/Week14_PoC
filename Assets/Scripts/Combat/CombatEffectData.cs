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

        [Header("Muzzle Flash VFX")]
        [Tooltip("플레이어와 플레이어 클론의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [SerializeField] private GameObject playerMuzzleFlashVfxPrefab;
        [Tooltip("Hog와 Hog 미니언의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("enemyMuzzleFlashVfxPrefab")]
        [SerializeField] private GameObject hogMuzzleFlashVfxPrefab;
        [Tooltip("Muscle과 Muscle 미니언의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [SerializeField] private GameObject muscleMuzzleFlashVfxPrefab;
        [Tooltip("Hacker와 Hacker 미니언의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [SerializeField] private GameObject hackerMuzzleFlashVfxPrefab;
        [Tooltip("Assassin과 Assassin 미니언의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [SerializeField] private GameObject assassinMuzzleFlashVfxPrefab;
        [Tooltip("Arsonist와 Arsonist 미니언의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [SerializeField] private GameObject arsonistMuzzleFlashVfxPrefab;
        [Tooltip("Conductor와 Conductor 미니언의 총구에서 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 발사 방향입니다.")]
        [SerializeField] private GameObject conductorMuzzleFlashVfxPrefab;

        [Header("Parry")]
        [Tooltip("패링 성공 지점에 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 패링 방향입니다.")]
        [SerializeField] private GameObject parrySuccessVfxPrefab;

        [Header("Hit Impact")]
        [Tooltip("플레이어 공격이 적에게 적중했을 때 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 타격 방향입니다.")]
        [SerializeField] private GameObject enemyHitVfxPrefab;
        [Tooltip("플레이어가 적 공격에 맞았을 때 생성할 일회성 이펙트 프리팹입니다. 로컬 +X가 타격 방향입니다.")]
        [SerializeField] private GameObject playerHitVfxPrefab;

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
        public GameObject PlayerMuzzleFlashVfxPrefab => playerMuzzleFlashVfxPrefab;
        public GameObject HogMuzzleFlashVfxPrefab => hogMuzzleFlashVfxPrefab;
        public GameObject MuscleMuzzleFlashVfxPrefab => muscleMuzzleFlashVfxPrefab;
        public GameObject HackerMuzzleFlashVfxPrefab => hackerMuzzleFlashVfxPrefab;
        public GameObject AssassinMuzzleFlashVfxPrefab => assassinMuzzleFlashVfxPrefab;
        public GameObject ArsonistMuzzleFlashVfxPrefab => arsonistMuzzleFlashVfxPrefab;
        public GameObject ConductorMuzzleFlashVfxPrefab => conductorMuzzleFlashVfxPrefab;
        public GameObject ParrySuccessVfxPrefab => parrySuccessVfxPrefab;
        public GameObject EnemyHitVfxPrefab => enemyHitVfxPrefab;
        public GameObject PlayerHitVfxPrefab => playerHitVfxPrefab;
        public Color PlayerBodyBulletEmptyColor => playerBodyBulletEmptyColor;
        public Color PlayerBodyHitColor => playerBodyHitColor;
        public Color EnemyBodyHitColor => enemyBodyHitColor;
        public float BodyHitColorSeconds => bodyHitColorSeconds > 0f ? bodyHitColorSeconds : 0.08f;

        public Color ExecutionImpactColor => executionImpactColor;
        public float ExecutionImpactParticleSeconds => executionImpactParticleSeconds;
        public int ExecutionImpactParticleCount => executionImpactParticleCount;
    }
}
