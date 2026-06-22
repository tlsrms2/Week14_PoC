using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        private PatternKind SelectPattern()
        {
            patternSelector.EnsurePhasePatterns(ref phasePatterns, MaxLives);
            return patternSelector.SelectNext(new HogPatternSelector.Settings(
                phasePatterns,
                CurrentPhaseIndex,
                randomizePatterns,
                preventRandomRepeatPattern,
                debugUseFixedPattern,
                debugPattern));
        }

        private IEnumerator RunPatternLoop()
        {
            PatternKind pattern = SelectPattern();
            while (true)
            {
                yield return RunPattern(pattern);
                yield return FinishPattern();

                PatternKind nextPattern = SelectPattern();
                yield return RecoverBeforeNextPattern(nextPattern);
                pattern = nextPattern;
            }
        }

        private IEnumerator FinishPattern()
        {
            yield return ApplyPendingEnrageIfAny();
            Stop();
        }

        private IEnumerator RecoverBeforeNextPattern(PatternKind nextPattern)
        {
            float recoverySeconds = patternRecovery.GetRecoverySeconds(minPatternRecoverySeconds, maxPatternRecoverySeconds);
            if (recoverySeconds > 0f)
            {
                yield return RunPatternRecovery(nextPattern, recoverySeconds);
            }
        }

        private IEnumerator RunPattern(PatternKind pattern)
        {
            BeginPatternBulletPreviewPlayback(pattern);

            switch (pattern)
            {
                case PatternKind.Pattern1:
                    yield return RunPattern1();
                    break;
                case PatternKind.Pattern2:
                    yield return RunPattern2();
                    break;
                case PatternKind.Pattern3:
                    yield return RunPattern3();
                    break;
                case PatternKind.Pattern4:
                    yield return RunPattern4();
                    break;
                case PatternKind.Pattern5:
                    yield return RunPattern5();
                    break;
                case PatternKind.Pattern6:
                    yield return RunPattern6();
                    break;
                case PatternKind.Pattern7:
                    yield return RunPattern7();
                    break;
                default:
                    yield return RunPattern1();
                    break;
            }
        }

        private IEnumerator RunPatternRecovery(PatternKind nextPattern, float duration)
        {
            float recoveryDuration = Mathf.Max(0f, duration);
            BeginPatternBulletPreview(nextPattern, recoveryDuration);
            BeginRecoveryTelegraph(nextPattern);

            yield return patternRecovery.RunRecovery(
                recoveryDuration,
                (total, remaining) =>
                {
                    UpdatePatternBulletPreviewLoading(total, remaining);
                    UpdateRecoveryTelegraph(nextPattern);
                },
                IsBossExecutionPaused,
                Stop);
        }

        private void BeginRecoveryTelegraph(PatternKind nextPattern)
        {
            if (nextPattern != PatternKind.Pattern5 && nextPattern != PatternKind.Pattern7)
            {
                return;
            }

            FirePoint firePoint = nextPattern == PatternKind.Pattern7 ? pattern7.FirePoint : pattern5.FirePoint;
            SetFirePointActive(firePoint, true);
            RotateFirePointToPlayer(firePoint);
        }

        private void UpdateRecoveryTelegraph(PatternKind nextPattern)
        {
            if (nextPattern == PatternKind.Pattern5)
            {
                RotateFirePointToPlayer(pattern5.FirePoint);
            }
            else if (nextPattern == PatternKind.Pattern7)
            {
                RotateFirePointToPlayer(pattern7.FirePoint);
            }
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            patternSelector.Reset();
            patternSelector.EnsurePhasePatterns(ref phasePatterns, MaxLives);
            HidePatternBulletPreview();
        }

    }
}
