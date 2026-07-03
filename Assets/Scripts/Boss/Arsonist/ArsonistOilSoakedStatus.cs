using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [AddComponentMenu("")]
    internal sealed class ArsonistOilSoakedStatus : MonoBehaviour
    {
        private readonly List<ArsonistOilPatch> trailPatches = new();
        private ArsonistBossAI owner;
        private PlayerCombatController player;
        private float expiresAt;
        private float trailInterval;
        private float trailMinDistance;
        private float nextTrailAt;
        private Vector3 lastTrailPosition;
        private Color oilColor;
        private bool initialized;

        public PlayerCombatController Player => player;

        public void Configure(
            ArsonistBossAI nextOwner,
            PlayerCombatController nextPlayer,
            Color nextOilColor,
            float duration,
            float nextTrailInterval,
            float nextTrailMinDistance)
        {
            owner = nextOwner;
            player = nextPlayer;
            oilColor = nextOilColor;
            expiresAt = Time.time + Mathf.Max(0.05f, duration);
            trailInterval = Mathf.Max(0.01f, nextTrailInterval);
            trailMinDistance = Mathf.Max(0.01f, nextTrailMinDistance);
            if (!initialized)
            {
                initialized = true;
                lastTrailPosition = transform.position;
                nextTrailAt = 0f;
            }
        }

        public void AddTrailPatch(ArsonistOilPatch patch)
        {
            if (patch != null && !trailPatches.Contains(patch))
            {
                trailPatches.Add(patch);
            }
        }

        public void RemoveTrailPatch(ArsonistOilPatch patch)
        {
            trailPatches.Remove(patch);
        }

        public bool ShouldIgniteFromOilPatch(ArsonistOilPatch patch, float connectionRadius)
        {
            if (patch == null || player == null)
            {
                return false;
            }

            if (trailPatches.Contains(patch))
            {
                return true;
            }

            float maxDistance = patch.Radius + Mathf.Max(0f, connectionRadius);
            return Vector2.SqrMagnitude((Vector2)player.transform.position - (Vector2)patch.transform.position)
                <= maxDistance * maxDistance;
        }

        private void Update()
        {
            if (!initialized || owner == null || player == null || player.Health == null || player.Health.IsDead)
            {
                Destroy(this);
                return;
            }

            if (Time.time >= expiresAt)
            {
                Destroy(this);
                return;
            }

            if (Time.time < nextTrailAt)
            {
                return;
            }

            if (Vector2.SqrMagnitude((Vector2)transform.position - (Vector2)lastTrailPosition)
                < trailMinDistance * trailMinDistance)
            {
                return;
            }

            ArsonistOilPatch patch = owner.CreateOilTrailPatch(transform.position, player, oilColor);
            AddTrailPatch(patch);
            lastTrailPosition = transform.position;
            nextTrailAt = Time.time + trailInterval;
        }

        private void OnDestroy()
        {
            owner?.UnregisterOilSoakedStatus(player, this);
        }
    }
}
