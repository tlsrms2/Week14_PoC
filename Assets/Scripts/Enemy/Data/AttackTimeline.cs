using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    /// <summary>
    /// 하나의 공격 패턴을 정의하는 ScriptableObject.
    /// Inspector에서 타이밍별 발사 이벤트를 자유롭게 편집할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Week14/Enemy/Attack Timeline", fileName = "AttackTimeline")]
    public sealed class AttackTimeline : ScriptableObject
    {
        [SerializeField] private string timelineName;

        [Tooltip("패턴 종료 후 다음 공격까지 쿨다운(초)")]
        [SerializeField, Min(0f)] private float cooldownAfter = 1f;

        [Tooltip("시간순으로 정렬된 발사 이벤트 목록")]
        [SerializeField] private List<AttackEvent> events = new();

        public string TimelineName => string.IsNullOrWhiteSpace(timelineName) ? name : timelineName;
        public float CooldownAfter => cooldownAfter;
        public IReadOnlyList<AttackEvent> Events => events;

        /// <summary>마지막 이벤트의 fireTime. 이벤트가 없으면 0.</summary>
        public float TotalDuration
        {
            get
            {
                if (events == null || events.Count == 0) return 0f;
                float max = 0f;
                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].FireTime > max) max = events[i].FireTime;
                }
                return max;
            }
        }
    }

    /// <summary>
    /// 타임라인 내 단일 발사 이벤트.
    /// 특정 시각에 몇 발을, 어떤 확산/속도/데미지로 발사할지 정의.
    /// </summary>
    [Serializable]
    public sealed class AttackEvent
    {
        [Tooltip("발사 시각 (타임라인 시작 기준, 초)")]
        [SerializeField, Min(0f)] private float fireTime;

        [Tooltip("동시 발사 탄 수")]
        [SerializeField, Min(1)] private int bulletCount = 1;

        [Tooltip("다중 탄환 확산 각도 (0 = 일직선)")]
        [SerializeField, Range(0f, 360f)] private float spreadAngle;

        [Tooltip("탄속")]
        [SerializeField, Min(0.1f)] private float bulletSpeed = 7f;

        [Tooltip("탄당 대미지")]
        [SerializeField, Min(0f)] private float damage = 18f;

        public float FireTime => fireTime;
        public int BulletCount => bulletCount;
        public float SpreadAngle => spreadAngle;
        public float BulletSpeed => bulletSpeed;
        public float Damage => damage;
    }
}
