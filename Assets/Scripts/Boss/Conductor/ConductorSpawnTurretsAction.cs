using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum ConductorTurretSpawnAreaMode
    {
        GroundBounds,
        AroundPlayer,
        AroundBoss,
        WorldCenter
    }

    [Serializable]
    public sealed class ConductorSpawnTurretsAction : BossAction, IBossActionContextDurationProvider, IBossProjectileEmissionAction
    {
        private const string GroundLayerName = "Ground";

        [Header("Projectile")]
        [SerializeField, BossGraphProjectileName] private string turretProjectileName = "Turret";
        [SerializeField, HideInInspector] private BossProjectileSettings turretProjectile = new();

        [Header("Spawn")]
        [SerializeField] private ConductorTurretSpawnAreaMode spawnAreaMode = ConductorTurretSpawnAreaMode.GroundBounds;
        [SerializeField, Min(1)] private int turretCount = 4;
        [SerializeField, Min(0)] private int maxTurretsOnMap = 4;
        [SerializeField, Min(0.1f)] private float spawnRadius = 7f;
        [SerializeField] private Vector2 worldCenter;
        [SerializeField] private Vector2 centerOffset;
        [SerializeField, Min(0f)] private float minimumSpacing = 1.75f;
        [SerializeField, Min(0.01f)] private float groundProbeRadius = 0.16f;
        [SerializeField, Min(1)] private int maxAttemptsPerTurret = 80;
        [SerializeField, Min(0f)] private float spawnInterval;

        [Header("Turret Fire")]
        [SerializeField, Min(0f)] private float deploySeconds = 0.55f;
        [SerializeField, Min(0f)] private float firstFireDelay = 0.35f;
        [SerializeField, Min(0.05f)] private float fireInterval = 1.25f;
        [SerializeField, Min(0)] private int fireCycles;
        [SerializeField] private float angleOffsetDegrees;
        [SerializeField, Min(0f)] private float muzzleOffset = 0.18f;
        [SerializeField, Min(0f)] private float muzzleFlashScale = 0.55f;
        [SerializeField, Min(0f)] private float turretLifetimeSeconds;

        public bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            seconds = 0f;
            if (context?.Boss == null)
            {
                return false;
            }

            BossProjectileSettings resolvedTurret = context.ResolveGraphProjectileSettings(turretProjectileName)
                ?? turretProjectile;
            if (resolvedTurret == null || resolvedTurret.Prefab == null)
            {
                return false;
            }

            int spawnCount = Mathf.Min(Mathf.Max(1, turretCount), GetAvailableTurretSlots());
            if (spawnCount <= 0)
            {
                return false;
            }

            seconds = GetTurretSpawnSequenceSeconds(spawnCount);
            return true;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null)
            {
                yield break;
            }

            BossProjectileSettings resolvedTurret = context.ResolveGraphProjectileSettings(turretProjectileName)
                ?? turretProjectile;
            if (resolvedTurret == null || resolvedTurret.Prefab == null)
            {
                yield break;
            }

            int spawnCount = Mathf.Min(Mathf.Max(1, turretCount), GetAvailableTurretSlots());
            if (spawnCount <= 0)
            {
                yield break;
            }

            List<Vector2> positions = ResolveSpawnPositions(context, spawnCount);
            for (int i = 0; i < positions.Count; i++)
            {
                bool spawned = SpawnTurret(context, resolvedTurret, positions[i]);

                if (spawned && spawnInterval > 0f && i + 1 < positions.Count)
                {
                    yield return context.WaitSeconds(spawnInterval);
                }
            }
        }

        private bool SpawnTurret(
            BossActionContext context,
            BossProjectileSettings resolvedTurret,
            Vector2 position)
        {
            Vector3 spawnOrigin = context.OriginPosition;
            EnemyProjectile spawned = context.Boss.FireGraphProjectile(
                resolvedTurret,
                spawnOrigin,
                Vector2.right,
                0f,
                aimAtPlayerWhileChargingOverride: false,
                aimAtPlayerOnLaunchOverride: false,
                chargeSecondsOverride: 0f,
                suppressHoming: true);
            if (spawned is not ConductorTurretProjectile turret)
            {
                if (spawned != null)
                {
                    spawned.DestroyFromOwner();
                }

                return false;
            }

            turret.ConfigureTurret(
                context.Boss,
                position,
                deploySeconds,
                firstFireDelay,
                fireInterval,
                fireCycles,
                angleOffsetDegrees,
                muzzleOffset,
                muzzleFlashScale,
                turretLifetimeSeconds);
            return true;
        }

        private float GetTurretSpawnSequenceSeconds(int spawnCount)
        {
            return Mathf.Max(0f, spawnInterval) * Mathf.Max(0, spawnCount - 1);
        }

        private List<Vector2> ResolveSpawnPositions(BossActionContext context, int count)
        {
            List<Vector2> positions = new(count);
            Bounds groundBounds = GetGroundBounds();
            bool canUseGroundBounds = spawnAreaMode == ConductorTurretSpawnAreaMode.GroundBounds
                && groundBounds.size.x > 0.01f
                && groundBounds.size.y > 0.01f;
            Vector2 center = ResolveCenter(context);
            int totalAttempts = Mathf.Max(1, maxAttemptsPerTurret) * count;

            for (int attempt = 0; attempt < totalAttempts && positions.Count < count; attempt++)
            {
                Vector2 candidate = canUseGroundBounds
                    ? RandomPointInBounds(groundBounds)
                    : center + UnityEngine.Random.insideUnitCircle * Mathf.Max(0.1f, spawnRadius);
                if (!IsGroundPosition(candidate) || IsTooCloseToExisting(candidate, positions))
                {
                    continue;
                }

                positions.Add(candidate);
            }

            return positions;
        }

        private int GetAvailableTurretSlots()
        {
            return Mathf.Max(0, maxTurretsOnMap - CountActiveTurretsOnMap());
        }

        private static int CountActiveTurretsOnMap()
        {
            ConductorTurretProjectile[] turrets =
                UnityEngine.Object.FindObjectsByType<ConductorTurretProjectile>(FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i] != null && turrets[i].IsAliveTurret)
                {
                    count++;
                }
            }

            return count;
        }

        private Vector2 ResolveCenter(BossActionContext context)
        {
            Vector2 center = spawnAreaMode switch
            {
                ConductorTurretSpawnAreaMode.AroundPlayer when context.Boss.Player != null => context.Boss.Player.position,
                ConductorTurretSpawnAreaMode.AroundBoss => context.OriginPosition,
                ConductorTurretSpawnAreaMode.WorldCenter => worldCenter,
                _ => context.Boss.Player != null ? context.Boss.Player.position : context.OriginPosition
            };

            return center + centerOffset;
        }

        private bool IsTooCloseToExisting(Vector2 candidate, IReadOnlyList<Vector2> positions)
        {
            float minSqrDistance = minimumSpacing * minimumSpacing;
            for (int i = 0; i < positions.Count; i++)
            {
                if ((positions[i] - candidate).sqrMagnitude < minSqrDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGroundPosition(Vector2 position)
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                return true;
            }

            int groundMask = 1 << groundLayer;
            if (Physics2D.OverlapCircle(position, groundProbeRadius, groundMask) != null)
            {
                return true;
            }

            Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null || !tilemap.isActiveAndEnabled || tilemap.gameObject.layer != groundLayer)
                {
                    continue;
                }

                if (tilemap.HasTile(tilemap.WorldToCell(position)))
                {
                    return true;
                }
            }

            return false;
        }

        private static Bounds GetGroundBounds()
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer < 0)
            {
                return default;
            }

            Tilemap[] tilemaps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            Bounds bounds = default;
            bool found = false;
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap == null || !tilemap.isActiveAndEnabled || tilemap.gameObject.layer != groundLayer)
                {
                    continue;
                }

                Bounds nextBounds = tilemap.localBounds;
                nextBounds.center = tilemap.transform.TransformPoint(nextBounds.center);
                if (!found)
                {
                    bounds = nextBounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(nextBounds);
                }
            }

            return found ? bounds : default;
        }

        private static Vector2 RandomPointInBounds(Bounds bounds)
        {
            return new Vector2(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x),
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y));
        }
    }
}
