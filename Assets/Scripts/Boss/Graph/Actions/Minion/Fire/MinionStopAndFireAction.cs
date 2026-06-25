using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionStopAndFireAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(1)] private int bulletCount = 3;
        [SerializeField, Min(0f)] private float fireInterval = 0.2f;
        [SerializeField] private bool resumeIdle = true;
        [SerializeField] private bool waitForDuration = true;

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

            MinionGraphProjectileFireSpec fireSpec = new(minionOrigin, aim, effects, context);
            MinionGraphCommandRequest request = MinionGraphCommandRequest.StopAndFire(
                projectile,
                Mathf.Max(1, bulletCount),
                Mathf.Max(0f, fireInterval),
                fireSpec,
                resumeIdle);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
