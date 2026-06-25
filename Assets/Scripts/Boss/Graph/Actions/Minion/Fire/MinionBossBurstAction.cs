using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionBossBurstAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string bossProjectileName = "Default";
        [SerializeField, BossGraphProjectileName] private string minionProjectileName = "Default";
        [SerializeField, Min(0f)] private float windupSeconds = 0.45f;
        [SerializeField, Min(1)] private int bulletCount = 5;
        [SerializeField, Min(0f)] private float fireInterval = 0.18f;
        [SerializeField, Min(0f)] private float spawnSpacing = 0.12f;
        [SerializeField] private bool notifyMinions;

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

            BossProjectileSettings minionProjectile = null;
            if (notifyMinions)
            {
                MinionGraphActionHost.TryResolveProjectile(host, minionProjectileName, out minionProjectile);
            }

            MinionGraphBossBurstRequest request = new(
                bossProjectile,
                bulletCount,
                fireInterval,
                spawnSpacing,
                windupSeconds,
                notifyMinions,
                minionProjectile);

            yield return host.FireBossBurst(request);
        }
    }
}
