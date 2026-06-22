using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class HogPatternSelector
    {
        private static readonly HogBossAI.PatternKind[] DefaultPatterns =
        {
            HogBossAI.PatternKind.Pattern1,
            HogBossAI.PatternKind.Pattern2,
            HogBossAI.PatternKind.Pattern3,
            HogBossAI.PatternKind.Pattern4,
            HogBossAI.PatternKind.Pattern5,
            HogBossAI.PatternKind.Pattern6,
            HogBossAI.PatternKind.Pattern7
        };

        private int nextPatternIndex;
        private bool hasLastPattern;
        private HogBossAI.PatternKind lastPattern;

        public List<HogBossAI.PhasePatternSet> EnsurePhasePatternSlots(
            List<HogBossAI.PhasePatternSet> phasePatterns,
            int maxLives)
        {
            phasePatterns ??= new List<HogBossAI.PhasePatternSet>();
            while (phasePatterns.Count < maxLives)
            {
                phasePatterns.Add(new HogBossAI.PhasePatternSet { Phase = phasePatterns.Count + 1 });
            }

            return phasePatterns;
        }

        public void EnsurePhasePatternLabels(IReadOnlyList<HogBossAI.PhasePatternSet> phasePatterns)
        {
            if (phasePatterns == null)
            {
                return;
            }

            for (int i = 0; i < phasePatterns.Count; i++)
            {
                if (phasePatterns[i] != null)
                {
                    phasePatterns[i].Phase = i + 1;
                }
            }
        }

        public void Reset()
        {
            nextPatternIndex = 0;
            hasLastPattern = false;
        }

        public void RecordExecuted(HogBossAI.PatternKind pattern)
        {
            lastPattern = pattern;
            hasLastPattern = true;
        }

        public HogBossAI.PatternKind Select(
            IReadOnlyList<HogBossAI.PhasePatternSet> phasePatterns,
            int currentPhaseIndex,
            bool randomizePatterns,
            bool preventRandomRepeatPattern,
            bool debugUseFixedPattern,
            HogBossAI.PatternKind debugPattern)
        {
            if (debugUseFixedPattern)
            {
                return debugPattern;
            }

            IReadOnlyList<HogBossAI.PatternKind> availablePatterns = GetCurrentPhasePatterns(phasePatterns, currentPhaseIndex);
            if (randomizePatterns)
            {
                return SelectRandomPattern(availablePatterns, preventRandomRepeatPattern);
            }

            HogBossAI.PatternKind pattern = availablePatterns[nextPatternIndex % availablePatterns.Count];
            nextPatternIndex++;
            return pattern;
        }

        private static IReadOnlyList<HogBossAI.PatternKind> GetCurrentPhasePatterns(
            IReadOnlyList<HogBossAI.PhasePatternSet> phasePatterns,
            int currentPhaseIndex)
        {
            HogBossAI.PhasePatternSet phasePatternSet = GetCurrentPhasePatternSet(phasePatterns, currentPhaseIndex);
            if (phasePatternSet != null && phasePatternSet.Patterns != null && phasePatternSet.Patterns.Count > 0)
            {
                return phasePatternSet.Patterns;
            }

            return DefaultPatterns;
        }

        private static HogBossAI.PhasePatternSet GetCurrentPhasePatternSet(
            IReadOnlyList<HogBossAI.PhasePatternSet> phasePatterns,
            int currentPhaseIndex)
        {
            if (phasePatterns == null || phasePatterns.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(currentPhaseIndex, 0, phasePatterns.Count - 1);
            return phasePatterns[index];
        }

        private HogBossAI.PatternKind SelectRandomPattern(
            IReadOnlyList<HogBossAI.PatternKind> availablePatterns,
            bool preventRandomRepeatPattern)
        {
            if (availablePatterns == null || availablePatterns.Count == 0)
            {
                return HogBossAI.PatternKind.Pattern1;
            }

            if (!preventRandomRepeatPattern || !hasLastPattern || availablePatterns.Count <= 1)
            {
                return availablePatterns[Random.Range(0, availablePatterns.Count)];
            }

            int repeatCount = 0;
            for (int i = 0; i < availablePatterns.Count; i++)
            {
                if (availablePatterns[i] == lastPattern)
                {
                    repeatCount++;
                }
            }

            int candidateCount = availablePatterns.Count - repeatCount;
            if (candidateCount <= 0)
            {
                return availablePatterns[Random.Range(0, availablePatterns.Count)];
            }

            int selectedIndex = Random.Range(0, candidateCount);
            for (int i = 0; i < availablePatterns.Count; i++)
            {
                if (availablePatterns[i] == lastPattern)
                {
                    continue;
                }

                if (selectedIndex == 0)
                {
                    return availablePatterns[i];
                }

                selectedIndex--;
            }

            return availablePatterns[Random.Range(0, availablePatterns.Count)];
        }
    }
}
