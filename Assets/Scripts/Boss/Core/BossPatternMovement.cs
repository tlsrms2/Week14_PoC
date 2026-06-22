using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class BossPatternMovement
    {
        public IEnumerator WaitSeconds(
            float seconds,
            Action onTick,
            Func<bool> isExecutionPaused,
            Action stop)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (isExecutionPaused?.Invoke() == true)
                {
                    stop?.Invoke();
                    yield return null;
                    continue;
                }

                onTick?.Invoke();
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator WaitStoppedSeconds(
            float seconds,
            Func<bool> isExecutionPaused,
            Action stop)
        {
            yield return WaitSeconds(seconds, stop, isExecutionPaused, stop);
        }

        public IEnumerator WaitWhileExecutionPaused(Func<bool> isExecutionPaused, Action stop)
        {
            while (isExecutionPaused?.Invoke() == true)
            {
                stop?.Invoke();
                yield return null;
            }
        }
    }
}
