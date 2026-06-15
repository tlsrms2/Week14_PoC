using UnityEngine;

namespace Week14.Encounter
{
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Min(0f)] private float radius = 0f;

        public GameObject EnemyPrefab => enemyPrefab;
        public int Count => count;
        public float Radius => radius;

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
        }
    }
}
