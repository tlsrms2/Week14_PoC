using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class DronePatternSelector
    {
        internal readonly struct Settings
        {
            public Settings(
                IReadOnlyList<DronePilot.PatternKind> patternSequence,
                bool randomizePatterns)
            {
                PatternSequence = patternSequence;
                RandomizePatterns = randomizePatterns;
            }

            public IReadOnlyList<DronePilot.PatternKind> PatternSequence { get; }
            public bool RandomizePatterns { get; }
        }

        private int nextBossPatternIndex;
        private int nextDronePatternIndex;

        public DronePilot.PatternKind SelectBossPattern(Settings settings)
        {
            return SelectPattern(
                settings,
                true,
                ref nextBossPatternIndex,
                DronePilot.PatternKind.BossBurst);
        }

        public DronePilot.PatternKind SelectDronePattern(Settings settings)
        {
            return SelectPattern(
                settings,
                false,
                ref nextDronePatternIndex,
                DronePilot.PatternKind.DronePattern1);
        }

        private static DronePilot.PatternKind SelectPattern(
            Settings settings,
            bool bossPattern,
            ref int nextIndex,
            DronePilot.PatternKind fallback)
        {
            IReadOnlyList<DronePilot.PatternKind> patternSequence = settings.PatternSequence;
            if (patternSequence == null || patternSequence.Count == 0)
            {
                return fallback;
            }

            return settings.RandomizePatterns
                ? SelectRandomPattern(patternSequence, bossPattern, fallback)
                : SelectSequentialPattern(patternSequence, bossPattern, ref nextIndex, fallback);
        }

        private static DronePilot.PatternKind SelectRandomPattern(
            IReadOnlyList<DronePilot.PatternKind> patternSequence,
            bool bossPattern,
            DronePilot.PatternKind fallback)
        {
            int matchCount = 0;
            for (int i = 0; i < patternSequence.Count; i++)
            {
                if (IsPatternGroup(patternSequence[i], bossPattern))
                {
                    matchCount++;
                }
            }

            if (matchCount <= 0)
            {
                return fallback;
            }

            int selected = Random.Range(0, matchCount);
            for (int i = 0; i < patternSequence.Count; i++)
            {
                if (!IsPatternGroup(patternSequence[i], bossPattern))
                {
                    continue;
                }

                if (selected-- <= 0)
                {
                    return patternSequence[i];
                }
            }

            return fallback;
        }

        private static DronePilot.PatternKind SelectSequentialPattern(
            IReadOnlyList<DronePilot.PatternKind> patternSequence,
            bool bossPattern,
            ref int nextIndex,
            DronePilot.PatternKind fallback)
        {
            for (int i = 0; i < patternSequence.Count; i++)
            {
                int index = nextIndex % patternSequence.Count;
                DronePilot.PatternKind pattern = patternSequence[index];
                nextIndex++;
                if (IsPatternGroup(pattern, bossPattern))
                {
                    return pattern;
                }
            }

            return fallback;
        }

        private static bool IsPatternGroup(DronePilot.PatternKind pattern, bool bossPattern)
        {
            if (pattern == DronePilot.PatternKind.SummonDrone)
            {
                return false;
            }

            bool isBossPattern = pattern == DronePilot.PatternKind.BossBurst;
            return bossPattern ? isBossPattern : !isBossPattern;
        }
    }
}
