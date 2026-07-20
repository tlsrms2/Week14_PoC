using System;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    internal sealed class HackerParrySpawnEffectSettings
    {
        internal const float LeadSeconds = 0.4f;
        private const float PlaybackSpeed = 2f;

        [Tooltip("Parry 탄 생성 0.4초 전에 재생할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject effectPrefab;
        [Tooltip("Parry 탄 생성 위치를 기준으로 적용할 월드 X/Y 오프셋입니다.")]
        [SerializeField] private Vector2 positionOffset;
        [Tooltip("이펙트 프리팹 원본 스케일에 곱할 배율입니다.")]
        [SerializeField, Min(0.01f)] private float scale = 1f;

        internal float LeadDurationSeconds => effectPrefab != null ? LeadSeconds : 0f;

        internal bool Play(Vector3 projectilePosition)
        {
            if (effectPrefab == null)
            {
                return false;
            }

            ProjectileVfx.PlayPrefab(
                effectPrefab,
                projectilePosition + (Vector3)positionOffset,
                Quaternion.identity,
                null,
                Mathf.Max(0.01f, scale),
                false,
                PlaybackSpeed);
            return true;
        }
    }

    internal sealed class HackerPatternParryRewardTracker
    {
        private bool isActive = true;
        private int count;

        internal void Register()
        {
            if (isActive)
            {
                count++;
            }
        }

        internal int Complete(bool completed)
        {
            int result = isActive && completed ? count : 0;
            isActive = false;
            count = 0;
            return result;
        }
    }

    internal sealed class HackerParryBait : IDisposable
    {
        private ParryBaitRewardProjectile projectile;
        private readonly HackerPatternParryRewardTracker rewardTracker;

        private HackerParryBait(
            ParryBaitRewardProjectile projectile,
            HackerPatternParryRewardTracker rewardTracker)
        {
            this.projectile = projectile;
            this.rewardTracker = rewardTracker;
            projectile.HackerParried += HandleParried;
        }

        internal bool WasParried { get; private set; }

        internal static HackerParryBait Spawn(
            BossActionContext context,
            BossProjectileSettings settings,
            Vector3 position,
            Transform followTarget,
            Vector3 followWorldOffset,
            float parrySeconds,
            float attackDelaySeconds,
            bool countPatternReward = false)
        {
            if (context?.Boss == null || settings?.Prefab is not ParryBaitRewardProjectile)
            {
                return null;
            }

            Vector2 direction = context.GetDirectionToPlayer(position);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            EnemyProjectile spawned = context.FireProjectile(
                settings,
                position,
                direction,
                0f,
                aimAtPlayerWhileChargingOverride: false,
                aimAtPlayerOnLaunchOverride: false,
                chargeSecondsOverride: 0f,
                suppressHoming: true,
                useExactSettings: true);

            if (spawned is not ParryBaitRewardProjectile bait)
            {
                spawned?.DestroyFromOwner();
                return null;
            }

            bait.ConfigureHackerResilientMode(
                parrySeconds,
                attackDelaySeconds,
                followTarget,
                followWorldOffset);
            if (context.Boss is HackerHologramBoss)
            {
                bait.ConfigureParryLockOnIndicatorColor(new Color(0.04f, 0.18f, 0.75f, 1f));
            }

            HackerPatternParryRewardTracker rewardTracker = null;
            if (countPatternReward
                && context.Boss is HackerBossAI hacker
                && hacker is not HackerHologramBoss)
            {
                rewardTracker = hacker.ActivePatternParryRewardTracker;
            }

            return new HackerParryBait(bait, rewardTracker);
        }

        public void Dispose()
        {
            if (projectile != null)
            {
                projectile.HackerParried -= HandleParried;
                projectile = null;
            }
        }

        private void HandleParried()
        {
            if (WasParried)
            {
                return;
            }

            WasParried = true;
            rewardTracker?.Register();
        }
    }
}
