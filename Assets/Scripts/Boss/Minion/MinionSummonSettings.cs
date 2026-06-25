using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    [System.Serializable]
    public sealed class MinionSummonSettings
    {
        [SerializeField, Tooltip("소환할 미니언 프리팹입니다.")] private Minion prefab;
        [FormerlySerializedAs("claimSceneDrones")]
        [SerializeField, Tooltip("씬에 이미 배치된 소유자 없는 미니언도 이 보스가 함께 지휘합니다.")] private bool claimSceneMinions = true;
        [FormerlySerializedAs("maxOwnedDrones")]
        [SerializeField, Min(0), Tooltip("소유 미니언 최대 수입니다. 0이면 제한하지 않습니다.")] private int maxOwnedMinions = 5;
        [SerializeField, Min(1), Tooltip("소환 패턴 한 번에 생성할 미니언 수입니다.")] private int summonCount = 1;
        [SerializeField, Min(0f), Tooltip("보스 주변 소환 반지름입니다.")] private float spawnRadius = 1.2f;
        [SerializeField, Min(0f), Tooltip("미니언을 여러 마리 소환할 때 사이 간격입니다.")] private float summonInterval = 0.2f;

        [SerializeField, Min(0f), Tooltip("보스 중심에서 소환 위치까지 이동하며 커지는 시간입니다.")] private float introSeconds = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("소환 시작 시 미니언 크기 비율입니다.")] private float introStartScale = 0.05f;

        [SerializeField, Min(0f), Tooltip("자동 미니언 소환 최소 간격입니다.")] private float minAutoSummonInterval = 4f;
        [SerializeField, Min(0f), Tooltip("자동 미니언 소환 최대 간격입니다.")] private float maxAutoSummonInterval = 7f;

        public Minion Prefab => prefab;
        public bool ClaimSceneMinions => claimSceneMinions;
        public int MaxOwnedMinions => maxOwnedMinions;
        public int SummonCount => summonCount;
        public float SpawnRadius => spawnRadius;
        public float SummonInterval => summonInterval;
        public float IntroSeconds => introSeconds;
        public float IntroStartScale => introStartScale;
        public float MinAutoSummonInterval => minAutoSummonInterval;
        public float MaxAutoSummonInterval => maxAutoSummonInterval;
    }
}
