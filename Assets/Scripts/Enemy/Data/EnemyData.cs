using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Enemy/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Meta")]
        [Tooltip("상태 UI 등에 표시할 적 이름입니다. 비워두면 에셋 이름을 사용합니다.")]
        [SerializeField] private string displayName;
        [Tooltip("이 적이 사용할 공통 전투 이펙트와 색상 설정입니다.")]
        [SerializeField] private CombatEffectData effectData;

        [Header("Bullet")]
        [Tooltip("적이 보유할 수 있는 최대 탄환 수입니다.")]
        [SerializeField, Min(1)] private int maxBullets = 60;
        [Tooltip("적 탄환이 0이 되었을 때 처형 가능 상태를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float bulletEmptyExecutionSeconds = 2f;

        [Header("Color")]
        [Tooltip("기본 상태에서 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("적 탄환이 0이 되었을 때 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color bulletEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("플레이어 공격에 맞아 경직 중일 때 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        [Header("Detection")]
        [Tooltip("플레이어를 감지할 수 있는 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float detectionRange = 10f;

        [Header("Movement")]
        [Tooltip("적의 기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.8f;
        [Header("Attack")]
        [Tooltip("플레이어가 이 거리 안에 있으면 공격 상태로 전환할 수 있습니다.")]
        [SerializeField, Min(0f)] private float attackRange = 8f;
        [Tooltip("교전 상태에 처음 들어간 뒤 첫 공격까지 기다리는 시간입니다.")]
        [SerializeField, Min(0f)] private float initialAttackDelaySeconds = 2f;
        [Tooltip("공격 타임라인 실행 전 준비 동작으로 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.35f;
        [Tooltip("공격 타임라인 종료 후 다음 행동까지 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float recoverySeconds = 0.35f;
        [Tooltip("플레이어 공격에 맞았을 때 적이 경직되는 시간입니다.")]
        [SerializeField, Min(0f)] private float staggerSeconds = 0.18f;
        [Tooltip("경직 중 적 몸체가 흔들리는 거리입니다.")]
        [SerializeField, Min(0f)] private float staggerShakeDistance = 0.06f;
        [Tooltip("경직 중 흔들림의 초당 반복 횟수입니다.")]
        [SerializeField, Min(0f)] private float staggerShakeFrequency = 32f;

        [Tooltip("적이 순서대로 실행할 공격 타임라인 목록입니다.")]
        [SerializeField] private List<AttackTimeline> attackTimelines = new();

        [Header("Projectile")]
        [Tooltip("적이 발사할 투사체 프리팹입니다.")]
        [SerializeField] private EnemyProjectile projectilePrefab;
        [Tooltip("적 투사체가 플레이어에게 맞았을 때 플레이어 탄환을 감소시키는 양입니다.")]
        [SerializeField, Min(0)] private int projectileBulletDamage = 18;
        [Tooltip("적 투사체가 발사 전 충전 상태로 머무는 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileChargeSeconds = 0.35f;
        [Tooltip("적 투사체 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float projectileSpeed = 7f;
        [Tooltip("적 투사체가 자동으로 사라지기까지 걸리는 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileLifetime = 3f;
        [Tooltip("적 투사체 충돌 반지름입니다.")]
        [SerializeField, Min(0f)] private float projectileRadius = 0.12f;
        [Tooltip("발사 방향 계산 시 플레이어 이동을 예측할 최대 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileLeadPredictionSeconds = 0.45f;
        [Tooltip("켜면 이 적의 기본 투사체가 플레이어를 추적합니다.")]
        [SerializeField] private bool projectileHomingEnabled;
        [Tooltip("발사 후 플레이어를 추적하는 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileHomingSeconds = 0.8f;
        [Tooltip("추적 중 적 투사체가 초당 회전할 수 있는 최대 각도입니다.")]
        [SerializeField, Min(0f)] private float projectileHomingTurnDegreesPerSecond = 540f;

        [Header("Status UI")]
        [Tooltip("적 탄환 상태 UI의 배경 색입니다.")]
        [SerializeField] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [Tooltip("적 탄환 UI의 기본 채움 색입니다.")]
        [SerializeField] private Color bulletBarColor = new(1f, 0.55f, 0.1f, 1f);
        [Tooltip("적 탄환이 비었을 때 탄환 UI에 적용할 색입니다.")]
        [SerializeField] private Color emptyBulletBarColor = Color.red;
        [Tooltip("플레이어가 이 적을 락온했을 때 표시할 색입니다.")]
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [Tooltip("이 적이 처형 가능할 때 표시할 색입니다.")]
        [SerializeField] private Color executionIndicatorColor = Color.red;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public int MaxBullets => maxBullets;
        public float BulletEmptyExecutionSeconds => bulletEmptyExecutionSeconds;

        public Color NormalColor => normalColor;
        public Color BulletEmptyColor => bulletEmptyColor;
        public Color StaggeredColor => staggeredColor;
        public float DetectionRange => detectionRange;

        public float MoveSpeed => moveSpeed;

        public float AttackRange => attackRange;
        public float InitialAttackDelaySeconds => initialAttackDelaySeconds;
        public float WindupSeconds => windupSeconds;
        public float RecoverySeconds => recoverySeconds;
        public float StaggerSeconds => staggerSeconds;
        public float StaggerShakeDistance => staggerShakeDistance;
        public float StaggerShakeFrequency => staggerShakeFrequency;
        public virtual IReadOnlyList<AttackTimeline> AttackTimelines => attackTimelines;

        public EnemyProjectile ProjectilePrefab => projectilePrefab;
        public int ProjectileBulletDamage => projectileBulletDamage;
        public float ProjectileChargeSeconds => projectileChargeSeconds;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileLeadPredictionSeconds => projectileLeadPredictionSeconds;
        public bool ProjectileHomingEnabled => projectileHomingEnabled;
        public float ProjectileHomingSeconds => projectileHomingSeconds;
        public float ProjectileHomingTurnDegreesPerSecond => projectileHomingTurnDegreesPerSecond;
        public Color ProjectileColor => effectData != null ? effectData.EnemyProjectileColor : new Color(1f, 0.95f, 0.25f, 1f);
        public float ProjectileTrailSeconds => effectData != null ? effectData.EnemyProjectileTrailSeconds : 0.1f;
        public float ProjectileTrailWidthMultiplier => effectData != null ? effectData.EnemyProjectileTrailWidthMultiplier : 3f;
        public int AttackImpactSparkCount => effectData != null ? effectData.AttackImpactSparkCount : 14;
        public int AttackImpactBackSparkCount => effectData != null ? effectData.AttackImpactBackSparkCount : 6;
        public int AttackImpactFlameCount => effectData != null ? effectData.AttackImpactFlameCount : 8;
        public float AttackImpactEffectScale => effectData != null ? effectData.AttackImpactEffectScale : 0.65f;
        public Color AttackImpactSparkColor => effectData != null ? effectData.AttackImpactSparkColor : new Color(1f, 0.92f, 0.62f, 1f);
        public Color AttackImpactBackSparkColor => effectData != null ? effectData.AttackImpactBackSparkColor : new Color(1f, 0.52f, 0.12f, 1f);
        public Color AttackImpactFlameColor => effectData != null ? effectData.AttackImpactFlameColor : new Color(1f, 0.48f, 0.08f, 1f);
        public Color AttackImpactRingColor => effectData != null ? effectData.AttackImpactRingColor : new Color(1f, 0.92f, 0.62f, 0.72f);
        public Color BossParrySparkColor => effectData != null ? effectData.BossParrySparkColor : new Color(1f, 0.78f, 0.18f, 1f);
        public Color BossParryRingColor => effectData != null ? effectData.BossParryRingColor : new Color(1f, 0.42f, 0.08f, 0.85f);
        public Color BossParryGlitterColor => effectData != null ? effectData.BossParryGlitterColor : new Color(1f, 0.95f, 0.52f, 1f);

        public Color BodyHitColor => effectData != null ? effectData.EnemyBodyHitColor : new Color(1f, 0.35f, 0.25f, 1f);
        public float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : 0.08f;
        public Color StatusBarBackgroundColor => statusBarBackgroundColor;
        public Color BulletBarColor => bulletBarColor;
        public Color EmptyBulletBarColor => emptyBulletBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;
    }
}
