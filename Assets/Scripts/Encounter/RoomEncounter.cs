using System;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;
using Week14.Map;

namespace Week14.Encounter
{
    public sealed class RoomEncounter : MonoBehaviour, IRoomEnterHandler
    {
        [SerializeField] private bool startOnEnable;
        [SerializeField] private bool respawnAfterCleared;
        [SerializeField] private bool destroyEnemyOnDeath = true;
        [SerializeField] private bool combatRoomsOnly = true;
        [SerializeField] private bool autoCollectSpawnPoints = true;
        [SerializeField] private List<EnemySpawnPoint> spawnPoints = new List<EnemySpawnPoint>();

        private readonly List<Health> aliveEnemies = new List<Health>();
        private readonly Dictionary<Health, GameObject> enemyObjectByHealth = new Dictionary<Health, GameObject>();
        private bool started;
        private bool cleared;

        public event Action<RoomEncounter> Started;
        public event Action<RoomEncounter> Cleared;

        public bool IsStarted => started;
        public bool IsCleared => cleared;
        public int AliveEnemyCount => aliveEnemies.Count;

        private void Awake()
        {
            if (autoCollectSpawnPoints)
            {
                CollectSpawnPoints();
            }
        }

        private void OnEnable()
        {
            if (!startOnEnable)
            {
                return;
            }

            TryStartEncounter();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        public void TryStartEncounter()
        {
            if (started || (cleared && !respawnAfterCleared))
            {
                return;
            }

            started = true;
            cleared = false;
            SpawnEnemies();
            Started?.Invoke(this);

            if (aliveEnemies.Count == 0)
            {
                MarkCleared();
            }
        }

        public void OnPlayerEnteredRoom(RoomSlot slot)
        {
            if (combatRoomsOnly
                && slot.Definition != null
                && slot.Definition.RoomType != RoomType.Combat
                && slot.Definition.RoomType != RoomType.Boss)
            {
                return;
            }

            TryStartEncounter();
        }

        public void ResetEncounter()
        {
            DestroySpawnedEnemies();
            UnsubscribeAll();
            started = false;
            cleared = false;
        }

        private void CollectSpawnPoints()
        {
            spawnPoints.Clear();
            GetComponentsInChildren(true, spawnPoints);
        }

        private void SpawnEnemies()
        {
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning($"{nameof(RoomEncounter)} has no {nameof(EnemySpawnPoint)}.", this);
            }

            for (int i = 0; i < spawnPoints.Count; i++)
            {
                EnemySpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint == null)
                {
                    Debug.LogWarning($"{nameof(RoomEncounter)} has a missing {nameof(EnemySpawnPoint)} reference.", this);
                    continue;
                }

                if (spawnPoint.EnemyPrefab == null)
                {
                    Debug.LogWarning($"{nameof(EnemySpawnPoint)} requires an enemy prefab.", spawnPoint);
                    continue;
                }

                for (int count = 0; count < spawnPoint.Count; count++)
                {
                    GameObject enemy = Instantiate(
                        spawnPoint.EnemyPrefab,
                        spawnPoint.GetSpawnPosition(count),
                        spawnPoint.transform.rotation,
                        transform);
                    enemy.SetActive(true);

                    Health enemyHealth = enemy.GetComponentInChildren<Health>();
                    if (enemyHealth == null)
                    {
                        Debug.LogWarning($"{spawnPoint.EnemyPrefab.name} requires {nameof(Health)}.", enemy);
                        Destroy(enemy);
                        continue;
                    }

                    if (enemy.GetComponentInChildren<Week14.Enemy.EnemyAI>() == null
                        && enemy.GetComponentInChildren<Week14.Enemy.BossAI>() == null
                        && enemy.GetComponentInChildren<Week14.Enemy.Drone>() == null)
                    {
                        Debug.LogWarning($"{spawnPoint.EnemyPrefab.name} requires {nameof(Week14.Enemy.EnemyAI)}, {nameof(Week14.Enemy.BossAI)} or {nameof(Week14.Enemy.Drone)}.", enemy);
                    }

                    aliveEnemies.Add(enemyHealth);
                    enemyObjectByHealth.Add(enemyHealth, enemy);
                    enemyHealth.Died += HandleEnemyDied;
                }
            }
        }

        private void HandleEnemyDied(Health enemyHealth)
        {
            enemyHealth.Died -= HandleEnemyDied;
            aliveEnemies.Remove(enemyHealth);

            if (enemyObjectByHealth.TryGetValue(enemyHealth, out GameObject enemyObject))
            {
                enemyObjectByHealth.Remove(enemyHealth);
                if (destroyEnemyOnDeath && enemyObject != null)
                {
                    Destroy(enemyObject);
                }
            }

            if (aliveEnemies.Count == 0)
            {
                MarkCleared();
            }
        }

        private void MarkCleared()
        {
            if (cleared)
            {
                return;
            }

            cleared = true;
            started = false;
            Cleared?.Invoke(this);
        }

        private void UnsubscribeAll()
        {
            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                if (aliveEnemies[i] != null)
                {
                    aliveEnemies[i].Died -= HandleEnemyDied;
                }
            }

            aliveEnemies.Clear();
            enemyObjectByHealth.Clear();
        }

        private void DestroySpawnedEnemies()
        {
            foreach (GameObject enemyObject in enemyObjectByHealth.Values)
            {
                if (enemyObject != null)
                {
                    Destroy(enemyObject);
                }
            }

            enemyObjectByHealth.Clear();
        }
    }
}
