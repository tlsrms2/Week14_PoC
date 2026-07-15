using System;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class HackerParryBait : IDisposable
    {
        private ParryBaitRewardProjectile projectile;

        private HackerParryBait(ParryBaitRewardProjectile projectile)
        {
            this.projectile = projectile;
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
            float reassembleSeconds)
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
                reassembleSeconds,
                followTarget,
                followWorldOffset);
            if (context.Boss is HackerHologramBoss)
            {
                bait.ConfigureParryLockOnIndicatorColor(new Color(0.04f, 0.18f, 0.75f, 1f));
            }
            return new HackerParryBait(bait);
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
            WasParried = true;
        }
    }
}
