using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionFireAllAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, Min(1)] private int shotCount = 1;
        [SerializeField, Min(0f)] private float fireInterval;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryResolveProjectile(
                context,
                projectileName,
                out IMinionPatternHost host,
                out BossProjectileSettings projectile))
            {
                yield break;
            }

            int safeShotCount = Mathf.Max(1, shotCount);
            for (int i = 0; i < safeShotCount; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                host.FireAllMinions(projectile);
                if (i < safeShotCount - 1 && fireInterval > 0f)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }
        }
    }
}
