using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    /// <summary>
    /// 적 하나의 전체 프로필을 정의하는 ScriptableObject.
    /// 기존 EnemyCombatConfig를 완전 대체한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Week14/Enemy/Enemy Data", fileName = "EnemyData")]
    public class EnemyData : ScriptableObject
    {
        // ── 메타 ──────────────────────────────────────
        [Header("메타")]
        [Tooltip("인스펙터와 UI에서 사용할 적 표시 이름입니다. 비워두면 에셋 이름을 사용합니다.")]
        [SerializeField] private string displayName;
        [Tooltip("이 적이 사용할 공통 전투 이펙트 데이터입니다.")]
        [SerializeField] private CombatEffectData effectData;

        // ── 내구도/열 ─────────────────────────────────
        [Header("내구도")]
        [Tooltip("적의 최대 내구도입니다.")]
        [SerializeField, Min(1f)] private float maxDurability = 60f;
        [Tooltip("내구도가 0이 된 뒤 처형 가능 상태로 유지되는 시간입니다.")]
        [SerializeField, Min(0f)] private float durabilityDepletedSeconds = 2f;

        [Header("열 게이지")]
        [Tooltip("적이 과열되기까지 필요한 최대 열기입니다.")]
        [SerializeField, Min(1f)] private float maxHeat = 100f;
        [Tooltip("열기 감소가 막혀 있지 않을 때 초당 감소하는 열기량입니다.")]
        [SerializeField, Min(0f)] private float heatCoolingPerSecond = 8f;
        [Tooltip("과열 상태가 유지되는 시간입니다. 이 시간 동안 적은 행동하지 않습니다.")]
        [SerializeField, Min(0f)] private float overheatSeconds = 4f;

        // ── 색상 ──────────────────────────────────────
        [Header("색상")]
        [Tooltip("기본 상태에서 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("과열 상태에서 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color overheatedColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("내구도 고갈 상태에서 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color durabilityDepletedColor = new(0.6f, 0.6f, 0.6f, 1f);
        [Tooltip("플레이어 공격으로 경직된 동안 적 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        // ── 감지 ──────────────────────────────────────
        [Header("감지")]
        [Tooltip("플레이어를 감지할 수 있는 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float detectionRange = 10f;

        // ── 이동 ──────────────────────────────────────
        [Header("이동")]
        [Tooltip("적의 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.8f;
        // ── 공격 ──────────────────────────────────────
        [Header("공격")]
        [Tooltip("플레이어가 이 거리 안에 있으면 공격 상태로 진입할 수 있습니다.")]
        [SerializeField, Min(0f)] private float attackRange = 8f;
        [Tooltip("플레이어를 마주 보고 교전 상태에 들어간 뒤 첫 공격까지 기다리는 시간입니다.")]
        [SerializeField, Min(0f)] private float initialAttackDelaySeconds = 2f;
        [Tooltip("공격 시작 전 준비 동작으로 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.35f;
        [Tooltip("공격 타임라인 종료 후 후딜레이로 대기하는 시간입니다.")]
        [SerializeField, Min(0f)] private float recoverySeconds = 0.35f;
        [Tooltip("적 탄이 패링되거나 패링탄과 충돌했을 때 이 적에게 추가되는 열기량입니다.")]
        [SerializeField, Min(0f)] private float heatPerShot = 34f;
        [Tooltip("적 탄이 패링 처리된 뒤 이 적의 열기 자연 감소를 멈추는 시간입니다.")]
        [SerializeField, Min(0f)] private float heatCoolingDelayAfterShot = 0.6f;
        [Tooltip("플레이어 공격을 맞았을 때 적이 경직되는 시간입니다.")]
        [SerializeField, Min(0f)] private float staggerSeconds = 0.18f;
        [Tooltip("경직 중 움찔거리는 위치 흔들림 크기입니다.")]
        [SerializeField, Min(0f)] private float staggerShakeDistance = 0.06f;
        [Tooltip("경직 중 초당 움찔거림 횟수입니다.")]
        [SerializeField, Min(0f)] private float staggerShakeFrequency = 32f;

        [Tooltip("공격 패턴 목록 (일반=1개, 보스=여러개). 순차(Round-Robin) 실행.")]
        [FormerlySerializedAs("bossAttackTimelines")]
        [SerializeField] private List<AttackTimeline> attackTimelines = new();

        // ── 발사체 ────────────────────────────────────
        [Header("발사체")]
        [Tooltip("적이 발사할 탄 프리팹입니다.")]
        [SerializeField] private EnemyProjectile projectilePrefab;
        [Tooltip("적 탄이 플레이어에게 맞았을 때 주는 데미지입니다.")]
        [SerializeField, Min(0f)] private float projectileDamage = 18f;
        [Tooltip("적 총알이 생성 위치에서 멈춰 기를 모으는 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileChargeSeconds = 0.35f;
        [Tooltip("적 탄의 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float projectileSpeed = 7f;
        [Tooltip("적 탄이 자동으로 사라지기까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileLifetime = 3f;
        [Tooltip("적 탄의 충돌 반지름입니다.")]
        [SerializeField, Min(0f)] private float projectileRadius = 0.12f;
        [Tooltip("적 탄 발사 시 플레이어 이동을 예측하는 최대 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileLeadPredictionSeconds = 0.45f;
        [Tooltip("적 탄이 발사 후 플레이어를 추적하는 시간입니다.")]
        [SerializeField, Min(0f)] private float projectileHomingSeconds = 0.8f;
        [Tooltip("적 탄이 플레이어 쪽으로 휘어질 수 있는 초당 회전 각도입니다.")]
        [SerializeField, Min(0f)] private float projectileHomingTurnDegreesPerSecond = 540f;

        // ── 상태 UI 색상 ──────────────────────────────
        [Header("상태바 UI")]
        [Tooltip("적 주변 상태 UI의 배경 색입니다.")]
        [SerializeField] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [Tooltip("적 내구도 UI의 채움 색입니다.")]
        [SerializeField] private Color durabilityBarColor = new(0.75f, 0.95f, 1f, 1f);
        [Tooltip("적 열기 UI의 기본 채움 색입니다.")]
        [SerializeField] private Color heatBarColor = new(1f, 0.55f, 0.1f, 1f);
        [Tooltip("적 과열 상태에서 열기 UI에 적용할 색입니다.")]
        [SerializeField] private Color overheatedBarColor = Color.red;
        [Tooltip("락온 표시 링의 색입니다.")]
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [Tooltip("처형 가능 표시 링의 색입니다.")]
        [SerializeField] private Color executionIndicatorColor = Color.red;

        // ── 공개 프로퍼티 ─────────────────────────────
        // 메타
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        // 내구도/열
        public float MaxDurability => maxDurability;
        public float DurabilityDepletedSeconds => durabilityDepletedSeconds;
        public float MaxHeat => maxHeat;
        public float HeatCoolingPerSecond => heatCoolingPerSecond;
        public float OverheatSeconds => overheatSeconds;

        // 색상
        public Color NormalColor => normalColor;
        public Color OverheatedColor => overheatedColor;
        public Color DurabilityDepletedColor => durabilityDepletedColor;
        public Color StaggeredColor => staggeredColor;
        // 감지
        public float DetectionRange => detectionRange;

        // 이동
        public float MoveSpeed => moveSpeed;

        // 공격
        public float AttackRange => attackRange;
        public float InitialAttackDelaySeconds => initialAttackDelaySeconds;
        public float WindupSeconds => windupSeconds;
        public float RecoverySeconds => recoverySeconds;
        public float HeatPerShot => heatPerShot;
        public float HeatCoolingDelayAfterShot => heatCoolingDelayAfterShot;
        public float StaggerSeconds => staggerSeconds;
        public float StaggerShakeDistance => staggerShakeDistance;
        public float StaggerShakeFrequency => staggerShakeFrequency;
        public virtual IReadOnlyList<AttackTimeline> AttackTimelines => attackTimelines;

        // 발사체
        public EnemyProjectile ProjectilePrefab => projectilePrefab;
        public float ProjectileDamage => projectileDamage;
        public float ProjectileChargeSeconds => projectileChargeSeconds;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileLeadPredictionSeconds => projectileLeadPredictionSeconds;
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
        public Color BossDefenseSparkColor => effectData != null ? effectData.BossDefenseSparkColor : new Color(0.52f, 0.92f, 1f, 1f);
        public Color BossDefenseRingColor => effectData != null ? effectData.BossDefenseRingColor : new Color(0.28f, 0.72f, 1f, 0.9f);

        // 상태바 UI
        public Color BodyHitColor => effectData != null ? effectData.EnemyBodyHitColor : new Color(1f, 0.35f, 0.25f, 1f);
        public float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : 0.08f;
        public Color StatusBarBackgroundColor => statusBarBackgroundColor;
        public Color DurabilityBarColor => durabilityBarColor;
        public Color HeatBarColor => heatBarColor;
        public Color OverheatedBarColor => overheatedBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;
    }
}
