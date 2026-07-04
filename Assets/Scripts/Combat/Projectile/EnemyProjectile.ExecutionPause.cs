using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private void PauseForExecution()
        {
            if (!pausedByExecution)
            {
                pausedByExecution = true;
                executionPauseStartedAt = Time.time;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void ResumeFromExecutionPause()
        {
            if (!pausedByExecution)
            {
                return;
            }

            float pausedSeconds = Mathf.Max(0f, Time.time - executionPauseStartedAt);
            chargeEndsAt += pausedSeconds;
            destroyAt += pausedSeconds;
            ExtendSpecialTimers(pausedSeconds);
            if (radialSplitAt > 0f)
            {
                radialSplitAt += pausedSeconds;
            }

            if (pathIndicatorEndsAt > 0f)
            {
                pathIndicatorEndsAt += pausedSeconds;
            }

            pausedByExecution = false;
            executionPauseStartedAt = 0f;
            if (launched && body != null)
            {
                body.linearVelocity = flightDirection * projectileSpeed * EnemyTimeScale.Current;
            }
        }

    }
}
