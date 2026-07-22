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
        // 해커 저항 모드는 "진짜 시간 초과로 놓침"과 "보스 처형/사망 등 외부 강제 정리"가 둘 다
        // DestroyFromOwner()(reason = OwnerDestroyed)를 거쳐서 reason만으로는 구분이 안 된다.
        // 그래서 TickHackerResilientMode에서 시간 초과가 실제로 발생한 순간에만 이 플래그를 켠다.
        private bool hackerParryWindowExpired;
        private float hackerParryDuration;
        private float hackerParryEndsAt;
        private float hackerAttackAt;
        private Transform hackerFollowTarget;
        private Vector3 hackerFollowWorldOffset;

        internal event Action HackerParried;

        // 이 미끼는 요격(패링)만 성립해야 하고, 야구방망이가 "보스 쪽으로 반사"해버리면 억제/보상 판정이
        // 전부 Intercepted reason에 걸려 있어서 무효가 됩니다. 항상 요격 경로(TryDestroyByInterceptShot)로만
        // 처리되도록 반사 대상에서 제외합니다.
        protected override bool PreventsReflectionWhileInterceptable => true;

        // 이 미끼가 패링당하지 못하고 사라졌을 때(수명 만료 등) 발생하는 전역 이벤트입니다. 챌린지처럼
        // "이 미끼를 놓치면 실패" 같은 조건을 스폰 시점을 몰라도 구독 한 번으로 감지하고 싶을 때 씁니다.
        internal static event Action<ParryBaitRewardProjectile> AnyParryFailed;

        protected override void OnProjectileInitialized()
        {
            hackerResilientMode = false;
            hackerWasParried = false;
            hackerParryWindowExpired = false;
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
            hackerParryWindowExpired = false;
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
            bool wasParried = hackerResilientMode
                ? hackerWasParried
                : reason == EnemyProjectileDestroyReason.Intercepted;

            if (!hackerResilientMode && wasParried)
            {
                FireRewardCircle(position);
            }

            // 처형 시작(SetExecutionLocked) 등 보스 쪽 강제 정리는 EnemyProjectile.DestroyFromOwner()를
            // 거치면서 hackerResilientMode 여부와 상관없이 항상 OwnerDestroyed로 나온다. 플레이어가
            // 정말로 놓친 경우만 실패로 잡아야 하므로, 일반 모드는 자연 수명 만료(Expired)일 때만,
            // 해커 저항 모드는 TickHackerResilientMode에서 실제로 시간 초과가 발생했을 때만 실패로 친다.
            bool genuinelyFailedToParry = hackerResilientMode
                ? !wasParried && hackerParryWindowExpired
                : reason == EnemyProjectileDestroyReason.Expired;

            if (genuinelyFailedToParry)
            {
                AnyParryFailed?.Invoke(this);
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
                    hackerParryWindowExpired = true;
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
