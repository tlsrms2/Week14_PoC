using UnityEngine;

namespace Week14.Challenge
{
    [CreateAssetMenu(menuName = "Week14/Challenge/Hit Limit Challenge", fileName = "HitLimitChallenge")]
    public sealed class HitLimitChallengeSO : ChallengeDefinitionSO
    {
        [Tooltip("전투 중 이 횟수 이하로 피격해야 달성됩니다.")]
        [SerializeField, Min(0)] private int maxHits = 3;

        public override ChallengeType Kind => ChallengeType.HitLimit;

        public override ChallengeRunState CreateRunState()
        {
            return new RunState(maxHits);
        }

        private sealed class RunState : ChallengeRunState
        {
            private readonly int maxHits;

            public RunState(int maxHits)
            {
                this.maxHits = maxHits;
            }

            public override void OnPlayerHit(int totalHitsThisRun)
            {
                if (State == ChallengeState.Active && totalHitsThisRun > maxHits)
                {
                    State = ChallengeState.Failed;
                }
            }

            public override bool TryFinalize(bool victory, string challengeId)
            {
                return victory && State != ChallengeState.Failed;
            }
        }
    }
}
