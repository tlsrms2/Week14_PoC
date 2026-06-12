using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    /// <summary>
    /// 적 하나의 전체 프로필을 정의하는 ScriptableObject.
    /// 기존 EnemyCombatConfig를 완전 대체한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Week14/Enemy/Enemy Data", fileName = "EnemyData")]
    public sealed class EnemyData : ScriptableObject
    {
        // ── 메타 ──────────────────────────────────────
        [Header("메타")]
        [SerializeField] private string displayName;
        [SerializeField] private EnemyCategory category = EnemyCategory.Normal;

        // ── 내구도/열 ─────────────────────────────────
        [Header("내구도")]
        [SerializeField, Min(1f)] private float maxDurability = 60f;
        [SerializeField, Min(0f)] private float durabilityDepletedSeconds = 2f;

        [Header("열 게이지")]
        [SerializeField, Min(1f)] private float maxHeat = 100f;
        [SerializeField, Min(0f)] private float heatCoolingPerSecond = 8f;
        [SerializeField, Min(0f)] private float overheatSeconds = 4f;
        [SerializeField, Range(0f, 1f)] private float heatAfterOverheatRatio = 0.2f;

        // ── 색상 ──────────────────────────────────────
        [Header("색상")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color overheatedColor = new(0.45f, 0.65f, 1f, 1f);
        [SerializeField] private Color durabilityDepletedColor = new(0.6f, 0.6f, 0.6f, 1f);

        // ── 감지 ──────────────────────────────────────
        [Header("감지")]
        [SerializeField, Min(0f)] private float detectionRange = 10f;
        [SerializeField, Range(0f, 360f)] private float fieldOfViewAngle = 160f;

        // ── 이동 ──────────────────────────────────────
        [Header("이동")]
        [SerializeField, Min(0f)] private float moveSpeed = 2.8f;
        [SerializeField] private PatrolMode patrolMode = PatrolMode.Stationary;

        [Tooltip("Patrol 모드: 웨이포인트 도착 후 대기 시간")]
        [SerializeField, Min(0f)] private float patrolWaitTime = 1f;

        // ── 공격 ──────────────────────────────────────
        [Header("공격")]
        [SerializeField, Min(0f)] private float attackRange = 8f;
        [SerializeField, Min(0f)] private float windupSeconds = 0.35f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.35f;
        [SerializeField, Min(0f)] private float heatPerShot = 34f;
        [SerializeField, Min(0f)] private float heatCoolingDelayAfterShot = 0.6f;

        [Tooltip("공격 패턴 목록 (일반=1개, 보스=여러개). 순차(Round-Robin) 실행.")]
        [SerializeField] private List<AttackTimeline> attackTimelines = new();

        // ── 발사체 ────────────────────────────────────
        [Header("발사체")]
        [SerializeField] private EnemyProjectile projectilePrefab;
        [SerializeField, Min(0f)] private float projectileSpeed = 7f;
        [SerializeField, Min(0f)] private float projectileLifetime = 3f;
        [SerializeField, Min(0f)] private float projectileRadius = 0.12f;
        [SerializeField] private Color projectileColor = new(1f, 0.95f, 0.25f, 1f);
        [SerializeField, Min(0.01f)] private float projectileTrailSeconds = 0.1f;
        [SerializeField, Min(0.1f)] private float projectileTrailWidthMultiplier = 3f;
        [SerializeField, Min(1f)] private float projectileGlowScale = 3.2f;

        // ── 상태 UI 색상 ──────────────────────────────
        [Header("상태바 UI")]
        [SerializeField] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color durabilityBarColor = new(0.75f, 0.95f, 1f, 1f);
        [SerializeField] private Color heatBarColor = new(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private Color overheatedBarColor = Color.red;
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [SerializeField] private Color executionIndicatorColor = Color.red;

        // ── 공개 프로퍼티 ─────────────────────────────
        // 메타
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public EnemyCategory Category => category;

        // 내구도/열
        public float MaxDurability => maxDurability;
        public float DurabilityDepletedSeconds => durabilityDepletedSeconds;
        public float MaxHeat => maxHeat;
        public float HeatCoolingPerSecond => heatCoolingPerSecond;
        public float OverheatSeconds => overheatSeconds;
        public float HeatAfterOverheatRatio => heatAfterOverheatRatio;

        // 색상
        public Color NormalColor => normalColor;
        public Color OverheatedColor => overheatedColor;
        public Color DurabilityDepletedColor => durabilityDepletedColor;

        // 감지
        public float DetectionRange => detectionRange;
        public float FieldOfViewAngle => fieldOfViewAngle;

        // 이동
        public float MoveSpeed => moveSpeed;
        public PatrolMode PatrolMode => patrolMode;
        public float PatrolWaitTime => patrolWaitTime;

        // 공격
        public float AttackRange => attackRange;
        public float WindupSeconds => windupSeconds;
        public float RecoverySeconds => recoverySeconds;
        public float HeatPerShot => heatPerShot;
        public float HeatCoolingDelayAfterShot => heatCoolingDelayAfterShot;
        public IReadOnlyList<AttackTimeline> AttackTimelines => attackTimelines;

        // 발사체
        public EnemyProjectile ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileLifetime => projectileLifetime;
        public float ProjectileRadius => projectileRadius;
        public Color ProjectileColor => projectileColor;
        public float ProjectileTrailSeconds => projectileTrailSeconds;
        public float ProjectileTrailWidthMultiplier => projectileTrailWidthMultiplier;
        public float ProjectileGlowScale => projectileGlowScale;

        // 상태바 UI
        public Color StatusBarBackgroundColor => statusBarBackgroundColor;
        public Color DurabilityBarColor => durabilityBarColor;
        public Color HeatBarColor => heatBarColor;
        public Color OverheatedBarColor => overheatedBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;
    }
}
