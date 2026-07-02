using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class DronePilot : GraphBossAI, IMinionPlayerHitHandler
    {
        [SerializeField, Min(0f)] private float bodyHitDamageMultiplier = 1f;
        [SerializeField, Min(0f)] private float minionHitDamageMultiplier = 0.5f;
        [SerializeField, Range(0f, 1f)] private float minionOutlineIdleAlpha = 0.3f;
        [SerializeField, Min(0f)] private float minionOutlineFlashSeconds = 0.12f;

        private readonly Dictionary<Health, Minion> spawnedMinionsByHealth = new();
        private readonly Dictionary<Minion, Transform> spawnedMinionOutlines = new();
        private readonly Dictionary<Minion, Coroutine> outlineFlashRoutines = new();

        protected override bool RotatesBodyToPlayer => false;

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

        public override EnemyProjectile FireMinionProjectile(
            Minion source,
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash)
        {
            EnemyProjectile projectile = base.FireMinionProjectile(source, settings, origin, direction, playMuzzleFlash);
            if (projectile != null)
            {
                FlashMinionOutline(source);
            }

            return projectile;
        }

        protected override void OnMinionSpawned(Minion minion)
        {
            base.OnMinionSpawned(minion);
            TrackSpawnedMinion(minion);
            TrackMinionOutline(minion);
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

        private void LateUpdate()
        {
            foreach (KeyValuePair<Minion, Transform> entry in spawnedMinionOutlines)
            {
                if (entry.Key == null
                    || entry.Value == null
                    || outlineFlashRoutines.ContainsKey(entry.Key))
                {
                    continue;
                }

                ApplyMinionOutlineIdle(entry.Value);
            }
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
            if (minionHealth == null || !spawnedMinionsByHealth.TryGetValue(minionHealth, out Minion minion))
            {
                return;
            }

            spawnedMinionsByHealth.Remove(minionHealth);
            minionHealth.Died -= HandleSpawnedMinionDied;
            UntrackMinionOutline(minion);
        }

        private void UntrackAllSpawnedMinions()
        {
            foreach (Minion minion in spawnedMinionsByHealth.Values)
            {
                UntrackMinionOutline(minion);
            }

            foreach (Health minionHealth in spawnedMinionsByHealth.Keys)
            {
                if (minionHealth != null)
                {
                    minionHealth.Died -= HandleSpawnedMinionDied;
                }
            }

            spawnedMinionsByHealth.Clear();
            spawnedMinionOutlines.Clear();
            outlineFlashRoutines.Clear();
        }

        private void TrackMinionOutline(Minion minion)
        {
            Transform outline = FindMinionOutline(minion);
            if (minion == null || outline == null)
            {
                return;
            }

            spawnedMinionOutlines[minion] = outline;
            ApplyMinionOutlineIdle(outline);
        }

        private void FlashMinionOutline(Minion minion)
        {
            if (minion == null || !spawnedMinionOutlines.TryGetValue(minion, out Transform outline) || outline == null)
            {
                return;
            }

            if (outlineFlashRoutines.TryGetValue(minion, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            outlineFlashRoutines[minion] = StartCoroutine(FlashMinionOutlineRoutine(minion, outline));
        }

        private IEnumerator FlashMinionOutlineRoutine(Minion minion, Transform outline)
        {
            outline.gameObject.SetActive(true);
            SetMinionOutlineAlpha(outline, 1f);

            float remainingSeconds = minionOutlineFlashSeconds;
            while (remainingSeconds > 0f)
            {
                remainingSeconds -= Time.deltaTime;
                yield return null;
            }

            if (outline != null)
            {
                ApplyMinionOutlineIdle(outline);
            }

            outlineFlashRoutines.Remove(minion);
        }

        private void UntrackMinionOutline(Minion minion)
        {
            if (minion == null)
            {
                return;
            }

            if (outlineFlashRoutines.TryGetValue(minion, out Coroutine routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            outlineFlashRoutines.Remove(minion);
            if (spawnedMinionOutlines.TryGetValue(minion, out Transform outline) && outline != null)
            {
                outline.gameObject.SetActive(false);
            }

            spawnedMinionOutlines.Remove(minion);
        }

        private void ApplyMinionOutlineIdle(Transform outline)
        {
            if (outline == null)
            {
                return;
            }

            outline.gameObject.SetActive(true);
            SetMinionOutlineAlpha(outline, minionOutlineIdleAlpha);
        }

        private static void SetMinionOutlineAlpha(Transform outline, float alpha)
        {
            if (outline == null)
            {
                return;
            }

            SpriteRenderer[] renderers = outline.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }
        }

        private static Transform FindMinionOutline(Minion minion)
        {
            if (minion == null)
            {
                return null;
            }

            Transform[] children = minion.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == "Outline")
                {
                    return children[i];
                }
            }

            return null;
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
