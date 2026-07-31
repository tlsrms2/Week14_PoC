using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class WaitAction : BossAction, IBossActionDurationProvider
    {
        [SerializeField, Min(0f)] private float seconds = 1f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return context.WaitSeconds(seconds);
        }

        public bool TryGetDurationSeconds(out float durationSeconds)
        {
            durationSeconds = Mathf.Max(0f, seconds);
            return durationSeconds > 0f;
        }
    }

    [Serializable]
    public sealed class WindupAction : BossAction, IBossActionDurationProvider
    {
        [SerializeField, Min(0f)] private float seconds = 1f;
        [SerializeField] private bool stopMovement = true;
        [SerializeField] private BossGraphProjectileOriginSpec effectOrigin = new();
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (context.Boss is HogBossAI
                && context.IsCurrentPattern("Pattern5")
                && string.Equals(context.CurrentNodeId, "Windup1", StringComparison.OrdinalIgnoreCase))
            {
                context.PlaySfx(GameplaySfxIds.HogMachinegunCharge);
            }

            BossGraphProjectileOriginSpec originSpec = effectOrigin ?? new BossGraphProjectileOriginSpec();
            float elapsed = 0f;
            float nextSmokeAt = Time.time;
            while (elapsed < seconds)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                if (stopMovement)
                {
                    context.Stop();
                }

                Vector3 origin = originSpec.GetAimOrigin(context, 0);
                context.PlaySmokeIfDue(ref nextSmokeAt, effects, origin);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        public bool TryGetDurationSeconds(out float durationSeconds)
        {
            durationSeconds = Mathf.Max(0f, seconds);
            return durationSeconds > 0f;
        }
    }
}
