using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionFormationBarrageAction : BossAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, Min(0)] private int minimumMinionCount = 1;
        [SerializeField, Min(0.1f)] private float formationRadius = 2.8f;
        [SerializeField, Min(1f)] private float formationAngleSpacingDegrees = 28f;
        [SerializeField, Min(0f)] private float formationSpeedMultiplier = 1.2f;
        [SerializeField, Min(0f)] private float settleSeconds = 1f;
        [SerializeField, Range(0f, 1f)] private float preFormationDelayRatio = 0.5f;
        [SerializeField, Min(1)] private int fireCount = 6;
        [SerializeField, Min(0f)] private float fireInterval = 0.22f;
        [SerializeField] private bool resumeIdle = true;

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

            if (minimumMinionCount > 0)
            {
                yield return host.EnsureMinionCount(minimumMinionCount);
            }

            float safeSettleSeconds = Mathf.Max(0f, settleSeconds);
            float preFormationDelaySeconds = safeSettleSeconds * Mathf.Clamp01(preFormationDelayRatio);
            if (preFormationDelaySeconds > 0f)
            {
                yield return context.WaitSeconds(preFormationDelaySeconds);
            }

            MinionGraphCommandRequest formationRequest = MinionGraphCommandRequest.Formation(
                formationRadius,
                formationAngleSpacingDegrees,
                formationSpeedMultiplier,
                Mathf.Max(0f, safeSettleSeconds - preFormationDelaySeconds));
            float duration = host.CommandMinions(formationRequest);
            if (duration > 0f)
            {
                yield return context.WaitSeconds(duration);
            }

            int safeFireCount = Mathf.Max(1, fireCount);
            for (int i = 0; i < safeFireCount; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                host.FireAllMinions(projectile);
                if (i < safeFireCount - 1 && fireInterval > 0f)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }

            if (resumeIdle)
            {
                host.ResumeAllMinions();
            }
        }
    }
}
