using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class HogPatternRecoveryController
    {
        public float GetRecoverySeconds(float minPatternRecoverySeconds, float maxPatternRecoverySeconds)
        {
            float min = Mathf.Max(0f, minPatternRecoverySeconds);
            float max = Mathf.Max(min, maxPatternRecoverySeconds);
            return UnityEngine.Random.Range(min, max);
        }

        public IEnumerator RunRecovery(
            HogBossAI.PatternKind nextPattern,
            float duration,
            HogBossAI.FirePoint pattern5FirePoint,
            HogBossAI.FirePoint pattern7FirePoint,
            Action<HogBossAI.PatternKind, float> beginPreview,
            Action<float, float> updatePreview,
            Action<HogBossAI.FirePoint, bool> setFirePointActive,
            Action<HogBossAI.FirePoint> rotateFirePointToPlayer,
            Func<bool> isExecutionPaused,
            Action stop)
        {
            float remaining = duration;
            beginPreview?.Invoke(nextPattern, duration);
            BeginTelegraph(nextPattern, pattern5FirePoint, pattern7FirePoint, setFirePointActive, rotateFirePointToPlayer);

            while (remaining > 0f)
            {
                if (isExecutionPaused?.Invoke() == true)
                {
                    stop?.Invoke();
                    yield return null;
                    continue;
                }

                updatePreview?.Invoke(duration, remaining);
                UpdateTelegraph(nextPattern, pattern5FirePoint, pattern7FirePoint, rotateFirePointToPlayer);
                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        public IEnumerator ReloadWavePreview(
            HogBossAI.PatternKind pattern,
            float duration,
            Action<HogBossAI.PatternKind, float> beginPreview,
            Action<float, float> updatePreview,
            Action<HogBossAI.PatternKind> beginPlayback,
            Func<bool> isExecutionPaused,
            Action stop)
        {
            float reloadDuration = Mathf.Max(0f, duration);
            float remaining = reloadDuration;
            if (remaining <= 0f)
            {
                beginPlayback?.Invoke(pattern);
                yield break;
            }

            beginPreview?.Invoke(pattern, remaining);

            while (remaining > 0f)
            {
                if (isExecutionPaused?.Invoke() == true)
                {
                    stop?.Invoke();
                    yield return null;
                    continue;
                }

                updatePreview?.Invoke(reloadDuration, remaining);
                remaining -= Time.deltaTime;
                yield return null;
            }

            beginPlayback?.Invoke(pattern);
        }

        private static void BeginTelegraph(
            HogBossAI.PatternKind nextPattern,
            HogBossAI.FirePoint pattern5FirePoint,
            HogBossAI.FirePoint pattern7FirePoint,
            Action<HogBossAI.FirePoint, bool> setFirePointActive,
            Action<HogBossAI.FirePoint> rotateFirePointToPlayer)
        {
            if (nextPattern != HogBossAI.PatternKind.Pattern5 && nextPattern != HogBossAI.PatternKind.Pattern7)
            {
                return;
            }

            HogBossAI.FirePoint firePoint = nextPattern == HogBossAI.PatternKind.Pattern7
                ? pattern7FirePoint
                : pattern5FirePoint;
            setFirePointActive?.Invoke(firePoint, true);
            rotateFirePointToPlayer?.Invoke(firePoint);
        }

        private static void UpdateTelegraph(
            HogBossAI.PatternKind nextPattern,
            HogBossAI.FirePoint pattern5FirePoint,
            HogBossAI.FirePoint pattern7FirePoint,
            Action<HogBossAI.FirePoint> rotateFirePointToPlayer)
        {
            if (nextPattern == HogBossAI.PatternKind.Pattern5)
            {
                rotateFirePointToPlayer?.Invoke(pattern5FirePoint);
            }
            else if (nextPattern == HogBossAI.PatternKind.Pattern7)
            {
                rotateFirePointToPlayer?.Invoke(pattern7FirePoint);
            }
        }
    }
}
