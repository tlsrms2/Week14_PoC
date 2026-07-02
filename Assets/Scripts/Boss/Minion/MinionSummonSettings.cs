using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    [System.Serializable]
    public sealed class MinionSummonSettings
    {
        [SerializeField, Tooltip("소환할 미니언 프리팹입니다.")] private Minion prefab;
        [SerializeField, Tooltip("순서대로 소환할 2~4번째 미니언 프리팹입니다.")] private Minion[] additionalPrefabs = new Minion[3];
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
        public bool HasAnyPrefab => ConfiguredPrefabCount > 0;
        public int ConfiguredPrefabCount
        {
            get
            {
                int count = prefab != null ? 1 : 0;
                if (additionalPrefabs == null)
                {
                    return count;
                }

                for (int i = 0; i < additionalPrefabs.Length; i++)
                {
                    if (additionalPrefabs[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ResolveSummonCount(int requestedCount)
        {
            if (requestedCount > 0)
            {
                return requestedCount;
            }

            int configuredPrefabCount = ConfiguredPrefabCount;
            return configuredPrefabCount > 1 ? configuredPrefabCount : Mathf.Max(1, summonCount);
        }

        public Minion GetPrefabForSummonIndex(int index)
        {
            int configuredPrefabCount = ConfiguredPrefabCount;
            if (configuredPrefabCount <= 0)
            {
                return null;
            }

            int prefabIndex = Mathf.Abs(index) % configuredPrefabCount;
            if (prefab != null)
            {
                if (prefabIndex == 0)
                {
                    return prefab;
                }

                prefabIndex--;
            }

            if (additionalPrefabs == null)
            {
                return prefab;
            }

            for (int i = 0; i < additionalPrefabs.Length; i++)
            {
                Minion additionalPrefab = additionalPrefabs[i];
                if (additionalPrefab == null)
                {
                    continue;
                }

                if (prefabIndex == 0)
                {
                    return additionalPrefab;
                }

                prefabIndex--;
            }

            return prefab;
        }

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
