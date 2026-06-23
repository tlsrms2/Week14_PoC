using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class WaitAction : BossAction
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
    }

    [Serializable]
    public sealed class WindupAction : BossAction
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
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
