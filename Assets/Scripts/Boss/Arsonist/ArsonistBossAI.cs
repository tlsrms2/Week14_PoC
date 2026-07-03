using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist Boss")]
    public sealed class ArsonistBossAI : GraphBossAI
    {
        private const string BgmId = "ArsonistBgm";

        [SerializeField, Min(0.05f)] private float oilTrailDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float oilTrailRadius = 0.28f;
        [SerializeField, Min(0.05f)] private float ignitedOilDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float oilConnectionRadius = 0.95f;
        [SerializeField] private Color oilColor = new(0.12f, 0.09f, 0.04f, 0.75f);

        [SerializeField, Min(0.05f)] private float oilSoakedDuration = 4f;
        [SerializeField, Min(0.01f)] private float oilTrailInterval = 0.18f;
        [SerializeField, Min(0.01f)] private float oilTrailMinDistance = 0.22f;

        [SerializeField, Min(1)] private int fireDamage = 1;
        [SerializeField, Min(0.05f)] private float fireDamageInterval = 0.45f;
        [SerializeField] private Color fireColor = new(1f, 0.35f, 0.05f, 0.9f);

        [SerializeField, Min(1)] private int burnDamage = 1;
        [SerializeField, Min(0.05f)] private float burnTickInterval = 0.75f;
        [SerializeField, Min(0.05f)] private float burnDuration = 3f;

        private readonly List<ArsonistOilPatch> oilPatches = new();
        private readonly List<ArsonistFireArea> fireAreas = new();
        private readonly Dictionary<PlayerCombatController, ArsonistOilSoakedStatus> oilSoakedStatuses = new();
        private readonly List<ArsonistOilSoakedStatus> statusBuffer = new();

        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm(BgmId);
        }

        protected override void OnBossDied()
        {
            ClearArsonistHazards();
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            ClearArsonistHazards();
            base.OnDisable();
        }

        internal ArsonistOilPatch CreateOilPatch(Vector3 position, float radius, float duration)
        {
            return CreateOilPatch(position, radius, duration, null);
        }

        internal ArsonistOilPatch CreateOilTrailPatch(Vector3 position, PlayerCombatController sourcePlayer)
        {
            return CreateOilPatch(position, oilTrailRadius, oilTrailDuration, sourcePlayer);
        }

        internal ArsonistFireArea CreateFireArea(Vector3 position, float radius, float duration)
        {
            GameObject fireObject = new("ArsonistFireArea");
            fireObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = fireObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistFireArea fireArea = fireObject.AddComponent<ArsonistFireArea>();
            fireAreas.Add(fireArea);
            fireArea.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), fireColor);
            return fireArea;
        }

        internal void ApplyOilSoaked(PlayerCombatController player)
        {
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            if (!oilSoakedStatuses.TryGetValue(player, out ArsonistOilSoakedStatus status) || status == null)
            {
                status = player.gameObject.GetComponent<ArsonistOilSoakedStatus>();
                if (status == null)
                {
                    status = player.gameObject.AddComponent<ArsonistOilSoakedStatus>();
                }

                oilSoakedStatuses[player] = status;
            }

            status.Configure(this, player, oilSoakedDuration, oilTrailInterval, oilTrailMinDistance);
        }

        internal void ApplyFireContact(
            PlayerCombatController player,
            Vector3 sourcePosition,
            Dictionary<PlayerCombatController, float> nextDamageAtByPlayer)
        {
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            float now = Time.time;
            if (nextDamageAtByPlayer != null
                && nextDamageAtByPlayer.TryGetValue(player, out float nextDamageAt)
                && now < nextDamageAt)
            {
                return;
            }

            Vector2 direction = (Vector2)(player.transform.position - sourcePosition);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.up;
            }

            if (player.ReceiveAttack(fireDamage, sourcePosition, direction.normalized))
            {
                ApplyBurn(player);
            }

            if (nextDamageAtByPlayer != null)
            {
                nextDamageAtByPlayer[player] = now + fireDamageInterval;
            }
        }

        internal void ApplyBurn(PlayerCombatController player)
        {
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            ArsonistBurnStatus burn = player.gameObject.GetComponent<ArsonistBurnStatus>();
            if (burn == null)
            {
                burn = player.gameObject.AddComponent<ArsonistBurnStatus>();
            }

            burn.Configure(player, burnDamage, burnTickInterval, burnDuration);
        }

        internal void IgniteOilNetwork(ArsonistOilPatch seed)
        {
            if (seed == null || !seed.CanIgnite)
            {
                return;
            }

            Queue<ArsonistOilPatch> queue = new();
            HashSet<ArsonistOilPatch> visited = new();
            queue.Enqueue(seed);
            visited.Add(seed);

            while (queue.Count > 0)
            {
                ArsonistOilPatch current = queue.Dequeue();
                if (current == null || !current.CanIgnite)
                {
                    continue;
                }

                current.IgniteLocal(ignitedOilDuration, fireColor);
                NotifyOilPatchIgnited(current);

                for (int i = 0; i < oilPatches.Count; i++)
                {
                    ArsonistOilPatch other = oilPatches[i];
                    if (other == null || !other.CanIgnite || visited.Contains(other))
                    {
                        continue;
                    }

                    if (!AreOilPatchesConnected(current, other))
                    {
                        continue;
                    }

                    visited.Add(other);
                    queue.Enqueue(other);
                }
            }
        }

        internal void TryIgniteOilAt(Vector3 position, float radius)
        {
            float igniteRadius = Mathf.Max(0f, radius) + oilConnectionRadius * 0.5f;
            for (int i = 0; i < oilPatches.Count; i++)
            {
                ArsonistOilPatch patch = oilPatches[i];
                if (patch == null || !patch.CanIgnite)
                {
                    continue;
                }

                float maxDistance = igniteRadius + patch.Radius;
                if (Vector2.SqrMagnitude((Vector2)patch.transform.position - (Vector2)position) <= maxDistance * maxDistance)
                {
                    IgniteOilNetwork(patch);
                }
            }
        }

        internal void UnregisterOilPatch(ArsonistOilPatch patch)
        {
            oilPatches.Remove(patch);
            foreach (ArsonistOilSoakedStatus status in oilSoakedStatuses.Values)
            {
                status?.RemoveTrailPatch(patch);
            }
        }

        internal void UnregisterFireArea(ArsonistFireArea fireArea)
        {
            fireAreas.Remove(fireArea);
        }

        internal void UnregisterOilSoakedStatus(PlayerCombatController player, ArsonistOilSoakedStatus status)
        {
            if (player == null)
            {
                return;
            }

            if (oilSoakedStatuses.TryGetValue(player, out ArsonistOilSoakedStatus current) && current == status)
            {
                oilSoakedStatuses.Remove(player);
            }
        }

        private ArsonistOilPatch CreateOilPatch(
            Vector3 position,
            float radius,
            float duration,
            PlayerCombatController sourcePlayer)
        {
            GameObject patchObject = new("ArsonistOilPatch");
            patchObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = patchObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistOilPatch patch = patchObject.AddComponent<ArsonistOilPatch>();
            patch.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), oilColor, fireColor);
            oilPatches.Add(patch);

            if (sourcePlayer != null
                && oilSoakedStatuses.TryGetValue(sourcePlayer, out ArsonistOilSoakedStatus status)
                && status != null)
            {
                status.AddTrailPatch(patch);
            }

            TryIgniteNewOilPatchFromActiveFire(patch);
            return patch;
        }

        private void TryIgniteNewOilPatchFromActiveFire(ArsonistOilPatch patch)
        {
            if (patch == null || !patch.CanIgnite)
            {
                return;
            }

            for (int i = 0; i < fireAreas.Count; i++)
            {
                ArsonistFireArea fireArea = fireAreas[i];
                if (fireArea != null && fireArea.CanIgniteOilAt(patch.transform.position, patch.Radius))
                {
                    IgniteOilNetwork(patch);
                    return;
                }
            }

            for (int i = 0; i < oilPatches.Count; i++)
            {
                ArsonistOilPatch other = oilPatches[i];
                if (other != null && other.IsIgnited && AreOilPatchesConnected(patch, other))
                {
                    IgniteOilNetwork(patch);
                    return;
                }
            }
        }

        private void NotifyOilPatchIgnited(ArsonistOilPatch patch)
        {
            statusBuffer.Clear();
            foreach (ArsonistOilSoakedStatus status in oilSoakedStatuses.Values)
            {
                if (status != null)
                {
                    statusBuffer.Add(status);
                }
            }

            for (int i = 0; i < statusBuffer.Count; i++)
            {
                ArsonistOilSoakedStatus status = statusBuffer[i];
                if (status != null && status.ShouldIgniteFromOilPatch(patch, oilConnectionRadius))
                {
                    ApplyBurn(status.Player);
                }
            }

            statusBuffer.Clear();
        }

        private bool AreOilPatchesConnected(ArsonistOilPatch first, ArsonistOilPatch second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            float maxDistance = first.Radius + second.Radius + oilConnectionRadius;
            return Vector2.SqrMagnitude((Vector2)first.transform.position - (Vector2)second.transform.position)
                <= maxDistance * maxDistance;
        }

        private void ClearArsonistHazards()
        {
            for (int i = oilPatches.Count - 1; i >= 0; i--)
            {
                if (oilPatches[i] != null)
                {
                    Destroy(oilPatches[i].gameObject);
                }
            }

            for (int i = fireAreas.Count - 1; i >= 0; i--)
            {
                if (fireAreas[i] != null)
                {
                    Destroy(fireAreas[i].gameObject);
                }
            }

            foreach (ArsonistOilSoakedStatus status in oilSoakedStatuses.Values)
            {
                if (status != null)
                {
                    Destroy(status);
                }
            }

            oilPatches.Clear();
            fireAreas.Clear();
            oilSoakedStatuses.Clear();
        }

        private static Vector3 FlattenPosition(Vector3 position)
        {
            position.z = 0f;
            return position;
        }
    }
}
