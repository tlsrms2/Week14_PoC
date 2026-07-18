using System;
using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [AddComponentMenu("Week14/Combat/Parry Bait Reward Projectile")]
    public sealed class ParryBaitRewardProjectile : EnemyProjectile
    {
        [Header("보상 투사체")]
        [SerializeField] private BossProjectileSettings rewardProjectile = new();
        [SerializeField, Min(1)] private int rewardBulletCount = 8;
        [SerializeField, Min(0.01f)] private float rewardCircleRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float rewardLifetime = 2f;

        [Header("남은 시간 게이지")]
        [SerializeField] private SpriteRenderer chargeGaugeRenderer;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");

        private int? rewardCountOverride;
        private float? rewardRadiusOverride;
        private float? rewardLifetimeOverride;
        private MaterialPropertyBlock chargeGaugePropertyBlock;
        private bool hackerResilientMode;
        private bool hackerWasParried;
        private float hackerParryDuration;
        private float hackerParryEndsAt;
        private float hackerAttackAt;
        private Transform hackerFollowTarget;
        private Vector3 hackerFollowWorldOffset;

        internal event Action HackerParried;

        protected override void OnProjectileInitialized()
        {
            hackerResilientMode = false;
            hackerWasParried = false;
            hackerFollowTarget = null;
            rewardCountOverride = null;
            rewardRadiusOverride = null;
            rewardLifetimeOverride = null;
            ConfigureParryLockOnIndicatorColor(null);
            ConfigurePlayerCollisionIgnored(true);
            ConfigureIgnoresWalls(true);
            SetChargeGaugeVisible(true);
        }

        internal void ConfigureBaitDuration(float lifetimeSeconds)
        {
            OverrideProjectileLifetime(lifetimeSeconds);
        }

        internal void ConfigureHackerResilientMode(
            float parrySeconds,
            float attackDelaySeconds,
            Transform followTarget,
            Vector3 followWorldOffset)
        {
            hackerResilientMode = true;
            hackerWasParried = false;
            hackerParryDuration = Mathf.Max(0.01f, parrySeconds);
            hackerParryEndsAt = Time.time + hackerParryDuration;
            hackerAttackAt = Time.time + Mathf.Max(hackerParryDuration, attackDelaySeconds);
            hackerFollowTarget = followTarget;
            hackerFollowWorldOffset = followWorldOffset;

            ConfigureExternalMotionDriven(true);
            ConfigurePlayerCollisionIgnored(true);
            ConfigureInterceptable(true);
            ConfigurePathIndicatorSuppressed(true);
            OverrideProjectileLifetime(hackerAttackAt - Time.time + 0.1f);
            FollowHackerAnchor();
            SetChargeGaugeVisible(true);
            SetChargeGaugeFill(1f);
        }

        internal void ConfigureRewardOverrides(int? countOverride, float? radiusOverride, float? lifetimeOverride)
        {
            rewardCountOverride = countOverride;
            rewardRadiusOverride = radiusOverride;
            rewardLifetimeOverride = lifetimeOverride;
        }

        public override bool TryDestroyByInterceptShot(out bool parried)
        {
            if (!hackerResilientMode)
            {
                return base.TryDestroyByInterceptShot(out parried);
            }

            if (!CanReceiveInterceptShot())
            {
                parried = false;
                return false;
            }

            parried = true;
            hackerWasParried = true;
            HackerParried?.Invoke();
            CompleteInterceptAndDestroy();
            return true;
        }

        protected override void OnProjectileTick()
        {
            if (IsDestroying)
            {
                return;
            }

            if (hackerResilientMode)
            {
                TickHackerResilientMode();
                return;
            }

            float remainingRatio = ProjectileLifetime > 0f
                ? Mathf.Clamp01((DestroyAt - Time.time) / ProjectileLifetime)
                : 0f;
            SetChargeGaugeFill(remainingRatio);
        }

        protected override void OnProjectileDestroying(EnemyProjectileDestroyReason reason, Vector3 position)
        {
            if (!hackerResilientMode && reason == EnemyProjectileDestroyReason.Intercepted)
            {
                FireRewardCircle(position);
            }

            HackerParried = null;
        }

        protected override void OnProjectileReturnedToPool()
        {
            HackerParried = null;
            base.OnProjectileReturnedToPool();
        }

        protected override void ExtendSpecialTimers(float pausedSeconds)
        {
            base.ExtendSpecialTimers(pausedSeconds);
            if (!hackerResilientMode || pausedSeconds <= 0f)
            {
                return;
            }

            hackerParryEndsAt += pausedSeconds;
            hackerAttackAt += pausedSeconds;
        }

        private void TickHackerResilientMode()
        {
            FollowHackerAnchor();
            if (!hackerWasParried)
            {
                float remainingRatio = Mathf.Clamp01(
                    (hackerParryEndsAt - Time.time) / hackerParryDuration);
                SetChargeGaugeFill(remainingRatio);
                if (Time.time >= hackerParryEndsAt)
                {
                    DestroyFromOwner();
                }

                return;
            }

            if (Time.time >= hackerAttackAt)
            {
                DestroyFromOwner();
            }
        }

        private void FollowHackerAnchor()
        {
            if (hackerFollowTarget != null)
            {
                transform.position = hackerFollowTarget.position + hackerFollowWorldOffset;
            }
        }

        private void SetChargeGaugeVisible(bool visible)
        {
            if (chargeGaugeRenderer != null)
            {
                chargeGaugeRenderer.enabled = visible;
            }
        }

        private void SetChargeGaugeFill(float remainingRatio)
        {
            if (chargeGaugeRenderer == null)
            {
                return;
            }

            chargeGaugePropertyBlock ??= new MaterialPropertyBlock();
            chargeGaugeRenderer.GetPropertyBlock(chargeGaugePropertyBlock);
            chargeGaugePropertyBlock.SetFloat(FillAmountId, remainingRatio);
            chargeGaugeRenderer.SetPropertyBlock(chargeGaugePropertyBlock);
        }

        private void FireRewardCircle(Vector3 center)
        {
            EnemyProjectile prefab = rewardProjectile?.Prefab;
            if (prefab == null)
            {
                return;
            }

            int count = Mathf.Max(1, rewardCountOverride ?? rewardBulletCount);
            float radius = Mathf.Max(0.01f, rewardRadiusOverride ?? rewardCircleRadius);
            float lifetime = Mathf.Max(0.01f, rewardLifetimeOverride ?? rewardLifetime);
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = AngleToDirection(step * i);
                Vector3 spawnPosition = center + (Vector3)(direction * radius);
                EnemyProjectile reward = Spawn(
                    prefab,
                    OwnerBullets,
                    spawnPosition,
                    direction,
                    rewardProjectile.BulletDamage,
                    0f,
                    rewardProjectile.Speed,
                    lifetime,
                    rewardProjectile.Radius,
                    ProjectileColor,
                    rewardProjectile.TrailSeconds,
                    rewardProjectile.TrailWidthMultiplier,
                    false,
                    0f,
                    0f);
                reward?.ConfigurePlayerCollisionIgnored(true);
                reward?.ConfigureIgnoresWalls(true);
            }
        }

        private static Vector2 AngleToDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
    }
}
