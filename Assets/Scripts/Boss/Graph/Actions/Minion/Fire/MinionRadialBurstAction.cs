using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionRadialBurstAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Min(1)] private int volleyCount = 1;
        [SerializeField, Min(1)] private int directionCount = 5;
        [SerializeField, Min(0f)] private float volleyInterval = 0.35f;
        [SerializeField, Range(0f, 360f)] private float spreadDegrees = 75f;
        [SerializeField, HideInInspector] private bool resumeIdle = true;
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
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);
            MinionGraphCommandRequest request = MinionGraphCommandRequest.RadialBurst(
                projectile,
                Mathf.Max(1, volleyCount),
                Mathf.Max(1, directionCount),
                Mathf.Max(0f, volleyInterval),
                spreadDegrees,
                fireSpec,
                resumeIdle);
            float duration = host.CommandMinions(request);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, duration, waitForDuration);
        }
    }
}
