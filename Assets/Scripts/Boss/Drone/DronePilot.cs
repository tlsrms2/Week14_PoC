using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class DronePilot : GraphBossAI, IMinionPlayerHitHandler
    {
        [SerializeField, Min(0f)] private float bodyHitDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float minionHitDamageMultiplier = 0.5f;

        private readonly Dictionary<Health, Minion> spawnedMinionsByHealth = new();

        public override bool ReceivePlayerHit(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            int sharedDamage = GetBodySharedDamage(bulletDamage);
            return base.ReceivePlayerHit(sharedDamage, strongHit, hitPosition, hitDirection, hitColor);
        }

        public int GetBodySharedDamage(int bulletDamage)
        {
            return GetSharedDamage(bulletDamage, bodyHitDamageMultiplier);
        }

        public bool TryGetMinionSharedDamage(Minion minion, int bulletDamage, out int sharedDamage)
        {
            sharedDamage = 0;

            Health minionHealth = minion != null ? minion.Health : null;
            if (minionHealth == null || !spawnedMinionsByHealth.ContainsKey(minionHealth))
            {
                return false;
            }

            sharedDamage = GetSharedDamage(bulletDamage, minionHitDamageMultiplier);
            return true;
        }

        public bool TryHandleMinionPlayerHit(
            Minion minion,
            int bulletDamage,
            bool strongHit,
            Vector3 hitPosition,
            Vector2 hitDirection,
            Color hitColor)
        {
            if (!TryGetMinionSharedDamage(minion, bulletDamage, out int sharedDamage))
            {
                return false;
            }

            base.ReceivePlayerHit(sharedDamage, strongHit, hitPosition, hitDirection, hitColor);
            return true;
        }

        protected override void OnMinionSpawned(Minion minion)
        {
            base.OnMinionSpawned(minion);
            TrackSpawnedMinion(minion);
        }

        protected override void OnBossDied()
        {
            UntrackAllSpawnedMinions();
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            UntrackAllSpawnedMinions();
            base.OnDisable();
        }

        private void TrackSpawnedMinion(Minion minion)
        {
            Health minionHealth = minion != null ? minion.Health : null;
            if (minionHealth == null || minionHealth.IsDead || spawnedMinionsByHealth.ContainsKey(minionHealth))
            {
                return;
            }

            spawnedMinionsByHealth.Add(minionHealth, minion);
            minionHealth.Died += HandleSpawnedMinionDied;
        }

        private void HandleSpawnedMinionDied(Health minionHealth)
        {
            UntrackSpawnedMinion(minionHealth);
        }

        private void UntrackSpawnedMinion(Health minionHealth)
        {
            if (minionHealth == null || !spawnedMinionsByHealth.Remove(minionHealth))
            {
                return;
            }

            minionHealth.Died -= HandleSpawnedMinionDied;
        }

        private void UntrackAllSpawnedMinions()
        {
            foreach (Health minionHealth in spawnedMinionsByHealth.Keys)
            {
                if (minionHealth != null)
                {
                    minionHealth.Died -= HandleSpawnedMinionDied;
                }
            }

            spawnedMinionsByHealth.Clear();
        }

        private static int GetSharedDamage(int bulletDamage, float multiplier)
        {
            if (bulletDamage <= 0 || multiplier <= 0f)
            {
                return 0;
            }

            return Mathf.Max(1, Mathf.RoundToInt(bulletDamage * multiplier));
        }
    }
}
