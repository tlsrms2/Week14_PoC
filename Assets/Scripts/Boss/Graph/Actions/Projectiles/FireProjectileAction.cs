using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum BossGraphAimMode
    {
        Player,
        Angle
    }

    public enum BossGraphProjectileOriginMode
    {
        BossOrigin,
        BossChild,
        BossChildList,
        AlternatingBossChildList,
        AlternatingBossChildren
    }

    public enum BossGraphProjectileAimMode
    {
        AtPlayer,
        FixedAngle,
        ClosestMinionToPlayer
    }

    [Serializable]
    public sealed class BossGraphProjectileOriginSpec
    {
        [SerializeField] private BossGraphProjectileOriginMode mode;
        [SerializeField, BossGraphBossChildPath] private string bossChildPath;
        [SerializeField, BossGraphBossChildPath] private List<string> bossChildPaths = new();
        [SerializeField, BossGraphBossChildPath] private string firstBossChildPath;
        [SerializeField, BossGraphBossChildPath] private string secondBossChildPath;
        [SerializeField, Min(0f)] private float fallbackSpacing = 0.18f;

        public Vector3 GetAimOrigin(BossActionContext context, int shotIndex)
        {
            if (context == null)
            {
                return Vector3.zero;
            }

            return mode switch
            {
                BossGraphProjectileOriginMode.BossChild => context.GetBossChildPosition(bossChildPath),
                BossGraphProjectileOriginMode.BossChildList => context.GetBossChildPosition(GetListPath(shotIndex, false)),
                BossGraphProjectileOriginMode.AlternatingBossChildList => context.GetBossChildPosition(GetListPath(shotIndex, true)),
                BossGraphProjectileOriginMode.AlternatingBossChildren => context.GetBossChildPosition(GetAlternatingPath(shotIndex)),
                _ => context.OriginPosition
            };
        }

        public Transform GetAimOriginTransform(BossActionContext context, int shotIndex)
        {
            if (context == null)
            {
                return null;
            }

            return mode switch
            {
                BossGraphProjectileOriginMode.BossChild => context.GetBossChildTransform(bossChildPath),
                BossGraphProjectileOriginMode.BossChildList => context.GetBossChildTransform(GetListPath(shotIndex, false)),
                BossGraphProjectileOriginMode.AlternatingBossChildList => context.GetBossChildTransform(GetListPath(shotIndex, true)),
                BossGraphProjectileOriginMode.AlternatingBossChildren => context.GetBossChildTransform(GetAlternatingPath(shotIndex)),
                _ => null
            };
        }

        public Vector3 GetSpawnOrigin(BossActionContext context, int shotIndex, Vector2 direction)
        {
            Vector3 origin = GetAimOrigin(context, shotIndex);
            if (mode != BossGraphProjectileOriginMode.BossOrigin || fallbackSpacing <= 0f || shotIndex <= 0)
            {
                return origin;
            }

            Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            Vector2 side = new(-normalizedDirection.y, normalizedDirection.x);
            int ring = (shotIndex + 1) / 2;
            float sign = shotIndex % 2 == 0 ? -1f : 1f;
            return origin + (Vector3)(side * ring * fallbackSpacing * sign);
        }

        private string GetAlternatingPath(int shotIndex)
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(firstBossChildPath);
            bool hasSecond = !string.IsNullOrWhiteSpace(secondBossChildPath);
            if (!hasFirst)
            {
                return secondBossChildPath;
            }

            if (!hasSecond)
            {
                return firstBossChildPath;
            }

            return shotIndex % 2 == 0 ? firstBossChildPath : secondBossChildPath;
        }

        private string GetListPath(int shotIndex, bool loop)
        {
            if (bossChildPaths == null || bossChildPaths.Count == 0)
            {
                return bossChildPath;
            }

            int index = loop
                ? Mathf.Abs(shotIndex) % bossChildPaths.Count
                : Mathf.Clamp(shotIndex, 0, bossChildPaths.Count - 1);
            return bossChildPaths[index];
        }
    }

    [Serializable]
    public sealed class BossGraphProjectileAimSpec
    {
        [SerializeField] private BossGraphProjectileAimMode mode;
        [SerializeField] private float angleDegrees;

        public BossGraphProjectileAimMode Mode => mode;

        public Vector2 GetDirection(BossActionContext context, Vector3 origin)
        {
            return GetDirection(context != null ? context.GetDirectionToPlayer : null, origin);
        }

        public Vector2 GetDirection(Func<Vector3, Vector2> getDirectionToPlayer, Vector3 origin)
        {
            if (mode == BossGraphProjectileAimMode.FixedAngle)
            {
                return BossActionContext.AngleToDirection(angleDegrees);
            }

            if (getDirectionToPlayer == null)
            {
                return Vector2.left;
            }

            return getDirectionToPlayer(origin);
        }
    }

    [Serializable]
    public sealed class FireProjectileAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            ResolveShot(context, originSpec, aimSpec, 0, out Vector3 spawnOrigin, out Vector2 finalDirection);

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                finalDirection,
                0f,
                projectileName: projectileName);
            if (firedProjectile != null)
            {
                context.PlaySfx(fireSfxId);
                context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                context.PlayOriginBurst(effects, spawnOrigin);
                context.PlayMuzzleFlashIfEnabled(effects, spawnOrigin, finalDirection);
                context.PlayCameraShakeIfEnabled(effects, finalDirection);
            }

            yield break;
        }

        private void ResolveShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int shotIndex,
            out Vector3 spawnOrigin,
            out Vector2 finalDirection)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, shotIndex);
            Vector2 direction = aimSpec.GetDirection(context, aimOrigin);
            spawnOrigin = originSpec.GetSpawnOrigin(context, shotIndex, direction);
            finalDirection = aimSpec.GetDirection(context, spawnOrigin);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(finalDirection.normalized * spawnForwardOffset);
            }
        }
    }

    [Serializable]
    public sealed class FireProjectileBurstAction : BossAction
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, Min(1)] private int bulletCount = 4;
            [SerializeField, Min(0f)] private float fireInterval = 0.12f;
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;

            public int BulletCount => Mathf.Max(1, bulletCount);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [SerializeField, Min(0f)] private float startDelaySeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField] private List<Volley> volleys = new() { new Volley() };

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || volleys == null || volleys.Count == 0)
            {
                yield break;
            }

            if (startDelaySeconds > 0f)
            {
                yield return context.WaitSeconds(startDelaySeconds);
            }

            int shotIndex = 0;
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                for (int bulletIndex = 0; bulletIndex < volley.BulletCount; bulletIndex++)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        bulletIndex--;
                        continue;
                    }

                    FireShot(context, shotIndex);
                    shotIndex++;

                    if (bulletIndex < volley.BulletCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return context.WaitSeconds(volley.FireInterval);
                    }
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        private void FireShot(BossActionContext context, int shotIndex)
        {
            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            ResolveShot(context, originSpec, aimSpec, shotIndex, out Vector3 spawnOrigin, out Vector2 finalDirection);

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                finalDirection,
                0f,
                projectileName: projectileName);

            if (firedProjectile == null)
            {
                return;
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            context.PlayMuzzleFlashIfEnabled(effects, spawnOrigin, finalDirection);
            context.PlayCameraShakeIfEnabled(effects, finalDirection);
        }

        private void ResolveShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            int shotIndex,
            out Vector3 spawnOrigin,
            out Vector2 finalDirection)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, shotIndex);
            Vector2 direction = aimSpec.GetDirection(context, aimOrigin);
            spawnOrigin = originSpec.GetSpawnOrigin(context, shotIndex, direction);
            finalDirection = aimSpec.GetDirection(context, spawnOrigin);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(finalDirection.normalized * spawnForwardOffset);
            }
        }
    }
}
