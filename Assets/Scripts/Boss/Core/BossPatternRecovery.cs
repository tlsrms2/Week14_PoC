using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class BossPatternRecovery
    {
        public float GetRecoverySeconds(float minPatternRecoverySeconds, float maxPatternRecoverySeconds)
        {
            float min = Mathf.Max(0f, minPatternRecoverySeconds);
            float max = Mathf.Max(min, maxPatternRecoverySeconds);
            return UnityEngine.Random.Range(min, max);
        }

        public IEnumerator RunRecovery(
            float duration,
            Action<float, float> onTick,
            Func<bool> isExecutionPaused,
            Action stop)
        {
            float recoveryDuration = Mathf.Max(0f, duration);
            float remaining = recoveryDuration;
            while (remaining > 0f)
            {
                if (isExecutionPaused?.Invoke() == true)
                {
                    stop?.Invoke();
                    yield return null;
                    continue;
                }

                onTick?.Invoke(recoveryDuration, remaining);
                remaining -= Time.deltaTime;
                yield return null;
            }
        }
    }
}
