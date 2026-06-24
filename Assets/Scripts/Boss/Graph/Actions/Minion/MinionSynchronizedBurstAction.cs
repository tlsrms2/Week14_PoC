using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionSynchronizedBurstAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string bossProjectileName = "Default";
        [SerializeField, BossGraphProjectileName] private string minionProjectileName = "Default";
        [SerializeField] private bool useBossProjectileForMinions = true;
        [SerializeField, Min(0)] private int ensureMinionCount = 1;
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(1)] private int bulletCount = 5;
        [SerializeField, Min(0f)] private float fireInterval = 0.18f;
        [SerializeField, Min(0f)] private float spawnSpacing = 0.12f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryResolveProjectile(
                context,
                bossProjectileName,
                out IMinionPatternHost host,
                out BossProjectileSettings bossProjectile))
            {
                yield break;
            }

            BossProjectileSettings minionProjectile = bossProjectile;
            if (!useBossProjectileForMinions
                && !MinionGraphActionHost.TryResolveProjectile(host, minionProjectileName, out minionProjectile))
            {
                yield break;
            }

            if (ensureMinionCount > 0)
            {
                yield return host.EnsureMinionCount(ensureMinionCount);
            }

            int syncVersion = host.BeginSynchronizedMinionFire(minionProjectile, bulletCount);
            MinionGraphBossBurstRequest request = new(
                bossProjectile,
                bulletCount,
                fireInterval,
                spawnSpacing,
                windupSeconds,
                false,
                null);

            yield return host.FireBossBurst(request);
            yield return host.WaitSynchronizedMinionFire(syncVersion);
        }
    }
}
