using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public abstract class BossEnemyData : ScriptableObject
    {
        [Header("Meta")]
        [Tooltip("상태 UI 등에 표시할 보스 이름입니다. 비워두면 에셋 이름을 사용합니다.")]
        [SerializeField] private string displayName;
        [Tooltip("이 보스가 사용할 공통 전투 이펙트와 색상 설정입니다.")]
        [SerializeField] private CombatEffectData effectData;

        [Header("Bullet")]
        [Tooltip("보스가 보유할 수 있는 최대 탄환 수입니다.")]
        [SerializeField, Min(1)] private int maxBullets = 60;
        [Tooltip("보스 탄환이 0이 되었을 때 처형 가능 상태를 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float bulletEmptyExecutionSeconds = 3f;

        [Header("Color")]
        [Tooltip("기본 상태에서 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color normalColor = Color.white;
        [Tooltip("보스 탄환이 0이 되었을 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color bulletEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [Tooltip("플레이어 공격에 맞아 경직 중일 때 보스 스프라이트에 적용할 색입니다.")]
        [SerializeField] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);

        [Header("Detection")]
        [Tooltip("플레이어를 감지할 수 있는 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float detectionRange = 9f;

        [Header("Movement")]
        [Tooltip("보스의 기본 이동 속도입니다.")]
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;

        [Header("Status UI")]
        [Tooltip("보스 탄환 상태 UI의 배경 색입니다.")]
        [SerializeField] private Color statusBarBackgroundColor = new(0f, 0f, 0f, 0.55f);
        [Tooltip("보스 탄환 UI의 기본 채움 색입니다.")]
        [SerializeField] private Color bulletBarColor = new(1f, 0.55f, 0.1f, 1f);
        [Tooltip("보스 탄환이 비었을 때 탄환 UI에 적용할 색입니다.")]
        [SerializeField] private Color emptyBulletBarColor = Color.red;
        [Tooltip("플레이어가 이 보스를 락온했을 때 표시할 색입니다.")]
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [Tooltip("이 보스가 처형 가능할 때 표시할 색입니다.")]
        [SerializeField] private Color executionIndicatorColor = Color.red;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public int MaxBullets => maxBullets;
        public float BulletEmptyExecutionSeconds => bulletEmptyExecutionSeconds;
        public Color NormalColor => normalColor;
        public Color BulletEmptyColor => bulletEmptyColor;
        public Color StaggeredColor => staggeredColor;
        public float DetectionRange => detectionRange;
        public float MoveSpeed => moveSpeed;
        public Color BodyHitColor => effectData != null ? effectData.EnemyBodyHitColor : new Color(1f, 0.35f, 0.25f, 1f);
        public float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : 0.08f;
        public Color StatusBarBackgroundColor => statusBarBackgroundColor;
        public Color BulletBarColor => bulletBarColor;
        public Color EmptyBulletBarColor => emptyBulletBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;
    }
}
