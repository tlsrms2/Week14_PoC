using System.Collections.Generic;
using UnityEngine;

namespace Week14.Encounter
{
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Min(0f)] private float radius = 0f;

        [Header("순찰 웨이포인트 (Patrol 모드 전용)")]
        [Tooltip("자식 Transform을 웨이포인트로 사용. 비어있으면 자동 수집.")]
        [SerializeField] private List<Transform> patrolWaypointTransforms = new();

        public GameObject EnemyPrefab => enemyPrefab;
        public int Count => count;
        public float Radius => radius;

        /// <summary>월드 좌표 기준 웨이포인트 리스트 반환</summary>
        public List<Vector3> GetPatrolWaypoints()
        {
            if (patrolWaypointTransforms == null)
            {
                patrolWaypointTransforms = new List<Transform>();
            }

            // 비어있다면 자식 오브젝트들을 순찰 경로 웨이포인트로 자동 수집
            if (patrolWaypointTransforms.Count == 0)
            {
                foreach (Transform child in transform)
                {
                    patrolWaypointTransforms.Add(child);
                }
            }

            var result = new List<Vector3>();
            foreach (var wp in patrolWaypointTransforms)
            {
                if (wp != null) result.Add(wp.position);
            }
            return result;
        }

        public void Configure(GameObject prefab, int spawnCount, float spawnRadius)
        {
            enemyPrefab = prefab;
            count = Mathf.Max(1, spawnCount);
            radius = Mathf.Max(0f, spawnRadius);
        }

        public Vector3 GetSpawnPosition(int index)
        {
            if (radius <= 0f || count <= 1)
            {
                return transform.position;
            }

            float angle = (Mathf.PI * 2f / count) * index;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            return transform.position + offset;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);

            // 웨이포인트 경로 표시
            if (patrolWaypointTransforms is { Count: > 0 })
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolWaypointTransforms.Count; i++)
                {
                    if (patrolWaypointTransforms[i] == null) continue;
                    Gizmos.DrawWireSphere(patrolWaypointTransforms[i].position, 0.2f);
                    int next = (i + 1) % patrolWaypointTransforms.Count;
                    if (patrolWaypointTransforms[next] != null)
                        Gizmos.DrawLine(patrolWaypointTransforms[i].position, patrolWaypointTransforms[next].position);
                }
            }
        }
    }
}
