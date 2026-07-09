using UnityEngine;
using Week14.Save;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Phase Reach Challenge", fileName = "PhaseReachChallenge")]
    public sealed class PhaseReachChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("도달해야 하는 목표 페이즈 번호입니다 (1부터 시작).")]
        [SerializeField, Min(1)] private int targetPhaseNumber = 2;
        [Tooltip("여러 전투 시도에 걸쳐 목표 페이즈까지 도달해야 하는 누적 횟수입니다.")]
        [SerializeField, Min(1)] private int requiredReachCount = 1;

        public override ChallengeType Kind => ChallengeType.PhaseReach;

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(targetPhaseNumber, requiredReachCount);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly int targetPhaseNumber;
            private readonly int requiredReachCount;
            private bool reachedThisRun;

            public RunState(int targetPhaseNumber, int requiredReachCount)
            {
                this.targetPhaseNumber = targetPhaseNumber;
                this.requiredReachCount = requiredReachCount;
            }

            public override void OnPhaseReached(int phaseNumber)
            {
                if (phaseNumber >= targetPhaseNumber)
                {
                    reachedThisRun = true;
                }
            }

            public override bool TryFinalize(bool victory, string challengeId)
            {
                if (!reachedThisRun)
                {
                    return false;
                }

                int count = GameSaveManager.IncrementChallengeCounter(challengeId);
                return count >= requiredReachCount;
            }
        }
    }
}
