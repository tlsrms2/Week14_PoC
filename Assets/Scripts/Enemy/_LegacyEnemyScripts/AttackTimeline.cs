using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 적 공격 타임라인의 발사 시점과 이벤트별 패턴을 정의합니다.
    /// </summary>
    [CreateAssetMenu(menuName = "Week14/Enemy/Attack Timeline", fileName = "AttackTimeline")]
    public sealed class AttackTimeline : ScriptableObject
    {
        [Tooltip("공격 패턴을 식별할 표시 이름입니다. 비워두면 에셋 이름을 사용합니다.")]
        [SerializeField] private string timelineName;

        [Tooltip("이 타임라인이 끝난 뒤 다음 공격까지 기다리는 시간입니다.")]
        [SerializeField, Min(0f)] private float cooldownAfter = 1f;

        [Tooltip("Fire Time 기준으로 실행할 공격 이벤트 목록입니다.")]
        [SerializeField] private List<AttackEvent> events = new();

        public string TimelineName => string.IsNullOrWhiteSpace(timelineName) ? name : timelineName;
        public float CooldownAfter => cooldownAfter;
        public IReadOnlyList<AttackEvent> Events => events;

        public float TotalDuration
        {
            get
            {
                if (events == null || events.Count == 0)
                {
                    return 0f;
                }

                float max = 0f;
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].FireTime > max)
                    {
                        max = events[i].FireTime;
                    }
                }

                return max;
            }
        }
    }

    [Serializable]
    public sealed class AttackEvent
    {
        [Tooltip("이 이벤트에서 사용할 공격 방식입니다.")]
        [SerializeField] private EnemyAttackPatternKind patternKind = EnemyAttackPatternKind.DirectSpread;

        [Tooltip("타임라인 시작 후 이 시간이 지나면 이벤트를 실행합니다.")]
        [SerializeField, Min(0f)] private float fireTime;

        [Tooltip("이 이벤트에서 생성할 탄환 수입니다.")]
        [SerializeField, Min(1)] private int bulletCount = 1;

        [Tooltip("탄환을 퍼뜨릴 전체 각도입니다. 원형 패턴에서 0이면 180도를 사용합니다.")]
        [SerializeField, Range(0f, 360f)] private float spreadAngle;

        [Tooltip("원형 생성 또는 돌진 궤적 패턴이 진행되는 시간입니다.")]
        [SerializeField, Min(0f)] private float patternDuration = 0.6f;

        [Tooltip("돌진 궤적 패턴에서 적 이동 속도에 곱할 배율입니다.")]
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2.5f;

        [Tooltip("돌진 궤적 패턴에서 탄환을 남길 최소 이동 거리입니다.")]
        [SerializeField, Min(0.05f)] private float trailBulletSpacing = 0.55f;

        public EnemyAttackPatternKind PatternKind => patternKind;
        public float FireTime => fireTime;
        public int BulletCount => bulletCount;
        public float SpreadAngle => spreadAngle;
        public float PatternDuration => patternDuration;
        public float DashSpeedMultiplier => dashSpeedMultiplier;
        public float TrailBulletSpacing => trailBulletSpacing;
    }

    public enum EnemyAttackPatternKind
    {
        DirectSpread,
        LeftCircleSweep,
        DashTrail
    }
}
