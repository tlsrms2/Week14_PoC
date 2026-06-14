using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 하나의 공격 패턴을 정의하는 ScriptableObject.
    /// 타임라인별 발사 시점, 발사 수, 퍼짐만 가진다.
    /// 탄속과 데미지는 EnemyData에서 관리한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Week14/Enemy/Attack Timeline", fileName = "AttackTimeline")]
    public sealed class AttackTimeline : ScriptableObject
    {
        [Tooltip("공격 패턴의 표시 이름입니다. 비워두면 에셋 이름을 사용합니다.")]
        [SerializeField] private string timelineName;

        [Tooltip("패턴 종료 후 다음 공격까지 기다리는 시간입니다.")]
        [SerializeField, Min(0f)] private float cooldownAfter = 1f;

        [Tooltip("이 공격 패턴 안에서 실행할 발사 이벤트 목록입니다. Fire Time 기준으로 처리됩니다.")]
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
        [Tooltip("타임라인 시작 후 이 시간이 지나면 탄을 발사합니다.")]
        [SerializeField, Min(0f)] private float fireTime;

        [Tooltip("이 이벤트에서 동시에 발사할 탄 수입니다.")]
        [SerializeField, Min(1)] private int bulletCount = 1;

        [Tooltip("여러 탄을 발사할 때 전체 탄이 퍼지는 각도입니다.")]
        [SerializeField, Range(0f, 360f)] private float spreadAngle;

        public float FireTime => fireTime;
        public int BulletCount => bulletCount;
        public float SpreadAngle => spreadAngle;
    }
}
