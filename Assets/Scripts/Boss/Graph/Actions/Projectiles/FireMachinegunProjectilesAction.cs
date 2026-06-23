using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireMachinegunProjectilesAction : BossAction
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
        [SerializeField, BossGraphBossChildPath] private string firstProjectileOriginPath;
        [SerializeField, BossGraphBossChildPath] private string secondProjectileOriginPath;
        [SerializeField, Min(0f)] private float moveSpeedMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float spawnSpacing = 0.18f;
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

            int fired = 0;
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

                    context.MoveTowardPlayer(moveSpeedMultiplier);
                    EnemyProjectile projectile = FireBullet(context, fired);
                    if (projectile != null)
                    {
                        context.PlaySfx(fireSfxId);
                        context.PlaySfxOnLaunch(projectile, launchSfxId);
                    }

                    fired++;

                    if (bulletIndex < volley.BulletCount - 1 && volley.FireInterval > 0f)
                    {
                        yield return WaitMoving(context, volley.FireInterval);
                    }
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return WaitMoving(context, volley.RestSeconds);
                }
            }

            context.Stop();
        }

        private EnemyProjectile FireBullet(BossActionContext context, int bulletIndex)
        {
            bool hasConfiguredOrigin = HasConfiguredOrigin();
            Vector3 origin = GetOrigin(context, bulletIndex, hasConfiguredOrigin);
            Vector2 direction = context.GetDirectionToPlayer(origin);
            Vector3 spawnPosition = origin;

            if (!hasConfiguredOrigin)
            {
                Vector2 side = new(-direction.y, direction.x);
                spawnPosition += (Vector3)(side * GetAlternatingOffset(bulletIndex));
            }

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnPosition,
                direction,
                0f,
                projectileName: projectileName);
            if (firedProjectile != null)
            {
                context.PlayOriginBurst(effects, origin);
            }

            return firedProjectile;
        }

        private IEnumerator WaitMoving(BossActionContext context, float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                context.MoveTowardPlayer(moveSpeedMultiplier);
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private Vector3 GetOrigin(BossActionContext context, int bulletIndex, bool hasConfiguredOrigin)
        {
            if (!hasConfiguredOrigin)
            {
                return context.OriginPosition;
            }

            string path = GetAlternatingOriginPath(bulletIndex);
            return context.GetBossChildPosition(path);
        }

        private string GetAlternatingOriginPath(int bulletIndex)
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(firstProjectileOriginPath);
            bool hasSecond = !string.IsNullOrWhiteSpace(secondProjectileOriginPath);
            if (!hasFirst)
            {
                return secondProjectileOriginPath;
            }

            if (!hasSecond)
            {
                return firstProjectileOriginPath;
            }

            return bulletIndex % 2 == 0 ? firstProjectileOriginPath : secondProjectileOriginPath;
        }

        private bool HasConfiguredOrigin()
        {
            return !string.IsNullOrWhiteSpace(firstProjectileOriginPath)
                || !string.IsNullOrWhiteSpace(secondProjectileOriginPath);
        }

        private float GetAlternatingOffset(int index)
        {
            if (index <= 0 || spawnSpacing <= 0f)
            {
                return 0f;
            }

            int ring = (index + 1) / 2;
            float sign = index % 2 == 0 ? -1f : 1f;
            return ring * spawnSpacing * sign;
        }
    }
}
